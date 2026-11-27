using System.Linq.Expressions;

namespace Ticky.Base.Entities;

public class Board : AbstractDbEntity, IDeletable
{
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }
    public required string Description { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MinLength(1)]
    [MaxLength(5)]
    [RegularExpression("^[A-Z]*$", ErrorMessage = "The code must be in upper-case.")]
    public required string Code { get; set; }
    public required int ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;
    public virtual List<Column> Columns { get; set; } = [];
    public virtual List<BoardMembership> Memberships { get; set; } = [];
    public virtual List<Label> Labels { get; set; } = [];
    public virtual List<LastVisit> LastVisits { get; set; } = [];
    public virtual List<Favorite> Favorites { get; set; } = [];
    public bool DisableSortingAnimations { get; set; }

    /// <summary>
    /// Whether the user may open the board: they're a member of it or of its project. Needs <see cref="Memberships"/>
    /// and the project's memberships loaded; <see cref="AccessibleBy"/> is the same rule for database queries.
    /// </summary>
    public bool VerifyAccess(User user) => VerifyAccess(user.Id);

    /// <inheritdoc cref="VerifyAccess(User)"/>
    public bool VerifyAccess(int userId) =>
        Memberships.Any(x => x.UserId == userId) || Project.Memberships.Any(x => x.UserId == userId);

    /// <summary>
    /// <see cref="VerifyAccess(int)"/> as a query condition, so the database filters boards by it. Keep the two in step.
    /// Use it through the <c>AccessibleBy</c> query extensions, which also apply it to columns and cards.
    /// </summary>
    public static Expression<Func<Board, bool>> AccessibleBy(int userId) =>
        board =>
            board.Memberships.Any(m => m.UserId == userId)
            || board.Project.Memberships.Any(m => m.UserId == userId);

    /// <inheritdoc cref="IsAdmin(int)"/>
    public bool IsAdmin(User user) => IsAdmin(user.Id);

    /// <summary>
    /// Whether the user may manage the board (settings, labels, columns, members). A board membership decides when
    /// there is one; otherwise the project membership does. Needs <see cref="Memberships"/> and the project's
    /// memberships loaded.
    /// </summary>
    public bool IsAdmin(int userId)
    {
        var boardMembership = Memberships.FirstOrDefault(x => x.UserId == userId);

        if (boardMembership is not null)
            return boardMembership.IsAdmin;

        return Project.Memberships.FirstOrDefault(x => x.UserId == userId)?.IsAdmin ?? false;
    }
}
