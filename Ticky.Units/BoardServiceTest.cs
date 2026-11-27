using Microsoft.EntityFrameworkCore;
using Ticky.Base.DTOs;

namespace Ticky.Units;

/// <summary>Who may create, change, clone and delete boards and projects.</summary>
public class BoardServiceTest
{
    private TestDatabase _db = null!;
    private BoardService _boards = null!;
    private ProjectService _projects = null!;

    [SetUp]
    public async Task Setup()
    {
        _db = await TestDatabase.CreateAsync();
        _boards = new BoardService(_db.Factory);
        _projects = new ProjectService(_db.Factory);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    #region Boards

    [Test]
    public async Task Create_ProjectAdmin_AddsBoardWithDefaultLabels()
    {
        var result = await _boards.CreateAsync(_db.ProjectId, _db.ProjectAdminId, new(" Roadmap ", "Plans", "RM"));

        using var db = _db.CreateDbContext();
        var board = await db.Boards.Include(x => x.Labels).SingleAsync(x => x.Id == result.Value!.Id);

        Assert.That(board.Name, Is.EqualTo("Roadmap"));
        Assert.That(board.Labels.Select(x => x.Name), Is.EquivalentTo(new[] { "Bug", "Feature", "Enhancement" }));
    }

    [TestCase("project member", OperationStatus.Forbidden)]
    [TestCase("board admin", OperationStatus.NotFound)]
    [TestCase("outsider", OperationStatus.NotFound)]
    public async Task Create_NotAProjectAdmin_Refused(string who, OperationStatus status)
    {
        var userId = who switch
        {
            "project member" => _db.ProjectMemberId,
            "board admin" => _db.BoardAdminId,
            _ => _db.OutsiderId,
        };

        Assert.That((await _boards.CreateAsync(_db.ProjectId, userId, new("New", "", "NEW"))).Status, Is.EqualTo(status));
    }

    [TestCase("")]
    [TestCase("toolong")]
    [TestCase("abc")]
    [TestCase("A1")]
    public async Task Create_BadCode_Invalid(string code)
    {
        var result = await _boards.CreateAsync(_db.ProjectId, _db.ProjectAdminId, new("New", "", code));

        Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid));
        Assert.That(result.Field, Is.EqualTo(nameof(Board.Code)));
    }

    [TestCase("board admin", true)]
    [TestCase("project admin", true)]
    [TestCase("board member", false)]
    [TestCase("project member", false)]
    public async Task Update_OnlyAdmins(string who, bool allowed)
    {
        var userId = who switch
        {
            "board admin" => _db.BoardAdminId,
            "project admin" => _db.ProjectAdminId,
            "board member" => _db.BoardMemberId,
            _ => _db.ProjectMemberId,
        };

        var result = await _boards.UpdateAsync(_db.BoardId, userId, new() { Name = "Renamed", DisableSortingAnimations = true });

        using var db = _db.CreateDbContext();
        var board = await db.Boards.SingleAsync(x => x.Id == _db.BoardId);

        Assert.That(result.Status, Is.EqualTo(allowed ? OperationStatus.Succeeded : OperationStatus.Forbidden));
        Assert.That(board.Name, Is.EqualTo(allowed ? "Renamed" : "Board"));
        Assert.That(board.DisableSortingAnimations, Is.EqualTo(allowed));
    }

    [Test]
    public async Task Update_RoleTakenAway_ForbiddenAtOnce()
    {
        using (var db = _db.CreateDbContext())
        {
            (await db.BoardMemberships.SingleAsync(x => x.UserId == _db.BoardAdminId)).IsAdmin = false;
            await db.SaveChangesAsync();
        }

        Assert.That((await _boards.UpdateAsync(_db.BoardId, _db.BoardAdminId, new() { Name = "Late" })).Status, Is.EqualTo(OperationStatus.Forbidden));
    }

    [Test]
    public async Task Update_ProjectAdminWithBoardMemberRole_Forbidden()
    {
        // A board membership decides over the project role, so a project admin added to the board as a plain member
        // can open it but not manage it.
        using (var db = _db.CreateDbContext())
        {
            db.BoardMemberships.Add(new BoardMembership { UserId = _db.ProjectAdminId, BoardId = _db.BoardId, IsAdmin = false, AddedAt = DateTime.Now });
            await db.SaveChangesAsync();
        }

        var result = await _boards.UpdateAsync(_db.BoardId, _db.ProjectAdminId, new() { Name = "Renamed" });

        using var check = _db.CreateDbContext();
        Assert.That(result.Status, Is.EqualTo(OperationStatus.Forbidden));
        Assert.That((await check.Boards.SingleAsync(x => x.Id == _db.BoardId)).Name, Is.EqualTo("Board"));
    }

    [Test]
    public async Task Delete_BoardMember_ForbiddenAndAdmin_Deletes()
    {
        Assert.That((await _boards.DeleteAsync(_db.BoardId, _db.BoardMemberId)).Status, Is.EqualTo(OperationStatus.Forbidden));
        Assert.That((await _boards.DeleteAsync(_db.BoardId, _db.BoardAdminId)).Succeeded, Is.True);

        using var db = _db.CreateDbContext();
        Assert.That(await db.Boards.AnyAsync(), Is.False);
        Assert.That(await db.Cards.AnyAsync(), Is.False);
    }

    [Test]
    public async Task SetFavorite_TwiceThenOff_KeepsOneThenNone()
    {
        await _boards.SetFavoriteAsync(_db.BoardId, _db.BoardMemberId, true);
        await _boards.SetFavoriteAsync(_db.BoardId, _db.BoardMemberId, true);

        using (var db = _db.CreateDbContext())
            Assert.That(await db.Favorites.CountAsync(), Is.EqualTo(1));

        await _boards.SetFavoriteAsync(_db.BoardId, _db.BoardMemberId, false);

        using var check = _db.CreateDbContext();
        Assert.That(await check.Favorites.CountAsync(), Is.Zero);
        Assert.That((await _boards.SetFavoriteAsync(_db.BoardId, _db.OutsiderId, true)).Status, Is.EqualTo(OperationStatus.NotFound));
    }

    [Test]
    public async Task Clone_IntoProjectYouDontAdminister_Invalid()
    {
        // The board admin administers the board but not its project.
        var result = await _boards.CloneAsync(_db.BoardId, _db.BoardAdminId, _db.ProjectId, "CPY");

        Assert.That(result.Status, Is.EqualTo(OperationStatus.Invalid));
        Assert.That(result.Field, Is.EqualTo("TargetProjectId"));
    }

    [Test]
    public async Task Clone_CopiesCardsAndPointsRepeatsAtTheCopiedColumns()
    {
        using (var db = _db.CreateDbContext())
        {
            var card = await db.Cards.SingleAsync(x => x.Id == _db.CardIds[0]);
            card.RepeatInfo = new() { Type = RepeatType.Daily, Time = new TimeOnly(9, 0), CardPlacement = CardPlacement.Top, TargetColumnId = _db.ColumnId };
            db.Subtasks.Add(new Subtask { CardId = card.Id, Text = "Step", Index = 0 });

            // The same person and label on two cards: the copies must share them without clashing.
            var label = new Label { Name = "Bug", BoardId = _db.BoardId, TextColor = System.Drawing.Color.White, BackgroundColor = System.Drawing.Color.Red };
            var member = await db.Users.SingleAsync(x => x.Id == _db.BoardMemberId);

            foreach (var other in await db.Cards.Include(x => x.Labels).Include(x => x.Assignees).Where(x => _db.CardIds.Take(2).Contains(x.Id)).ToListAsync())
            {
                other.Labels.Add(label);
                other.Assignees.Add(member);
            }

            await db.SaveChangesAsync();
        }

        var result = await _boards.CloneAsync(_db.BoardId, _db.ProjectAdminId, _db.ProjectId, "CPY");

        using var check = _db.CreateDbContext();
        var copy = await check.Boards
            .Include(x => x.Columns).ThenInclude(x => x.Cards).ThenInclude(x => x.Subtasks)
            .Include(x => x.Columns).ThenInclude(x => x.Cards).ThenInclude(x => x.Labels)
            .Include(x => x.Columns).ThenInclude(x => x.Cards).ThenInclude(x => x.Assignees)
            .Include(x => x.Labels)
            .Include(x => x.Memberships)
            .SingleAsync(x => x.Id == result.Value!.Id);
        var copiedColumn = copy.Columns.Single();
        var repeating = copiedColumn.Cards.Single(x => x.RepeatInfo != null);

        Assert.Multiple(() =>
        {
            Assert.That(copy.Name, Is.EqualTo("Copy of Board"));
            Assert.That(copy.Code, Is.EqualTo("CPY"));
            Assert.That(copiedColumn.Cards, Has.Count.EqualTo(3));
            Assert.That(repeating.Subtasks.Single().Text, Is.EqualTo("Step"));
            Assert.That(repeating.RepeatInfo!.TargetColumnId, Is.EqualTo(copiedColumn.Id));
            Assert.That(copy.Memberships.Select(x => x.UserId), Is.EquivalentTo(new[] { _db.BoardAdminId, _db.BoardMemberId }));
            Assert.That(copiedColumn.Cards.Count(x => x.Assignees.Any(a => a.Id == _db.BoardMemberId)), Is.EqualTo(2));
            Assert.That(copiedColumn.Cards.SelectMany(x => x.Labels).Select(x => x.Id).Distinct(), Is.EqualTo(new[] { copy.Labels.Single().Id }));
        });
    }

    #endregion

    #region Projects

    [Test]
    public async Task CreateProject_MakesTheCreatorItsAdmin()
    {
        var result = await _projects.CreateAsync(_db.OutsiderId, "Mine");

        using var db = _db.CreateDbContext();
        var membership = await db.ProjectMemberships.SingleAsync(x => x.ProjectId == result.Value!.Id);

        Assert.That(membership.UserId, Is.EqualTo(_db.OutsiderId));
        Assert.That(membership.IsAdmin, Is.True);
    }

    [Test]
    public async Task RenameAndDeleteProject_OnlyItsAdmins()
    {
        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await _projects.RenameAsync(_db.ProjectId, _db.ProjectMemberId, "No")).Status, Is.EqualTo(OperationStatus.Forbidden));
            Assert.That((await _projects.RenameAsync(_db.ProjectId, _db.BoardAdminId, "No")).Status, Is.EqualTo(OperationStatus.NotFound));
            Assert.That((await _projects.DeleteAsync(_db.ProjectId, _db.ProjectMemberId)).Status, Is.EqualTo(OperationStatus.Forbidden));
            Assert.That((await _projects.RenameAsync(_db.ProjectId, _db.ProjectAdminId, " Yes ")).Value!.Name, Is.EqualTo("Yes"));
            Assert.That((await _projects.DeleteAsync(_db.ProjectId, _db.ProjectAdminId)).Succeeded, Is.True);
        });

        using var db = _db.CreateDbContext();
        Assert.That(await db.Projects.AnyAsync(), Is.False);
    }

    #endregion
}
