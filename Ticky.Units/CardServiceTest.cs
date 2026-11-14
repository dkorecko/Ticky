using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticky.Internal.Data;

namespace Ticky.Units;

/// <summary>Runs CardService against an in-memory SQLite database.</summary>
public class CardServiceTest
{
    private SqliteConnection _connection = null!;
    private TestDbContextFactory _factory = null!;
    private CardService _service = null!;

    private int _boardMemberId;
    private int _projectMemberId;
    private int _outsiderId;
    private int _cardId;
    private int _otherCardId;
    private int _thirdCardId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _factory = new TestDbContextFactory(
            new DbContextOptionsBuilder<DataContext>().UseSqlite(_connection).Options
        );
        _service = new CardService(_factory, new CardNumberingService(_factory));

        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();

        var boardMember = new User { DisplayName = "Board member", UserName = "board@test" };
        var projectMember = new User { DisplayName = "Project member", UserName = "project@test" };
        var outsider = new User { DisplayName = "Outsider", UserName = "outsider@test" };
        db.Users.AddRange(boardMember, projectMember, outsider);
        await db.SaveChangesAsync();

        var project = new Project { Name = "Project" };
        project.Memberships.Add(new ProjectMembership { UserId = projectMember.Id, ProjectId = 0, IsAdmin = false, AddedAt = DateTime.Now });
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var board = new Board { Name = "Board", Description = "", Code = "TST", ProjectId = project.Id };
        board.Memberships.Add(new BoardMembership { UserId = boardMember.Id, BoardId = 0, IsAdmin = false, AddedAt = DateTime.Now });
        db.Boards.Add(board);
        await db.SaveChangesAsync();

        var column = new Column { Name = "To do", BoardId = board.Id, Index = 0 };
        db.Columns.Add(column);
        await db.SaveChangesAsync();

        Card NewCard(int number) => new() { Name = $"Card {number}", Number = number, Index = number, ColumnId = column.Id, CreatedById = boardMember.Id };
        var card = NewCard(1);
        var otherCard = NewCard(2);
        var thirdCard = NewCard(3);
        db.Cards.AddRange(card, otherCard, thirdCard);
        await db.SaveChangesAsync();

        (_boardMemberId, _projectMemberId, _outsiderId) = (boardMember.Id, projectMember.Id, outsider.Id);
        (_cardId, _otherCardId, _thirdCardId) = (card.Id, otherCard.Id, thirdCard.Id);
    }

    [TearDown]
    public void TearDown() => _connection.Dispose();

    [Test]
    public async Task CanAccessCard_BoardMember_True() =>
        Assert.That(await _service.CanAccessCardAsync(_cardId, _boardMemberId), Is.True);

    [Test]
    public async Task CanAccessCard_ProjectMember_True() =>
        Assert.That(await _service.CanAccessCardAsync(_cardId, _projectMemberId), Is.True);

    [Test]
    public async Task CanAccessCard_Outsider_False() =>
        Assert.That(await _service.CanAccessCardAsync(_cardId, _outsiderId), Is.False);

    [Test]
    public async Task CanAccessCard_MissingCard_False() =>
        Assert.That(await _service.CanAccessCardAsync(999_999, _boardMemberId), Is.False);

    [Test]
    public async Task RemoveLink_RemovesOnlyItsOwnReverseLink()
    {
        // Link the third card first: the old code deleted the first link pointing back at the card,
        // which would then be the third card's reverse link instead of the other card's.
        Assert.That(await _service.LinkCardsAsync(_cardId, _thirdCardId, _boardMemberId, "relates to"), Is.Null);
        Assert.That(await _service.LinkCardsAsync(_cardId, _otherCardId, _boardMemberId, "is blocked by"), Is.Null);

        using (var db = _factory.CreateDbContext())
        {
            var link = await db.CardLinks.SingleAsync(x => x.CardOneId == _cardId && x.CardTwoId == _otherCardId);
            await _service.RemoveLinkAsync(link.Id, _boardMemberId);
        }

        using var check = _factory.CreateDbContext();
        var remaining = await check.CardLinks.Select(x => new { x.CardOneId, x.CardTwoId, x.Category }).ToListAsync();

        Assert.That(remaining, Has.Count.EqualTo(2));
        Assert.That(remaining, Has.One.Matches<dynamic>(x => x.CardOneId == _cardId && x.CardTwoId == _thirdCardId));
        Assert.That(remaining, Has.One.Matches<dynamic>(x => x.CardOneId == _thirdCardId && x.CardTwoId == _cardId && x.Category == "relates to"));
    }

    [Test]
    public async Task Activity_StoresRawValueAndEscapesWhenShown()
    {
        await _service.AddSubtaskAsync(_cardId, _boardMemberId, "<img src=x onerror=alert(1)>");

        using var db = _factory.CreateDbContext();
        var activity = (SubtaskAddedActivity)await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.Multiple(() =>
        {
            Assert.That(activity.ActivityType, Is.EqualTo(ActivityType.SubtaskAdded));
            Assert.That(activity.LegacyText, Is.Null);
            Assert.That(activity.SubtaskText, Is.EqualTo("<img src=x onerror=alert(1)>"));
            Assert.That(activity.ToHtml(), Is.EqualTo("<b>added</b> subtask <b>&lt;img src=x onerror=alert(1)&gt;</b>"));
        });
    }

    [Test]
    public async Task Rename_KeepsOldAndNewTitle()
    {
        await _service.RenameAsync(_cardId, _boardMemberId, "Renamed");

        using var db = _factory.CreateDbContext();
        var activity = (TitleChangedActivity)await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.That(activity.ToHtml(), Is.EqualTo("<b>changed</b> the title from <b>Card 1</b> to <b>Renamed</b>"));
    }

    [Test]
    public async Task Join_IsShownAsJoining()
    {
        await _service.JoinAsync(_cardId, _boardMemberId);

        using var db = _factory.CreateDbContext();
        var activity = (AssigneeAddedActivity)await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.That(activity.AssigneeName, Is.EqualTo("Board member"));
        Assert.That(activity.ToHtml(), Is.EqualTo("<b>joined</b> the card"));
    }

    private async Task<int> AddColumnAsync(string name, bool finished, int maxCards = 0)
    {
        using var db = _factory.CreateDbContext();
        var boardId = (await db.Cards.Include(x => x.Column).SingleAsync(x => x.Id == _cardId)).Column.BoardId;
        var column = new Column { Name = name, BoardId = boardId, Index = 1, Finished = finished, MaxCards = maxCards };
        db.Columns.Add(column);
        await db.SaveChangesAsync();
        return column.Id;
    }

    [Test]
    public async Task MoveToColumn_RecordsTypedMoveWithColumnSnapshot()
    {
        var doneId = await AddColumnAsync("Done & dusted", finished: true);

        Assert.That(await _service.MoveToColumnAsync(_cardId, _boardMemberId, doneId, null), Is.True);

        using var db = _factory.CreateDbContext();
        var activity = (CardMovedActivity)await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.Multiple(() =>
        {
            Assert.That(activity.ActivityType, Is.EqualTo(ActivityType.CardMoved));
            Assert.That(activity.FromColumnName, Is.EqualTo("To do"));
            Assert.That(activity.ToColumnId, Is.EqualTo(doneId));
            Assert.That(activity.ToColumnName, Is.EqualTo("Done & dusted"));
            Assert.That(activity.ToColumnFinished, Is.True);
            Assert.That(activity.ToHtml(), Is.EqualTo("<b>moved</b> the card from <b>To do</b> to <b>Done &amp; dusted</b>"));
        });
    }

    [Test]
    public async Task MoveToColumn_PlacesBeforeTargetCardAndFixesIndices()
    {
        var doneId = await AddColumnAsync("Done", finished: true);

        await _service.MoveToColumnAsync(_cardId, _boardMemberId, doneId, null);
        await _service.MoveToColumnAsync(_otherCardId, _boardMemberId, doneId, _cardId);

        using var db = _factory.CreateDbContext();
        var cards = await db.Cards.ToDictionaryAsync(x => x.Id);

        Assert.Multiple(() =>
        {
            Assert.That(cards[_otherCardId].ColumnId, Is.EqualTo(doneId));
            Assert.That(cards[_otherCardId].Index, Is.EqualTo(0));
            Assert.That(cards[_cardId].Index, Is.EqualTo(1));
            Assert.That(cards[_thirdCardId].Index, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task MoveToColumn_FullColumn_ReturnsFalseAndChangesNothing()
    {
        var doneId = await AddColumnAsync("Done", finished: true, maxCards: 1);
        await _service.MoveToColumnAsync(_cardId, _boardMemberId, doneId, null);

        Assert.That(await _service.MoveToColumnAsync(_otherCardId, _boardMemberId, doneId, null), Is.False);

        using var db = _factory.CreateDbContext();
        Assert.That((await db.Cards.SingleAsync(x => x.Id == _otherCardId)).ColumnId, Is.Not.EqualTo(doneId));
        Assert.That(await db.Activities.CountAsync(x => x.CardId == _otherCardId), Is.Zero);
    }

    private class TestDbContextFactory(DbContextOptions<DataContext> options) : IDbContextFactory<DataContext>
    {
        public DataContext CreateDbContext() => new(options);
    }
}
