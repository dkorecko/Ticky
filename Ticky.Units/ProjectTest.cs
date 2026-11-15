using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticky.Internal.Data;

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

    [Test]
    public async Task IsProjectAdminAsync_ReadsRoleFromDatabase()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DataContext>().UseSqlite(connection).Options;

        using var db = new DataContext(options);
        db.Database.EnsureCreated();

        var admin = new User { DisplayName = "Admin", UserName = "admin@test" };
        var member = new User { DisplayName = "Member", UserName = "member@test" };
        var outsider = new User { DisplayName = "Outsider", UserName = "outsider@test" };
        db.Users.AddRange(admin, member, outsider);
        await db.SaveChangesAsync();

        var project = new Project { Name = "Project" };
        project.Memberships.Add(new ProjectMembership { UserId = admin.Id, ProjectId = 0, IsAdmin = true, AddedAt = DateTime.Now });
        project.Memberships.Add(new ProjectMembership { UserId = member.Id, ProjectId = 0, IsAdmin = false, AddedAt = DateTime.Now });
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        Assert.That(await db.IsProjectAdminAsync(project.Id, admin.Id), Is.True);
        Assert.That(await db.IsProjectAdminAsync(project.Id, member.Id), Is.False);
        Assert.That(await db.IsProjectAdminAsync(project.Id, outsider.Id), Is.False);
        Assert.That(await db.IsProjectAdminAsync(project.Id + 1, admin.Id), Is.False);

        // A role taken away after the project was loaded is picked up on the next check.
        var adminMembership = await db.ProjectMemberships.SingleAsync(x => x.UserId == admin.Id);
        adminMembership.IsAdmin = false;
        await db.SaveChangesAsync();

        Assert.That(await db.IsProjectAdminAsync(project.Id, admin.Id), Is.False);
    }
}
