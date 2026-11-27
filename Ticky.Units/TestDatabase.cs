using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticky.Internal.Data;

namespace Ticky.Units;

/// <summary>
/// An in-memory SQLite database for service tests: a project with one board, a "To do" column holding cards 1–3, and
/// someone in every role. It lives until disposed.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public IDbContextFactory<DataContext> Factory { get; }

    /// <summary>Admin of the project, so of its boards too.</summary>
    public int ProjectAdminId { get; private set; }

    /// <summary>Member of the project, so of its boards too.</summary>
    public int ProjectMemberId { get; private set; }

    /// <summary>Admin of the board only.</summary>
    public int BoardAdminId { get; private set; }

    /// <summary>Member of the board only.</summary>
    public int BoardMemberId { get; private set; }

    /// <summary>Can open nothing.</summary>
    public int OutsiderId { get; private set; }

    public int ProjectId { get; private set; }
    public int BoardId { get; private set; }
    public int ColumnId { get; private set; }

    /// <summary>The cards in the column, top to bottom.</summary>
    public int[] CardIds { get; private set; } = [];

    private TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Factory = new ContextFactory(new DbContextOptionsBuilder<DataContext>().UseSqlite(_connection).Options);
    }

    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase();
        await database.SeedAsync();
        return database;
    }

    public DataContext CreateDbContext() => Factory.CreateDbContext();

    /// <summary>Adds a user, e.g. to invite them by e-mail; their e-mail is their user name.</summary>
    public async Task<int> AddUserAsync(string name, string email)
    {
        using var db = CreateDbContext();
        var user = new User { DisplayName = name, UserName = email, Email = email, NormalizedEmail = email.ToUpperInvariant() };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Adds a column after the board's others and returns its id.</summary>
    public async Task<int> AddColumnAsync(string name, bool finished = false, int maxCards = 0)
    {
        using var db = CreateDbContext();
        var index = await db.Columns.CountAsync(x => x.BoardId == BoardId);
        var column = new Column { Name = name, BoardId = BoardId, Index = index, Finished = finished, MaxCards = maxCards };
        db.Columns.Add(column);
        await db.SaveChangesAsync();
        return column.Id;
    }

    /// <summary>Adds a board in a project nobody here is a member of, so only <paramref name="memberId"/> can open it.</summary>
    public async Task<(int BoardId, int ColumnId, int CardId)> AddOtherBoardAsync(int memberId)
    {
        using var db = CreateDbContext();
        var project = new Project { Name = "Other project" };
        var board = new Board { Name = "Other board", Description = "", Code = "OTH", Project = project, ProjectId = 0 };
        board.Memberships.Add(new BoardMembership { UserId = memberId, BoardId = 0, IsAdmin = false, AddedAt = DateTime.Now });
        var column = new Column { Name = "Elsewhere", Board = board, BoardId = 0, Index = 0 };
        var card = new Card { Name = "Other card", Number = 1, Index = 0, Column = column, ColumnId = 0, CreatedById = memberId };
        db.Cards.Add(card);
        await db.SaveChangesAsync();
        return (board.Id, column.Id, card.Id);
    }

    private async Task SeedAsync()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();

        User NewUser(string name, string email) =>
            new() { DisplayName = name, UserName = email, Email = email, NormalizedEmail = email.ToUpperInvariant() };

        var projectAdmin = NewUser("Project admin", "project-admin@test");
        var projectMember = NewUser("Project member", "project@test");
        var boardAdmin = NewUser("Board admin", "board-admin@test");
        var boardMember = NewUser("Board member", "board@test");
        var outsider = NewUser("Outsider", "outsider@test");
        db.Users.AddRange(projectAdmin, projectMember, boardAdmin, boardMember, outsider);
        await db.SaveChangesAsync();

        var project = new Project { Name = "Project" };
        project.Memberships.Add(new ProjectMembership { UserId = projectAdmin.Id, ProjectId = 0, IsAdmin = true, AddedAt = DateTime.Now });
        project.Memberships.Add(new ProjectMembership { UserId = projectMember.Id, ProjectId = 0, IsAdmin = false, AddedAt = DateTime.Now });
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var board = new Board { Name = "Board", Description = "", Code = "TST", ProjectId = project.Id };
        board.Memberships.Add(new BoardMembership { UserId = boardAdmin.Id, BoardId = 0, IsAdmin = true, AddedAt = DateTime.Now });
        board.Memberships.Add(new BoardMembership { UserId = boardMember.Id, BoardId = 0, IsAdmin = false, AddedAt = DateTime.Now });
        db.Boards.Add(board);
        await db.SaveChangesAsync();

        var column = new Column { Name = "To do", BoardId = board.Id, Index = 0 };
        db.Columns.Add(column);
        await db.SaveChangesAsync();

        var cards = Enumerable.Range(1, 3)
            .Select(number => new Card { Name = $"Card {number}", Number = number, Index = number - 1, ColumnId = column.Id, CreatedById = boardMember.Id })
            .ToList();
        db.Cards.AddRange(cards);
        await db.SaveChangesAsync();

        (ProjectAdminId, ProjectMemberId, BoardAdminId, BoardMemberId, OutsiderId) =
            (projectAdmin.Id, projectMember.Id, boardAdmin.Id, boardMember.Id, outsider.Id);
        (ProjectId, BoardId, ColumnId) = (project.Id, board.Id, column.Id);
        CardIds = cards.Select(x => x.Id).ToArray();
    }

    public void Dispose() => _connection.Dispose();

    private sealed class ContextFactory(DbContextOptions<DataContext> options) : IDbContextFactory<DataContext>
    {
        public DataContext CreateDbContext() => new(options);
    }
}
