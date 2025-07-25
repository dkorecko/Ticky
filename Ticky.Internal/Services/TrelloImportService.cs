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

        var board = new Board
        {
            Name = importDto.Name,
            Description = importDto.Description,
            ProjectId = projectId,
            Code = importModel.Code
        };

        AddLabelIfPresent(
            board,
            importDto.LabelNames.Purple,
            Color.FromArgb(53, 44, 99),
            Color.FromArgb(159, 143, 239)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.PurpleDark,
            Color.White,
            Color.FromArgb(110, 93, 198)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.PurpleLight,
            Color.FromArgb(53, 44, 99),
            Color.FromArgb(223, 216, 253)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.Red,
            Color.FromArgb(93, 31, 26),
            Color.FromArgb(248, 113, 104)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.RedDark,
            Color.White,
            Color.FromArgb(201, 55, 44)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.RedLight,
            Color.FromArgb(93, 31, 26),
            Color.FromArgb(255, 213, 210)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.Orange,
            Color.FromArgb(112, 46, 0),
            Color.FromArgb(254, 163, 98)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.OrangeDark,
            Color.White,
            Color.FromArgb(194, 81, 0)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.OrangeLight,
            Color.FromArgb(112, 46, 0),
            Color.FromArgb(254, 222, 200)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.Yellow,
            Color.FromArgb(83, 63, 4),
            Color.FromArgb(245, 205, 71)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.YellowDark,
            Color.White,
            Color.FromArgb(148, 111, 0)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.YellowLight,
            Color.FromArgb(83, 63, 4),
            Color.FromArgb(248, 230, 160)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.Green,
            Color.FromArgb(22, 75, 53),
            Color.FromArgb(75, 206, 151)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.GreenDark,
            Color.White,
            Color.FromArgb(31, 132, 90)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.GreenLight,
            Color.FromArgb(22, 75, 53),
            Color.FromArgb(186, 243, 219)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.Blue,
            Color.FromArgb(9, 50, 108),
            Color.FromArgb(87, 157, 255)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.BlueDark,
            Color.White,
            Color.FromArgb(12, 102, 228)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.BlueLight,
            Color.FromArgb(9, 50, 108),
            Color.FromArgb(204, 224, 255)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.Sky,
            Color.FromArgb(22, 69, 85),
            Color.FromArgb(108, 195, 224)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.SkyDark,
            Color.White,
            Color.FromArgb(34, 125, 155)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.SkyLight,
            Color.FromArgb(22, 69, 85),
            Color.FromArgb(198, 237, 251)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.Lime,
            Color.FromArgb(55, 71, 31),
            Color.FromArgb(148, 199, 72)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.LimeDark,
            Color.White,
            Color.FromArgb(91, 127, 36)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.LimeLight,
            Color.FromArgb(55, 71, 31),
            Color.FromArgb(211, 241, 167)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.Pink,
            Color.FromArgb(80, 37, 63),
            Color.FromArgb(231, 116, 187)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.PinkDark,
            Color.White,
            Color.FromArgb(174, 71, 135)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.PinkLight,
            Color.FromArgb(80, 37, 63),
            Color.FromArgb(253, 208, 236)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.Black,
            Color.FromArgb(9, 30, 66),
            Color.FromArgb(133, 144, 162)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.BlackDark,
            Color.White,
            Color.FromArgb(98, 111, 134)
        );
        AddLabelIfPresent(
            board,
            importDto.LabelNames.BlackLight,
            Color.FromArgb(9, 30, 66),
            Color.FromArgb(220, 223, 228)
        );

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
                    CreatedById = currentUserId,
                    Number = cardNumber,
                    ColumnId = column.Id,
                };

                if (card.DueReminder.HasValue && card.Due.HasValue)
                    newCard.Reminders.Add(
                        new Reminder
                        {
                            At = card.Due.Value.AddDays(card.DueReminder.Value),
                            CardId = newCard.Id
                        }
                    );

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

                column.Cards.Add(newCard);
                cardNumber++;
                columnCardIndex++;
            }

            board.Columns.Add(column);
            columnIndex++;
        }

        db.Boards.Add(board);

        await db.SaveChangesAsync();
    }

    private void AddLabelIfPresent(
        Board board,
        string labelName,
        Color textColor,
        Color backgroundColor
    )
    {
        if (!string.IsNullOrWhiteSpace(labelName))
        {
            board.Labels.Add(
                new Label
                {
                    Name = labelName,
                    TextColor = textColor,
                    BackgroundColor = backgroundColor,
                    BoardId = board.Id
                }
            );
        }
    }
}
