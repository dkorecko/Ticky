using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticky.Internal.Data;
using Ticky.Internal.Services.Hosted;

namespace Ticky.Units;

public class RepeatHostedServiceTest
{
    /// <summary>Lets a test run one pass of the job instead of waiting for its timer.</summary>
    private class RunnableRepeatHostedService(IServiceScopeFactory serviceScopeFactory)
        : RepeatHostedService(serviceScopeFactory)
    {
        public Task RunOnceAsync() => OnRun();
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _provider = null!;

    private int _userId;
    private int _cardId;
    private int _firstColumnId;
    private int _secondColumnId;
    private int _otherBoardColumnId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<DataContext>(options => options.UseSqlite(_connection));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContext());
        services.AddScoped<CardNumberingService>();
        _provider = services.BuildServiceProvider();

        using var db = _provider.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContext();
        db.Database.EnsureCreated();

        var user = new User { DisplayName = "Owner", UserName = "owner@test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var project = new Project { Name = "Project" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var board = new Board { Name = "Own board", Description = "", Code = "OWN", ProjectId = project.Id };
        var otherBoard = new Board { Name = "Other board", Description = "", Code = "OTH", ProjectId = project.Id };
        db.Boards.AddRange(board, otherBoard);
        await db.SaveChangesAsync();

        var first = new Column { Name = "To do", BoardId = board.Id, Index = 0 };
        var second = new Column { Name = "Doing", BoardId = board.Id, Index = 1 };
        var other = new Column { Name = "Other board's column", BoardId = otherBoard.Id, Index = 0 };
        db.Columns.AddRange(first, second, other);
        await db.SaveChangesAsync();

        var card = new Card
        {
            Name = "Water the plants",
            Number = 1,
            Index = 0,
            ColumnId = first.Id,
            CreatedById = user.Id,
            RepeatInfo = new RepeatInfo
            {
                Type = RepeatType.Daily,
                Time = new TimeOnly(0, 0),
                LastRepeat = DateTime.Now.AddDays(-3),
                CardPlacement = CardPlacement.Top,
            },
        };
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        (_userId, _cardId) = (user.Id, card.Id);
        (_firstColumnId, _secondColumnId, _otherBoardColumnId) = (first.Id, second.Id, other.Id);
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private async Task SetTargetAsync(int? targetColumnId)
    {
        using var db = _provider.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContext();
        (await db.Cards.SingleAsync(x => x.Id == _cardId)).RepeatInfo!.TargetColumnId = targetColumnId;
        await db.SaveChangesAsync();
    }

    /// <summary>Runs the job once and returns the ids of the columns that got a copy of the card.</summary>
    private async Task<List<int>> RunJobAndGetCopyColumnsAsync()
    {
        var job = new RunnableRepeatHostedService(_provider.GetRequiredService<IServiceScopeFactory>());

        try
        {
            await job.RunOnceAsync();
        }
        finally
        {
            job.Dispose();
        }

        using var db = _provider.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContext();
        return await db.Cards.Where(x => x.Id != _cardId).Select(x => x.ColumnId).ToListAsync();
    }

    [Test]
    public async Task Job_TargetOnTheCardsBoard_CreatesTheCopyThere()
    {
        await SetTargetAsync(_secondColumnId);

        Assert.That(await RunJobAndGetCopyColumnsAsync(), Is.EqualTo(new[] { _secondColumnId }));
    }

    [Test]
    public async Task Job_StoredTargetOnAnotherBoard_FallsBackToTheCardsBoard()
    {
        await SetTargetAsync(_otherBoardColumnId);

        var copyColumns = await RunJobAndGetCopyColumnsAsync();

        Assert.That(copyColumns, Is.EqualTo(new[] { _firstColumnId }));
        using var db = _provider.GetRequiredService<IDbContextFactory<DataContext>>().CreateDbContext();
        Assert.That(await db.Cards.CountAsync(x => x.ColumnId == _otherBoardColumnId), Is.Zero);
        Assert.That((await db.Cards.SingleAsync(x => x.ColumnId == _firstColumnId && x.Id != _cardId)).CreatedById, Is.EqualTo(_userId));
    }

    [TestCase(null)]
    [TestCase(0)]
    [TestCase(999_999)]
    public async Task Job_NoUsableTarget_UsesTheFirstColumnOfTheCardsBoard(int? targetColumnId)
    {
        await SetTargetAsync(targetColumnId);

        Assert.That(await RunJobAndGetCopyColumnsAsync(), Is.EqualTo(new[] { _firstColumnId }));
    }

    private static Card NewCard(string name, int index = 0) =>
        new() { Name = name, Index = index, Number = 0, ColumnId = 1, CreatedById = 1 };

    private static string[] Order(List<Card> cards) => cards.OrderBy(x => x.Index).Select(x => x.Name).ToArray();

    [Test]
    public void PlaceNewCard_Top_GoesFirstAndShiftsOthers()
    {
        var cards = new List<Card> { NewCard("A", 0), NewCard("B", 1) };

        RepeatHostedService.PlaceNewCard(cards, NewCard("New"), CardPlacement.Top);

        Assert.Multiple(() =>
        {
            Assert.That(Order(cards), Is.EqualTo(new[] { "New", "A", "B" }));
            Assert.That(cards.Select(x => x.Index).Order(), Is.EqualTo(new[] { 0, 1, 2 }));
        });
    }

    [Test]
    public void PlaceNewCard_Bottom_GoesLast()
    {
        var cards = new List<Card> { NewCard("A", 0), NewCard("B", 1) };

        RepeatHostedService.PlaceNewCard(cards, NewCard("New"), CardPlacement.Bottom);

        Assert.Multiple(() =>
        {
            Assert.That(Order(cards), Is.EqualTo(new[] { "A", "B", "New" }));
            Assert.That(cards.Select(x => x.Index).Order(), Is.EqualTo(new[] { 0, 1, 2 }));
        });
    }

    [TestCase(CardPlacement.Top)]
    [TestCase(CardPlacement.Bottom)]
    public void PlaceNewCard_EmptyColumn_GoesFirst(CardPlacement placement)
    {
        var cards = new List<Card>();

        RepeatHostedService.PlaceNewCard(cards, NewCard("New", index: 5), placement);

        Assert.That(cards.Single().Index, Is.Zero);
    }
}
