namespace Ticky.Internal.Services.Hosted;

public class RepeatHostedService : AbstractHostedService<RepeatHostedService>
{
    public RepeatHostedService(IServiceScopeFactory serviceScopeFactory)
        : base(
            serviceScopeFactory,
            TimeSpan.FromSeconds(Constants.Limits.MINIMUM_SECOND_HOSTED_SERVICE_DELAY),
            TimeSpan.FromMinutes(2)
        ) { }

    protected override async void OnRun()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<DataContext>()!;
        var logger = scope.ServiceProvider.GetService<ILogger<RepeatHostedService>>()!;
        var cardNumberingService = scope.ServiceProvider.GetService<CardNumberingService>()!;

        var allRepeatCards = await db
            .Cards.Include(x => x.Column)
            .ThenInclude(x => x.Board)
            .Include(x => x.Subtasks)
            .Include(x => x.Assignees)
            .Include(x => x.Labels)
            .Include(x => x.Attachments)
            .Include(x => x.Reminders)
            .Include(x => x.Activities)
            .Include(x => x.LinkedIssuesOne)
            .Include(x => x.LinkedIssuesTwo)
            .ThenInclude(x => x.CardOne)
            .Where(x => x.RepeatInfo != null)
            .ToListAsync();

        var pendingNextRepeatCards = allRepeatCards
            .Where(x => x.CalculateNextRepeat(DateTime.Now) < DateTime.Now)
            .ToList();

        foreach (var card in pendingNextRepeatCards)
        {
            card.RepeatInfo!.LastRepeat = DateTime.Now;

            var targetColumn = await db
                .Columns.Where(x => x.Id.Equals(card.RepeatInfo.TargetColumnId))
                .Include(x => x.Cards)
                .FirstOrDefaultAsync();

            if (targetColumn is null)
                targetColumn = await db
                    .Columns.Where(x => x.BoardId.Equals(card.Column.BoardId))
                    .Include(x => x.Cards)
                    .OrderBy(x => x.Index)
                    .FirstOrDefaultAsync();

            if (targetColumn is null)
            {
                targetColumn = new Column()
                {
                    BoardId = card.Column.BoardId,
                    Name = "Default",
                    Index = 0
                };

                db.Columns.Add(targetColumn);
            }

            var newCard = new Card
            {
                Name = card.Name,
                Description = card.Description,
                Deadline = card.Deadline,
                Priority = card.Priority,
                CreatedById = card.CreatedById,
                SnoozedUntil = card.SnoozedUntil,
                Blocked = card.Blocked,
                ColumnId = targetColumn.Id,
                Number = await cardNumberingService.GetNextNumberAsync(targetColumn.BoardId),
                Index = 0,
                Assignees = card.Assignees,
                Labels = card.Labels
            };

            newCard.Attachments =
            [
                .. card.Attachments.Select(x => new Attachment()
                {
                    CardId = newCard.Id,
                    FileName = x.FileName,
                    OriginalName = x.OriginalName
                })
            ];
            newCard.Reminders =
            [
                .. card.Reminders.Select(x => new Reminder() { CardId = newCard.Id, At = x.At })
            ];
            newCard.Subtasks =
            [
                .. card.Subtasks.Select(x => new Subtask()
                {
                    CardId = newCard.Id,
                    Completed = x.Completed,
                    Index = x.Index,
                    Text = x.Text
                })
            ];

            var identifier = $"{card.Column.Board.Code}-{card.Number}";

            newCard.Activities.Add(
                new()
                {
                    CardId = newCard.Id,
                    Text = $"<b>was repeated</b> from card <b>{identifier}</b>",
                    UserId = card.CreatedById
                }
            );

            var oppositeCategory = Constants.LINK_TYPE_PAIRS.ContainsKey(Constants.REPEATED_KEY)
                ? Constants.LINK_TYPE_PAIRS[Constants.REPEATED_KEY]
                : Constants.LINK_TYPE_PAIRS.First(x => x.Value.Equals(Constants.REPEATED_KEY)).Key;

            foreach (var linkedIssueOne in card.LinkedIssuesOne)
            {
                if (
                    linkedIssueOne.Category.Equals(Constants.REPEATED_KEY)
                    || linkedIssueOne.Category.Equals(oppositeCategory)
                )
                    continue;

                newCard.LinkedIssuesOne.Add(
                    new()
                    {
                        CardOneId = newCard.Id,
                        CardTwoId = linkedIssueOne.CardTwoId,
                        Category = linkedIssueOne.Category
                    }
                );
            }

            foreach (var linkedIssueTwo in card.LinkedIssuesTwo)
            {
                if (
                    linkedIssueTwo.Category.Equals(Constants.REPEATED_KEY)
                    || linkedIssueTwo.Category.Equals(oppositeCategory)
                )
                    continue;

                newCard.LinkedIssuesTwo.Add(
                    new()
                    {
                        CardOneId = linkedIssueTwo.CardOneId,
                        CardTwoId = newCard.Id,
                        Category = linkedIssueTwo.Category
                    }
                );
            }

            newCard.LinkedIssuesOne.Add(
                new()
                {
                    CardOneId = newCard.Id,
                    CardTwoId = card.Id,
                    Category = Constants.REPEATED_KEY
                }
            );

            newCard.LinkedIssuesTwo.Add(
                new()
                {
                    CardOneId = card.Id,
                    CardTwoId = newCard.Id,
                    Category = oppositeCategory
                }
            );

            var newIdentifier = $"{card.Column.Board.Code}-{newCard.Number}";
            card.Activities.Add(
                new()
                {
                    CardId = card.Id,
                    Text = $"<b>created</b> repeat card <b>{newIdentifier}</b>",
                    UserId = card.CreatedById
                }
            );

            targetColumn.Cards.Add(newCard);

            if (card.RepeatInfo.CardPlacement.Equals(CardPlacement.Top))
                targetColumn.Cards.ForEach(x => x.Index++);
            else if (
                card.RepeatInfo.CardPlacement.Equals(CardPlacement.Bottom)
                && targetColumn.Cards.Any()
            )
                newCard.Index = targetColumn.Cards.Max(x => x.Index) + 1;
            else
                throw new Exception("Unsupported card placement type");
        }

        if (pendingNextRepeatCards.Any())
        {
            await db.SaveChangesAsync();
            logger.LogInformation($"{pendingNextRepeatCards.Count()} cards have been repeated.");
        }
    }
}
