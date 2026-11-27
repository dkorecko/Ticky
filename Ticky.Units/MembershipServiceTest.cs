using Microsoft.EntityFrameworkCore;
using Ticky.Base.DTOs;

namespace Ticky.Units;

/// <summary>Adding people to boards and projects, changing their role and removing them.</summary>
public class MembershipServiceTest
{
    private TestDatabase _db = null!;
    private MembershipService _service = null!;

    [SetUp]
    public async Task Setup()
    {
        _db = await TestDatabase.CreateAsync();
        _service = new MembershipService(_db.Factory);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task AddBoardMember_ByEmailInAnyCase_AddsMember()
    {
        var result = await _service.AddBoardMemberAsync(_db.BoardId, _db.BoardAdminId, " OUTSIDER@test ");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.UserId, Is.EqualTo(_db.OutsiderId));
        Assert.That(result.Value.IsAdmin, Is.False);
    }

    [Test]
    public async Task AddBoardMember_UnknownEmail_Invalid() =>
        Assert.That((await _service.AddBoardMemberAsync(_db.BoardId, _db.BoardAdminId, "nobody@test")).Field, Is.EqualTo("Email"));

    [TestCase("board@test")]
    [TestCase("board-admin@test")]
    public async Task AddBoardMember_AlreadyMemberOrSelf_Conflict(string email) =>
        Assert.That((await _service.AddBoardMemberAsync(_db.BoardId, _db.BoardAdminId, email)).Status, Is.EqualTo(OperationStatus.Conflict));

    [Test]
    public async Task AddBoardMember_Member_ForbiddenAndOutsider_NotFound()
    {
        Assert.That((await _service.AddBoardMemberAsync(_db.BoardId, _db.BoardMemberId, "outsider@test")).Status, Is.EqualTo(OperationStatus.Forbidden));
        Assert.That((await _service.AddBoardMemberAsync(_db.BoardId, _db.OutsiderId, "outsider@test")).Status, Is.EqualTo(OperationStatus.NotFound));
    }

    [Test]
    public async Task SetBoardAdmin_PromotesOthersButNotYourself()
    {
        Assert.That((await _service.SetBoardAdminAsync(_db.BoardId, _db.BoardAdminId, _db.BoardMemberId, true)).Succeeded, Is.True);
        Assert.That((await _service.SetBoardAdminAsync(_db.BoardId, _db.BoardAdminId, _db.BoardAdminId, false)).Status, Is.EqualTo(OperationStatus.Forbidden));

        using var db = _db.CreateDbContext();
        Assert.That((await db.BoardMemberships.SingleAsync(x => x.UserId == _db.BoardMemberId)).IsAdmin, Is.True);
        Assert.That((await db.BoardMemberships.SingleAsync(x => x.UserId == _db.BoardAdminId)).IsAdmin, Is.True);
    }

    [Test]
    public async Task SetBoardAdmin_ProjectMemberWithoutBoardMembership_NotFound() =>
        Assert.That((await _service.SetBoardAdminAsync(_db.BoardId, _db.BoardAdminId, _db.ProjectMemberId, true)).Status, Is.EqualTo(OperationStatus.NotFound));

    [Test]
    public async Task RemoveBoardMember_OthersButNotYourself()
    {
        Assert.That((await _service.RemoveBoardMemberAsync(_db.BoardId, _db.BoardAdminId, _db.BoardAdminId)).Status, Is.EqualTo(OperationStatus.Forbidden));
        Assert.That((await _service.RemoveBoardMemberAsync(_db.BoardId, _db.BoardAdminId, _db.BoardMemberId)).Succeeded, Is.True);

        using var db = _db.CreateDbContext();
        Assert.That(await db.Boards.AccessibleBy(_db.BoardMemberId).AnyAsync(), Is.False);
    }

    [Test]
    public async Task ProjectMembers_OnlyProjectAdminsManageThem()
    {
        await Assert.MultipleAsync(async () =>
        {
            // A board admin administers the board, not its project.
            Assert.That((await _service.AddProjectMemberAsync(_db.ProjectId, _db.BoardAdminId, "outsider@test")).Status, Is.EqualTo(OperationStatus.NotFound));
            Assert.That((await _service.AddProjectMemberAsync(_db.ProjectId, _db.ProjectMemberId, "outsider@test")).Status, Is.EqualTo(OperationStatus.Forbidden));
            Assert.That((await _service.AddProjectMemberAsync(_db.ProjectId, _db.ProjectAdminId, "outsider@test")).Succeeded, Is.True);
            Assert.That((await _service.SetProjectAdminAsync(_db.ProjectId, _db.ProjectAdminId, _db.OutsiderId, true)).Succeeded, Is.True);
            Assert.That((await _service.RemoveProjectMemberAsync(_db.ProjectId, _db.ProjectAdminId, _db.ProjectAdminId)).Status, Is.EqualTo(OperationStatus.Forbidden));
            Assert.That((await _service.RemoveProjectMemberAsync(_db.ProjectId, _db.ProjectAdminId, _db.ProjectMemberId)).Succeeded, Is.True);
        });

        using var db = _db.CreateDbContext();
        Assert.That(await db.Boards.AccessibleBy(_db.OutsiderId).AnyAsync(), Is.True);
        Assert.That(await db.Boards.AccessibleBy(_db.ProjectMemberId).AnyAsync(), Is.False);
    }
}
