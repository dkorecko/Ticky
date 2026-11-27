namespace Ticky.Internal.Services;

/// <summary>
/// Adding people to boards and projects, changing their role and removing them, for both the web UI and the API. Only
/// admins of the board or project may, and nobody can change their own role or remove themselves, so a board or
/// project never loses the admin making the change. Boards and projects follow the same rules, written once.
/// </summary>
public class MembershipService(IDbContextFactory<DataContext> dbContextFactory)
{
    private const string MANAGE_BOARD_MEMBERS = "manage the board's members";
    private const string MANAGE_PROJECT_MEMBERS = "manage the project's members";

    #region Boards

    /// <summary>Adds the person with this e-mail address to the board as a member.</summary>
    public async Task<OperationResult<BoardMembership>> AddBoardMemberAsync(int boardId, int userId, string email)
    {
        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckBoardAdminAsync(boardId, userId, MANAGE_BOARD_MEMBERS) is { } denied)
            return denied;

        var memberships = await db.BoardMemberships.Where(x => x.BoardId == boardId).ToListAsync();

        return await AddAsync(db, memberships, userId, email, "board", user => new BoardMembership
        {
            BoardId = boardId,
            UserId = user.Id,
            IsAdmin = false,
            AddedAt = DateTime.Now,
        });
    }

    public async Task<OperationResult> SetBoardAdminAsync(int boardId, int userId, int memberId, bool isAdmin)
    {
        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckBoardAdminAsync(boardId, userId, MANAGE_BOARD_MEMBERS) is { } denied)
            return denied;

        var memberships = await db.BoardMemberships.Where(x => x.BoardId == boardId).ToListAsync();
        return await SetAdminAsync(db, memberships, userId, memberId, isAdmin, "board");
    }

    /// <summary>
    /// Removes the person's board membership. Members of the board's project keep their access through the project.
    /// </summary>
    public async Task<OperationResult> RemoveBoardMemberAsync(int boardId, int userId, int memberId)
    {
        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckBoardAdminAsync(boardId, userId, MANAGE_BOARD_MEMBERS) is { } denied)
            return denied;

        var memberships = await db.BoardMemberships.Where(x => x.BoardId == boardId).ToListAsync();
        return await RemoveAsync(db, memberships, userId, memberId, "board");
    }

    #endregion

    #region Projects

    /// <summary>Adds the person with this e-mail address to the project as a member, which opens all its boards to them.</summary>
    public async Task<OperationResult<ProjectMembership>> AddProjectMemberAsync(int projectId, int userId, string email)
    {
        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckProjectAdminAsync(projectId, userId, MANAGE_PROJECT_MEMBERS) is { } denied)
            return denied;

        var memberships = await db.ProjectMemberships.Where(x => x.ProjectId == projectId).ToListAsync();

        return await AddAsync(db, memberships, userId, email, "project", user => new ProjectMembership
        {
            ProjectId = projectId,
            UserId = user.Id,
            IsAdmin = false,
            AddedAt = DateTime.Now,
        });
    }

    public async Task<OperationResult> SetProjectAdminAsync(int projectId, int userId, int memberId, bool isAdmin)
    {
        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckProjectAdminAsync(projectId, userId, MANAGE_PROJECT_MEMBERS) is { } denied)
            return denied;

        var memberships = await db.ProjectMemberships.Where(x => x.ProjectId == projectId).ToListAsync();
        return await SetAdminAsync(db, memberships, userId, memberId, isAdmin, "project");
    }

    public async Task<OperationResult> RemoveProjectMemberAsync(int projectId, int userId, int memberId)
    {
        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckProjectAdminAsync(projectId, userId, MANAGE_PROJECT_MEMBERS) is { } denied)
            return denied;

        var memberships = await db.ProjectMemberships.Where(x => x.ProjectId == projectId).ToListAsync();
        return await RemoveAsync(db, memberships, userId, memberId, "project");
    }

    #endregion

    /// <param name="memberships">The board's or project's current memberships, tracked by <paramref name="db"/>.</param>
    /// <param name="place">"board" or "project", for messages.</param>
    private static async Task<OperationResult<T>> AddAsync<T>(
        DataContext db,
        List<T> memberships,
        int userId,
        string email,
        string place,
        Func<User, T> create
    )
        where T : class, IMembership
    {
        var normalizedEmail = email?.Trim().ToUpperInvariant() ?? string.Empty;
        var user = normalizedEmail.Length == 0
            ? null
            : await db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

        if (user is null)
            return OperationResult.Invalid("Email", "There is no user with this e-mail address.");

        // The admin adding people already has access, so adding themselves counts as a duplicate too.
        if (user.Id == userId || memberships.Any(x => x.UserId == user.Id))
            return OperationResult.Conflict($"This user already has access to this {place}.");

        var membership = create(user);
        membership.User = user;
        db.Add(membership);

        await db.SaveChangesAsync();
        return membership;
    }

    private static async Task<OperationResult> SetAdminAsync<T>(
        DataContext db,
        List<T> memberships,
        int userId,
        int memberId,
        bool isAdmin,
        string place
    )
        where T : class, IMembership
    {
        if (memberId == userId)
            return OperationResult.Forbidden("You can't change your own role.");

        var membership = memberships.FirstOrDefault(x => x.UserId == memberId);

        if (membership is null)
            return OperationResult.NotFound($"This user isn't a member of the {place}.");

        membership.IsAdmin = isAdmin;

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }

    private static async Task<OperationResult> RemoveAsync<T>(DataContext db, List<T> memberships, int userId, int memberId, string place)
        where T : class, IMembership
    {
        if (memberId == userId)
            return OperationResult.Forbidden($"You can't remove yourself from the {place}.");

        var membership = memberships.FirstOrDefault(x => x.UserId == memberId);

        if (membership is null)
            return OperationResult.NotFound($"This user isn't a member of the {place}.");

        db.Remove(membership);

        await db.SaveChangesAsync();
        return OperationResult.Success;
    }
}
