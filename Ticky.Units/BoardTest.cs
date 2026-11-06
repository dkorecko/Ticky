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
}
