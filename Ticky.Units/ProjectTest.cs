namespace Ticky.Units;

public class ProjectTest
{
    private const int UserId = 1;

    private static bool IsAdministeredBy(bool? projectAdmin)
    {
        var project = new Project { Name = "Project" };

        if (projectAdmin.HasValue)
            project.Memberships.Add(new ProjectMembership { UserId = UserId, ProjectId = 0, IsAdmin = projectAdmin.Value, AddedAt = DateTime.Now });

        // Someone else's admin membership must not count for the user.
        project.Memberships.Add(new ProjectMembership { UserId = UserId + 1, ProjectId = 0, IsAdmin = true, AddedAt = DateTime.Now });

        return Project.AdministeredBy(UserId).Compile()(project);
    }

    [Test]
    public void AdministeredBy_ProjectAdmin_True() => Assert.That(IsAdministeredBy(projectAdmin: true), Is.True);

    [Test]
    public void AdministeredBy_ProjectMember_False() => Assert.That(IsAdministeredBy(projectAdmin: false), Is.False);

    [Test]
    public void AdministeredBy_NotAMember_False() => Assert.That(IsAdministeredBy(projectAdmin: null), Is.False);
}
