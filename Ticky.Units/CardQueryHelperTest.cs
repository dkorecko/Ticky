using Microsoft.EntityFrameworkCore;
using Ticky.Base.Models;

namespace Ticky.Units;

/// <summary>The card lists shared by the web UI and the API: a board's filter, search and Your work.</summary>
public class CardQueryHelperTest
{
    private TestDatabase _db = null!;

    [SetUp]
    public async Task Setup()
    {
        _db = await TestDatabase.CreateAsync();

        using var db = _db.CreateDbContext();
        var cards = await db.Cards.Include(x => x.Assignees).OrderBy(x => x.Number).ToListAsync();
        var member = await db.Users.SingleAsync(x => x.Id == _db.BoardMemberId);

        cards[0].Assignees.Add(member);
        cards[0].Description = "Needs a review";
        cards[1].Assignees.Add(member);
        cards[1].SnoozedUntil = DateTime.Now.AddDays(1);
        cards[2].Name = "TST-1 lookalike";
        await db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task<string[]> NamesAsync(Func<IQueryable<Card>, IQueryable<Card>> query)
    {
        using var db = _db.CreateDbContext();
        return await query(db.Cards).Select(x => x.Name).ToArrayAsync();
    }

    [Test]
    public async Task Matching_TextFindsNameKeyOrDescription()
    {
        Assert.That(await NamesAsync(x => x.Matching(new FilterCardsModel { Text = "review" })), Is.EqualTo(new[] { "Card 1" }));
        Assert.That(await NamesAsync(x => x.Matching(new FilterCardsModel { Text = "TST-2" })), Is.EqualTo(new[] { "Card 2" }));
    }

    [Test]
    public async Task Matching_UnassignedAndAssignees()
    {
        Assert.That(await NamesAsync(x => x.Matching(new FilterCardsModel { IncludeUnassigned = true })), Is.EqualTo(new[] { "TST-1 lookalike" }));
        Assert.That(
            await NamesAsync(x => x.Matching(new FilterCardsModel { AssignedUserIds = [_db.BoardMemberId] }).OrderBy(c => c.Number)),
            Is.EqualTo(new[] { "Card 1", "Card 2" })
        );
    }

    [Test]
    public async Task Search_ExactKeyComesFirst()
    {
        var names = await NamesAsync(x => x.Search("TST-1"));

        Assert.That(names, Is.EqualTo(new[] { "Card 1", "TST-1 lookalike" }));
    }

    [Test]
    public async Task WorkOf_OpenAssignedAndNotSnoozed()
    {
        var doneId = await _db.AddColumnAsync("Done", finished: true);

        using (var db = _db.CreateDbContext())
        {
            var card = new Card { Name = "Finished", Number = 9, Index = 0, ColumnId = doneId, CreatedById = _db.BoardMemberId };
            card.Assignees.Add(await db.Users.SingleAsync(x => x.Id == _db.BoardMemberId));
            db.Cards.Add(card);
            await db.SaveChangesAsync();
        }

        Assert.That(await NamesAsync(x => x.WorkOf(_db.BoardMemberId)), Is.EqualTo(new[] { "Card 1" }));
    }
}
