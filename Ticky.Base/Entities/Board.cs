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

    public bool VerifyAccess(User user) =>
        Memberships.Any(x => x.UserId.Equals(user.Id))
        || Project.Memberships.Any(x => x.UserId.Equals(user.Id));

    /// <summary>
    /// Whether the user may manage the board (settings, labels, columns, members). A board membership decides when
    /// there is one; otherwise the project membership does. Needs <see cref="Memberships"/> and the project's
    /// memberships loaded.
    /// </summary>
    public bool IsAdmin(User user)
    {
        var boardMembership = Memberships.FirstOrDefault(x => x.UserId.Equals(user.Id));

        if (boardMembership is not null)
            return boardMembership.IsAdmin;

        return Project.Memberships.FirstOrDefault(x => x.UserId.Equals(user.Id))?.IsAdmin ?? false;
    }
}
