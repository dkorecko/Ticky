using System.Drawing;
using Ticky.Base.Models;

namespace Ticky.Internal.Services;

public class TrelloImportService
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;
    private readonly ILogger<TrelloImportService> _logger;

    public TrelloImportService(IDbContextFactory<DataContext> dbContextFactory, ILogger<TrelloImportService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task ImportTrelloBoardAsync(
        ImportModel importModel,
        int projectId,
        int currentUserId
    )
    {
        using var db = _dbContextFactory.CreateDbContext();
        var importDto = importModel.ImportDto!;

        Dictionary<string, User> trelloIdToUser = new();

        if (importModel.MemberIdentifiers is not null && importModel.MemberIdentifiers.Length != 0)
        {
            var users = await db.Users
                .Where(u => importModel.MemberIdentifiers.Contains(u.Email) || importModel.MemberIdentifiers.Contains(u.DisplayName))
                .ToListAsync();

            for (int i = 0; i < importModel.MemberIdentifiers.Length; i++)
            {
                var identifier = importModel.MemberIdentifiers[i];
                var trelloId = importDto.Members[i].Id;

                var user = users.FirstOrDefault(x =>
                    // Match the database's case-insensitive collation used by the query above.
                    identifier.Equals(x.Email, StringComparison.OrdinalIgnoreCase)
                    || identifier.Equals(x.DisplayName, StringComparison.OrdinalIgnoreCase)
                );

                if (user is null)
                    continue;

                trelloIdToUser.TryAdd(trelloId, user);
            }
        }

        using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            var board = new Board
            {
                Name = importDto.Name,
                Description = importDto.Description,
                ProjectId = projectId,
                Code = importModel.Code
            };

            foreach (var label in importDto.Labels)
            {
                var (textColor, backgroundColor) = GetLabelColorsFromName(label.Color);

                board.Labels.Add(
                    new Label
                    {
                        Name = label.Name,
                        BoardId = board.Id,
                        TextColor = textColor,
                        BackgroundColor = backgroundColor
                    }
                );
            }

            int columnIndex = 0;
            int cardNumber = 1;

            foreach (var list in importDto.Lists)
            {
                if (
                    importModel.ArchivedCardsHandling.Equals(TrelloArchivedHandlingType.DontAdd)
                    && list.Closed
                )
                    continue;

                var column = new Column
                {
                    Name = list.Name,
                    BoardId = board.Id,
                    Index = columnIndex,
                    MaxCards = list.SoftLimit ?? 0,
                    Finished =
                        importDto
                            .Cards.Where(x => x.IdList.Equals(list.Id))
                            .Any(x => x.DueComplete || x.DateCompleted.HasValue)
                        || list.Name.Contains("Done")
                        || list.Name.Contains("🎉")
                };

                int columnCardIndex = 0;
                foreach (var card in importDto.Cards.Where(x => x.IdList.Equals(list.Id)))
                {
                    if (
                        importModel.ArchivedCardsHandling.Equals(TrelloArchivedHandlingType.DontAdd)
                        && card.Closed
                    )
                        continue;

                    var newCard = new Card
                    {
                        Name = card.Name,
                        Description = card.Description,
                        Index = columnCardIndex,
                        Deadline = card.Due,
                        CreatedById = card.IdMemberCreator is not null
                            ? (
                                trelloIdToUser.GetValueOrDefault(card.IdMemberCreator)?.Id
                                ?? currentUserId
                            )
                            : currentUserId,
                        Number = cardNumber,
                        ColumnId = column.Id,
                    };

                    if (card.DueReminder.HasValue && card.Due.HasValue)
                    {
                        var calculatedAt = card.Due.Value.AddMinutes(-card.DueReminder.Value);

                        if (calculatedAt > DateTime.Now)
                        {
                            newCard.Reminders.Add(
                                new Reminder
                                {
                                    At = card.Due.Value.AddMinutes(-card.DueReminder.Value),
                                    CardId = newCard.Id
                                }
                            );
                        }
                        else
                        {
                            newCard.DeadlineProcessed = false;
                        }
                    }

                    if (card.Due.HasValue && card.Due.Value < DateTime.Now)
                    {
                        newCard.DeadlineProcessed = true;
                    }

                    var appliedLabels = card
                        .IdLabels.Select(x => importDto.Labels.First(l => l.Id.Equals(x)))
                        .Select(x => board.Labels.First(y => y.Name.Equals(x.Name)));

                    if (appliedLabels.Any())
                        newCard.Labels.AddRange(appliedLabels);

                    int subtaskIndex = 0;
                    foreach (
                        var checklists in importDto
                            .Checklists.Where(x => x.IdCard.Equals(card.Id))
                            .Select(x => x.CheckItems)
                    )
                    {
                        foreach (var checklist in checklists)
                        {
                            newCard.Subtasks.Add(
                                new()
                                {
                                    CardId = newCard.Id,
                                    Text = checklist.Name,
                                    Index = subtaskIndex,
                                    Completed = checklist.State == "complete"
                                }
                            );
                            subtaskIndex++;
                        }
                    }

                    foreach (var cardMember in card.IdMembers)
                    {
                        if (trelloIdToUser.TryGetValue(cardMember, out var user))
                            newCard.Assignees.Add(user);
                    }

                    column.Cards.Add(newCard);
                    cardNumber++;
                    columnCardIndex++;
                }

                board.Columns.Add(column);
                columnIndex++;
            }

            db.Boards.Add(board);

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static readonly Dictionary<string, (Color textColor, Color backgroundColor)> _labelColors = new()
    {
        { "purple", (Color.FromArgb(53, 44, 99), Color.FromArgb(159, 143, 239)) },
        { "purple_dark", (Color.White, Color.FromArgb(110, 93, 198)) },
        { "purple_light", (Color.FromArgb(53, 44, 99), Color.FromArgb(223, 216, 253)) },
        { "red", (Color.FromArgb(93, 31, 26), Color.FromArgb(248, 113, 104)) },
        { "red_dark", (Color.White, Color.FromArgb(201, 55, 44)) },
        { "red_light", (Color.FromArgb(93, 31, 26), Color.FromArgb(255, 213, 210)) },
        { "orange", (Color.FromArgb(112, 46, 0), Color.FromArgb(254, 163, 98)) },
        { "orange_dark", (Color.White, Color.FromArgb(194, 81, 0)) },
        { "orange_light", (Color.FromArgb(112, 46, 0), Color.FromArgb(254, 222, 200)) },
        { "yellow", (Color.FromArgb(83, 63, 4), Color.FromArgb(245, 205, 71)) },
        { "yellow_dark", (Color.White, Color.FromArgb(148, 111, 0)) },
        { "yellow_light", (Color.FromArgb(83, 63, 4), Color.FromArgb(248, 230, 160)) },
        { "green", (Color.FromArgb(22, 75, 53), Color.FromArgb(75, 206, 151)) },
        { "green_dark", (Color.White, Color.FromArgb(31, 132, 90)) },
        { "green_light", (Color.FromArgb(22, 75, 53), Color.FromArgb(186, 243, 219)) },
        { "blue", (Color.FromArgb(9, 50, 108), Color.FromArgb(87, 157, 255)) },
        { "blue_dark", (Color.White, Color.FromArgb(12, 102, 228)) },
        { "blue_light", (Color.FromArgb(9, 50, 108), Color.FromArgb(204, 224, 255)) },
        { "sky", (Color.FromArgb(22, 69, 85), Color.FromArgb(108, 195, 224)) },
        { "sky_dark", (Color.White, Color.FromArgb(34, 125, 155)) },
        { "sky_light", (Color.FromArgb(22, 69, 85), Color.FromArgb(198, 237, 251)) },
        { "lime", (Color.FromArgb(55, 71, 31), Color.FromArgb(148, 199, 72)) },
        { "lime_dark", (Color.White, Color.FromArgb(91, 127, 36)) },
        { "lime_light", (Color.FromArgb(55, 71, 31), Color.FromArgb(211, 241, 167)) },
        { "pink", (Color.FromArgb(80, 37, 63), Color.FromArgb(231, 116, 187)) },
        { "pink_dark", (Color.White, Color.FromArgb(174, 71, 135)) },
        { "pink_light", (Color.FromArgb(80, 37, 63), Color.FromArgb(253, 208, 236)) },
        { "black", (Color.FromArgb(9, 30, 66), Color.FromArgb(133, 144, 162)) },
        { "black_dark", (Color.White, Color.FromArgb(98, 111, 134)) },
        { "black_light", (Color.FromArgb(9, 30, 66), Color.FromArgb(220, 223, 228)) }
    };

    private (Color textColor, Color backgroundColor) GetLabelColorsFromName(string colorName)
    {
        if (_labelColors.TryGetValue(colorName, out var colors))
            return colors;

        _logger.LogWarning("Unknown Trello color: {ColorName}", colorName);
        return (Color.Black, Color.White);
    }
}
