using System.Drawing;
using Ticky.Base.Models;

namespace Ticky.Internal.Services;

public class TrelloImportService
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory;

    public TrelloImportService(IDbContextFactory<DataContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
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
            for (int i = 0; i < importModel.MemberIdentifiers.Length; i++)
            {
                var identifier = importModel.MemberIdentifiers[i];
                var trelloId = importDto.Members[i].Id;

                var user = await db.Users.FirstOrDefaultAsync(x =>
                    identifier.Equals(x.Email) || identifier.Equals(x.DisplayName)
                );

                if (user is null)
                    continue;

                trelloIdToUser.Add(trelloId, user);
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
                if (list.Closed)
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
                        newCard.Reminders.Add(
                            new Reminder
                            {
                                At = card.Due.Value.AddMinutes(-card.DueReminder.Value),
                                CardId = newCard.Id
                            }
                        );

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

    private (Color textColor, Color backgroundColor) GetLabelColorsFromName(string colorName)
    {
        switch (colorName)
        {
            case "purple":
                return (Color.FromArgb(53, 44, 99), Color.FromArgb(159, 143, 239));

            case "purple_dark":
                return (Color.White, Color.FromArgb(110, 93, 198));
            case "purple_light":
                return (Color.FromArgb(53, 44, 99), Color.FromArgb(223, 216, 253));

            case "red":
                return (Color.FromArgb(93, 31, 26), Color.FromArgb(248, 113, 104));

            case "red_dark":
                return (Color.White, Color.FromArgb(201, 55, 44));
            case "red_light":
                return (Color.FromArgb(93, 31, 26), Color.FromArgb(255, 213, 210));

            case "orange":
                return (Color.FromArgb(112, 46, 0), Color.FromArgb(254, 163, 98));

            case "orange_dark":
                return (Color.White, Color.FromArgb(194, 81, 0));
            case "orange_light":
                return (Color.FromArgb(112, 46, 0), Color.FromArgb(254, 222, 200));

            case "yellow":
                return (Color.FromArgb(83, 63, 4), Color.FromArgb(245, 205, 71));

            case "yellow_dark":
                return (Color.White, Color.FromArgb(148, 111, 0));
            case "yellow_light":
                return (Color.FromArgb(83, 63, 4), Color.FromArgb(248, 230, 160));

            case "green":
                return (Color.FromArgb(22, 75, 53), Color.FromArgb(75, 206, 151));

            case "green_dark":
                return (Color.White, Color.FromArgb(31, 132, 90));
            case "green_light":
                return (Color.FromArgb(22, 75, 53), Color.FromArgb(186, 243, 219));

            case "blue":
                return (Color.FromArgb(9, 50, 108), Color.FromArgb(87, 157, 255));

            case "blue_dark":
                return (Color.White, Color.FromArgb(12, 102, 228));
            case "blue_light":
                return (Color.FromArgb(9, 50, 108), Color.FromArgb(204, 224, 255));

            case "sky":
                return (Color.FromArgb(22, 69, 85), Color.FromArgb(108, 195, 224));

            case "sky_dark":
                return (Color.White, Color.FromArgb(34, 125, 155));
            case "sky_light":
                return (Color.FromArgb(22, 69, 85), Color.FromArgb(198, 237, 251));

            case "lime":
                return (Color.FromArgb(55, 71, 31), Color.FromArgb(148, 199, 72));

            case "lime_dark":
                return (Color.White, Color.FromArgb(91, 127, 36));
            case "lime_light":
                return (Color.FromArgb(55, 71, 31), Color.FromArgb(211, 241, 167));

            case "pink":
                return (Color.FromArgb(80, 37, 63), Color.FromArgb(231, 116, 187));

            case "pink_dark":
                return (Color.White, Color.FromArgb(174, 71, 135));
            case "pink_light":
                return (Color.FromArgb(80, 37, 63), Color.FromArgb(253, 208, 236));

            case "black":
                return (Color.FromArgb(9, 30, 66), Color.FromArgb(133, 144, 162));

            case "black_dark":
                return (Color.White, Color.FromArgb(98, 111, 134));
            case "black_light":
                return (Color.FromArgb(9, 30, 66), Color.FromArgb(220, 223, 228));
        }

        throw new Exception(
            "Unknown color. Please open a GitHub issue and include the application logs."
        );
    }
}
