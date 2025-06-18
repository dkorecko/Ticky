namespace Ticky.Base.Entities;

public class User : IdentityUser<int>, IDbEntry, IDeletable
{
    [NotMapped]
    public string Name
    {
        get => DisplayName;
    }

    public virtual Code? EmailVerificationCode { get; set; }

    public virtual List<ProjectMembership> ProjectMemberships { get; set; } = [];

    public virtual List<BoardMembership> BoardMemberships { get; set; } = [];

    public virtual List<Card> CreatedCards { get; set; } = [];

    public virtual List<Comment> CreatedComments { get; set; } = [];

    public virtual List<Card> AssignedTo { get; set; } = [];

    public virtual List<Activity> Activities { get; set; } = [];

    public virtual List<TimeRecord> TimeRecords { get; set; } = [];

    public virtual List<LastVisit> LastVisits { get; set; } = [];

    public virtual List<Favorite> Favorites { get; set; } = [];

    public required string DisplayName { get; set; }

    public bool InstantDelete { get; set; }

    public bool AutomaticCardEdit { get; set; }

    public bool AutomaticDeadlineReminder { get; set; }

    public bool AutomaticAssign { get; set; }

    public string? ProfilePictureFileName { get; set; }

    public int? LastVisitedBoardId { get; set; }

    public bool NeedsNewCredentials { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
