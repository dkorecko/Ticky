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
        _service = new CardService(_factory);

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
    public async Task ActivityText_EscapesUserInput()
    {
        await _service.AddSubtaskAsync(_cardId, _boardMemberId, "<img src=x onerror=alert(1)>");

        using var db = _factory.CreateDbContext();
        var activity = await db.Activities.SingleAsync(x => x.CardId == _cardId);

        Assert.That(activity.Text, Does.Not.Contain("<img"));
        Assert.That(activity.Text, Does.Contain("&lt;img src=x onerror=alert(1)&gt;"));
    }

    private class TestDbContextFactory(DbContextOptions<DataContext> options) : IDbContextFactory<DataContext>
    {
        public DataContext CreateDbContext() => new(options);
    }
}
