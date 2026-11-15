using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ticky.Base.DTOs;
using Ticky.Base.DTOs.Trello;
using Ticky.Base.Models;
using Ticky.Internal.Data;

namespace Ticky.Units;

/// <summary>Runs TrelloImportService against an in-memory SQLite database.</summary>
public class TrelloImportServiceTest
{
    private SqliteConnection _connection = null!;
    private TestDbContextFactory _factory = null!;
    private TrelloImportService _service = null!;

    private int _projectId;
    private int _adminId;
    private int _memberId;
    private int _outsiderId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _factory = new TestDbContextFactory(
            new DbContextOptionsBuilder<DataContext>().UseSqlite(_connection).Options
        );
        _service = new TrelloImportService(_factory, NullLogger<TrelloImportService>.Instance);

        using var db = _factory.CreateDbContext();
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

        (_projectId, _adminId, _memberId, _outsiderId) = (project.Id, admin.Id, member.Id, outsider.Id);
    }

    [TearDown]
    public void TearDown() => _connection.Dispose();

    private static ImportModel NewImport() => new()
    {
        Code = "IMP",
        ImportDto = new TrelloImportDTO
        {
            Name = "Imported",
            Description = "",
            Starred = false,
            Preferences = new TrelloPreferencesDTO { SelfJoin = false },
            Lists = [],
            Cards = [],
            Checklists = [],
            Labels = [],
            Members = [],
        },
    };

    private async Task<int> CountBoardsAsync()
    {
        using var db = _factory.CreateDbContext();
        return await db.Boards.CountAsync();
    }

    [Test]
    public async Task Import_ProjectAdmin_CreatesBoard()
    {
        await _service.ImportTrelloBoardAsync(NewImport(), _projectId, _adminId);

        Assert.That(await CountBoardsAsync(), Is.EqualTo(1));
    }

    [Test]
    public void Import_ProjectMember_Refused()
    {
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.ImportTrelloBoardAsync(NewImport(), _projectId, _memberId));
    }

    [Test]
    public void Import_Outsider_Refused()
    {
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.ImportTrelloBoardAsync(NewImport(), _projectId, _outsiderId));
    }

    [Test]
    public async Task Import_AdminDemotedBeforeImport_RefusedAndNothingCreated()
    {
        using (var db = _factory.CreateDbContext())
        {
            await db.ProjectMemberships
                .Where(x => x.UserId == _adminId)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsAdmin, false));
        }

        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.ImportTrelloBoardAsync(NewImport(), _projectId, _adminId));
        Assert.That(await CountBoardsAsync(), Is.Zero);
    }

    private class TestDbContextFactory(DbContextOptions<DataContext> options) : IDbContextFactory<DataContext>
    {
        public DataContext CreateDbContext() => new(options);
    }
}
