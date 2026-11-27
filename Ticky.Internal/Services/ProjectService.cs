namespace Ticky.Internal.Services;

/// <summary>
/// Creating, renaming and deleting projects, for both the web UI and the API. Anyone can create a project and becomes
/// its admin; only its admins may change or delete it. Members are managed by <see cref="MembershipService"/>.
/// </summary>
public class ProjectService(IDbContextFactory<DataContext> dbContextFactory)
{
    public async Task<OperationResult<Project>> CreateAsync(int userId, string name)
    {
        name = name?.Trim() ?? string.Empty;

        if (OperationResult.RequireText(name, nameof(Project.Name)) is { } empty)
            return empty;

        using var db = dbContextFactory.CreateDbContext();

        var project = new Project { Name = name };
        project.Memberships.Add(new ProjectMembership
        {
            ProjectId = 0,
            UserId = userId,
            IsAdmin = true,
            AddedAt = DateTime.Now,
        });

        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    public async Task<OperationResult<Project>> RenameAsync(int projectId, int userId, string name)
    {
        name = name?.Trim() ?? string.Empty;

        if (OperationResult.RequireText(name, nameof(Project.Name)) is { } empty)
            return empty;

        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckProjectAdminAsync(projectId, userId, "edit the project") is { } denied)
            return denied;

        var project = await db.Projects.FirstOrDefaultAsync(x => x.Id == projectId);

        if (project is null)
            return OperationResult.NotFound(BoardAccessHelper.PROJECT_NOT_FOUND);

        project.Name = name;

        await db.SaveChangesAsync();
        return project;
    }

    /// <summary>Deletes the project with all its boards.</summary>
    public async Task<OperationResult> DeleteAsync(int projectId, int userId)
    {
        using var db = dbContextFactory.CreateDbContext();

        if (await db.CheckProjectAdminAsync(projectId, userId, "delete the project") is { } denied)
            return denied;

        await db.Projects.Where(x => x.Id == projectId).ExecuteDeleteAsync();
        return OperationResult.Success;
    }
}
