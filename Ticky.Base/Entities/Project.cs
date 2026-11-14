using System.Linq.Expressions;

namespace Ticky.Base.Entities;

public class Project : AbstractDbEntity, IDeletable
{
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }

    public virtual List<Board> Boards { get; set; } = [];

    public virtual List<ProjectMembership> Memberships { get; set; } = [];

    /// <summary>
    /// The projects the user administers, as a query condition. Only their admins may add boards to a project, by
    /// creating or cloning one.
    /// </summary>
    public static Expression<Func<Project, bool>> AdministeredBy(int userId) =>
        project => project.Memberships.Any(m => m.UserId == userId && m.IsAdmin);
}
