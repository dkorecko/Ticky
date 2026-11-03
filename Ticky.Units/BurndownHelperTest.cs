using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticky.Internal.Data;
using Ticky.Internal.Helpers;
using static Ticky.Internal.Helpers.BurndownHelper;

namespace Ticky.Units;

public class BurndownHelperTest
{
    private static DateTime Day(int day, int hour = 12) => new(2025, 1, day, hour, 0, 0);

    private static CardTimeline Card(DateTime created, bool doneNow, DateTime? lastActivity = null, (DateTime, bool)[]? moves = null) =>
        new(created, doneNow, moves ?? [], lastActivity);

    private static int[] Open(IReadOnlyCollection<CardTimeline> cards, int fromDay, int toDay) =>
        GetOpenPerDay(cards, Day(fromDay), Day(toDay)).Select(x => x.Open).ToArray();

    [Test]
    public void GetOpenPerDay_NoCards_AllZero()
    {
        Assert.That(Open([], 1, 3), Is.EqualTo(new[] { 0, 0, 0 }));
    }

    [Test]
    public void GetOpenPerDay_ReturnsOnePointPerDay()
    {
        var result = GetOpenPerDay([], Day(1, 18), Day(5, 9));

        Assert.That(result.Select(x => x.Date), Is.EqualTo(Enumerable.Range(1, 5).Select(d => new DateTime(2025, 1, d))));
    }

    [Test]
    public void GetOpenPerDay_CardCountsFromCreationUntilMovedToFinished()
    {
        var card = Card(Day(2), doneNow: true, moves: [(Day(4), true)]);

        Assert.That(Open([card], 1, 5), Is.EqualTo(new[] { 0, 1, 1, 0, 0 }));
    }

    [Test]
    public void GetOpenPerDay_ReopenedCardCountsAgain()
    {
        var card = Card(Day(1), doneNow: false, moves: [(Day(2), true), (Day(4), false)]);

        Assert.That(Open([card], 1, 5), Is.EqualTo(new[] { 1, 0, 0, 1, 1 }));
    }

    [Test]
    public void GetOpenPerDay_UnrecordedMove_AssumedAtLastActivity()
    {
        // Dragged to a finished column before moves were recorded: only the current column tells us it's done
        var card = Card(Day(1), doneNow: true, lastActivity: Day(3));

        Assert.That(Open([card], 1, 4), Is.EqualTo(new[] { 1, 1, 0, 0 }));
    }

    [Test]
    public void GetOpenPerDay_UnrecordedMoveWithoutActivity_DoneFromCreation()
    {
        var card = Card(Day(2), doneNow: true);

        Assert.That(Open([card], 1, 3), Is.EqualTo(new[] { 0, 0, 0 }));
    }

    [Test]
    public void GetOpenPerDay_UnrecordedReopen_NeverBeforeLastRecordedMove()
    {
        var card = Card(Day(1), doneNow: false, lastActivity: Day(2), moves: [(Day(3), true)]);

        // The reopen is unrecorded, so it can only be placed at the last recorded move
        Assert.That(Open([card], 1, 4), Is.EqualTo(new[] { 1, 1, 1, 1 }));
    }

    [Test]
    public void GetOpenPerDay_LastDayMatchesCurrentState()
    {
        var cards = new[]
        {
            Card(Day(1), doneNow: true, lastActivity: Day(1)),
            Card(Day(1), doneNow: false, moves: [(Day(2), true)]),
            Card(Day(2), doneNow: false),
        };

        Assert.That(Open(cards, 1, 3)[^1], Is.EqualTo(2));
    }

    [Test]
    public void ToSvgPoints_NoValues_ReturnsEmpty()
    {
        Assert.That(ToSvgPoints([], 100, 40, 5), Is.Empty);
    }

    [Test]
    public void ToSvgPoints_SpreadsAcrossWidthWithZeroAtBottom()
    {
        Assert.That(ToSvgPoints([4, 2, 0], 100, 40, 4), Is.EqualTo("0,0 50,20 100,40"));
    }

    [Test]
    [SetCulture("nl-BE")]
    public void ToSvgPoints_CommaDecimalCulture_UsesDots()
    {
        Assert.That(ToSvgPoints([1, 2], 100, 10, 3), Is.EqualTo("0,6.67 100,3.33"));
    }

    [Test]
    public async Task LoadTimelines_ReadsMovesAndLastActivityOfThisBoardOnly()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var db = new DataContext(new DbContextOptionsBuilder<DataContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();

        var user = new User { DisplayName = "Ann", UserName = "ann@test" };
        var project = new Project { Name = "Project" };
        db.AddRange(user, project);
        await db.SaveChangesAsync();

        var board = new Board { Name = "Board", Description = "", Code = "TST", ProjectId = project.Id };
        var otherBoard = new Board { Name = "Other", Description = "", Code = "OTH", ProjectId = project.Id };
        db.AddRange(board, otherBoard);
        await db.SaveChangesAsync();

        var todo = new Column { Name = "To do", BoardId = board.Id, Index = 0 };
        var done = new Column { Name = "Done", BoardId = board.Id, Index = 1, Finished = true };
        var otherColumn = new Column { Name = "To do", BoardId = otherBoard.Id, Index = 0 };
        db.AddRange(todo, done, otherColumn);
        await db.SaveChangesAsync();

        Card NewCard(int number, Column column) =>
            new() { Name = $"Card {number}", Number = number, Index = 0, ColumnId = column.Id, CreatedById = user.Id, CreatedAt = Day(number) };
        var movedCard = NewCard(1, done);
        var oldMoveCard = NewCard(2, done);
        var unknownMoveCard = NewCard(3, todo);
        var otherBoardCard = NewCard(4, otherColumn);
        db.AddRange(movedCard, oldMoveCard, unknownMoveCard, otherBoardCard);
        await db.SaveChangesAsync();

        db.AddRange(
            // A move recorded with typed history knows whether its column was finished.
            new CardMovedActivity { CardId = movedCard.Id, UserId = user.Id, ToColumnId = todo.Id, ToColumnFinished = true, CreatedAt = Day(5) },
            // A converted old move only knows the target column, so its current setting is used.
            new CardMovedActivity { CardId = oldMoveCard.Id, UserId = user.Id, ToColumnId = done.Id, ToColumnName = "Done", CreatedAt = Day(6) },
            // A converted old move whose column is gone tells nothing and is skipped.
            new CardMovedActivity { CardId = unknownMoveCard.Id, UserId = user.Id, ToColumnName = "Deleted", CreatedAt = Day(7) },
            new CommentPostedActivity { CardId = unknownMoveCard.Id, UserId = user.Id, CreatedAt = Day(8) },
            new CommentPostedActivity { CardId = otherBoardCard.Id, UserId = user.Id, CreatedAt = Day(9) }
        );
        await db.SaveChangesAsync();

        var timelines = (await LoadTimelinesAsync(db, board.Id)).OrderBy(x => x.CreatedAt).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(timelines.Select(x => x.CreatedAt), Is.EqualTo(new[] { Day(1), Day(2), Day(3) }));
            Assert.That(timelines.Select(x => x.DoneNow), Is.EqualTo(new[] { true, true, false }));
            Assert.That(timelines[0].Moves, Is.EqualTo(new[] { (Day(5), true) }));
            Assert.That(timelines[1].Moves, Is.EqualTo(new[] { (Day(6), true) }));
            Assert.That(timelines[2].Moves, Is.Empty);
            Assert.That(timelines.Select(x => x.LastActivityAt), Is.EqualTo(new DateTime?[] { Day(5), Day(6), Day(8) }));
        });
    }
}
