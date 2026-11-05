using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticky.Internal.Data;

namespace Ticky.Units;

public class BoardTest
{
    private static readonly User User = new() { Id = 1, DisplayName = "User", UserName = "user@test" };

    private static Board NewBoard(bool? boardAdmin, bool? projectAdmin)
    {
        var project = new Project { Name = "Project" };

        if (projectAdmin.HasValue)
            project.Memberships.Add(new ProjectMembership { UserId = User.Id, ProjectId = 0, IsAdmin = projectAdmin.Value, AddedAt = DateTime.Now });

        var board = new Board { Name = "Board", Description = "", Code = "TST", ProjectId = 0, Project = project };

        if (boardAdmin.HasValue)
            board.Memberships.Add(new BoardMembership { UserId = User.Id, BoardId = 0, IsAdmin = boardAdmin.Value, AddedAt = DateTime.Now });

        return board;
    }

    [Test]
    public void IsAdmin_BoardAdmin_True() => Assert.That(NewBoard(boardAdmin: true, projectAdmin: null).IsAdmin(User), Is.True);

    [Test]
    public void IsAdmin_BoardMember_False() => Assert.That(NewBoard(boardAdmin: false, projectAdmin: null).IsAdmin(User), Is.False);

    [Test]
    public void IsAdmin_ProjectAdminOnly_True() => Assert.That(NewBoard(boardAdmin: null, projectAdmin: true).IsAdmin(User), Is.True);

    [Test]
    public void IsAdmin_ProjectMemberOnly_False() => Assert.That(NewBoard(boardAdmin: null, projectAdmin: false).IsAdmin(User), Is.False);

    [Test]
    public void IsAdmin_BoardMembershipDecidesOverProject_False() =>
        Assert.That(NewBoard(boardAdmin: false, projectAdmin: true).IsAdmin(User), Is.False);

    [Test]
    public void IsAdmin_Outsider_False() => Assert.That(NewBoard(boardAdmin: null, projectAdmin: null).IsAdmin(User), Is.False);

    [Test]
    public async Task IsBoardAdminAsync_ReadsRoleFromDatabase()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DataContext>().UseSqlite(connection).Options;

        using var db = new DataContext(options);
        db.Database.EnsureCreated();

        var admin = new User { DisplayName = "Admin", UserName = "admin@test" };
        var member = new User { DisplayName = "Member", UserName = "member@test" };
        db.Users.AddRange(admin, member);
        await db.SaveChangesAsync();

        var project = new Project { Name = "Project" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var board = new Board { Name = "Board", Description = "", Code = "TST", ProjectId = project.Id };
        board.Memberships.Add(new BoardMembership { UserId = admin.Id, BoardId = 0, IsAdmin = true, AddedAt = DateTime.Now });
        board.Memberships.Add(new BoardMembership { UserId = member.Id, BoardId = 0, IsAdmin = false, AddedAt = DateTime.Now });
        db.Boards.Add(board);
        await db.SaveChangesAsync();

        Assert.That(await db.IsBoardAdminAsync(board.Id, admin), Is.True);
        Assert.That(await db.IsBoardAdminAsync(board.Id, member), Is.False);
        Assert.That(await db.IsBoardAdminAsync(board.Id + 1, admin), Is.False);

        // A role taken away after the board was loaded is picked up on the next check.
        var adminMembership = await db.BoardMemberships.SingleAsync(x => x.UserId == admin.Id);
        adminMembership.IsAdmin = false;
        await db.SaveChangesAsync();

        Assert.That(await db.IsBoardAdminAsync(board.Id, admin), Is.False);
    }
}
