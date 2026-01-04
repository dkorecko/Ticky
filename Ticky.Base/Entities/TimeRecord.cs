namespace Ticky.Base.Entities;

public class TimeRecord : AbstractDbEntity
{
    public required int CardId { get; set; }

    public virtual Card Card { get; set; } = null!;

    public required int UserId { get; set; }

    public virtual User User { get; set; } = null!;

    public required DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }
    
    public TimeSpan GetTotalTime() =>
        new TimeSpan(
            ((EndedAt ?? DateTime.Now) - StartedAt).Ticks
        );
}
