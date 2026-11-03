namespace Ticky.Internal.Services.Hosted;

public class RepeatHostedService(IServiceScopeFactory serviceScopeFactory) : AbstractHostedService<RepeatHostedService>(
    serviceScopeFactory,
    TimeSpan.FromSeconds(Constants.Limits.MINIMUM_SECOND_HOSTED_SERVICE_DELAY),
    TimeSpan.FromMinutes(2))
{
    protected override async Task OnRun()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>()!;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RepeatHostedService>>()!;
        var cardNumberingService =
            scope.ServiceProvider.GetRequiredService<CardNumberingService>()!;

        var allRepeatCards = await db.Cards.Where(x => x.RepeatInfo != null).ToListAsync();

        var pendingNextRepeatCardIds = allRepeatCards
            .Where(x => x.CalculateNextRepeat(DateTime.Now) < DateTime.Now)
            .Select(x => x.Id)
            .ToList();

        var populatedPendingRepeatCards = await db
            .Cards.Include(x => x.Column)
            .ThenInclude(x => x.Board)
            .Include(x => x.Subtasks)
            .Include(x => x.Assignees)
            .Include(x => x.Labels)
            .Include(x => x.Attachments)
            .Include(x => x.Reminders)
            .Include(x => x.LinkedIssuesOne)
            .Include(x => x.LinkedIssuesTwo)
            .Where(x => pendingNextRepeatCardIds.Contains(x.Id))
            .AsSplitQuery()
            .ToListAsync();

        var createdActivities = new List<(RepeatCardCreatedActivity Activity, Card NewCard)>();

        foreach (var card in populatedPendingRepeatCards)
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
                Flagged = card.Flagged,
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
                new RepeatedFromActivity
                {
                    CardId = newCard.Id,
                    RelatedCardId = card.Id,
                    RelatedCardCode = identifier,
                    UserId = card.CreatedById
                }
            );

            var oppositeCategory = Constants.LINK_TYPE_PAIRS.ContainsKey(Constants.REPEATED_KEY)
                ? Constants.LINK_TYPE_PAIRS[Constants.REPEATED_KEY]
                : Constants.LINK_TYPE_PAIRS.First(x => x.Value.Equals(Constants.REPEATED_KEY)).Key;

            foreach (var linkedIssueOne in card.LinkedIssuesOne.Where(linkedIssueOne => !linkedIssueOne.Category.Equals(Constants.REPEATED_KEY)
                         && !linkedIssueOne.Category.Equals(oppositeCategory)))
            {
                newCard.LinkedIssuesOne.Add(
                    new()
                    {
                        CardOneId = newCard.Id,
                        CardTwoId = linkedIssueOne.CardTwoId,
                        Category = linkedIssueOne.Category
                    }
                );
            }

            foreach (var linkedIssueTwo in card.LinkedIssuesTwo.Where(linkedIssueTwo => !linkedIssueTwo.Category.Equals(Constants.REPEATED_KEY)
                         && !linkedIssueTwo.Category.Equals(oppositeCategory)))
            {
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
            // The new card's id only exists after saving; it is filled in below.
            var createdActivity = new RepeatCardCreatedActivity
            {
                CardId = card.Id,
                RelatedCardCode = newIdentifier,
                UserId = card.CreatedById
            };
            card.Activities.Add(createdActivity);
            createdActivities.Add((createdActivity, newCard));

            PlaceNewCard(targetColumn.Cards, newCard, card.RepeatInfo.CardPlacement);
        }

        if (populatedPendingRepeatCards.Count != 0)
        {
            await db.SaveChangesAsync();

            foreach (var (activity, newCard) in createdActivities)
                activity.RelatedCardId = newCard.Id;

            await db.SaveChangesAsync();
            logger.LogInformation("{Count} cards have been repeated.", populatedPendingRepeatCards.Count);
        }
    }

    /// <summary>Adds a repeated card to the top or bottom of a column's cards.</summary>
    public static void PlaceNewCard(List<Card> columnCards, Card newCard, CardPlacement placement)
    {
        switch (placement)
        {
            case CardPlacement.Top:
                columnCards.ForEach(x => x.Index++);
                newCard.Index = 0;
                break;
            case CardPlacement.Bottom:
                newCard.Index = columnCards.Count == 0 ? 0 : columnCards.Max(x => x.Index) + 1;
                break;
            default:
                throw new Exception("Unsupported card placement type");
        }

        columnCards.Add(newCard);
    }
}
