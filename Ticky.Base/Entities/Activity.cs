namespace Ticky.Base.Entities;

public class Activity : AbstractDbEntity
{
    public required int UserId { get; set; }
    public virtual User User { get; set; } = null!;
    public required string Text { get; set; }
    public required int CardId { get; set; }
    public virtual Card Card { get; set; } = null!;

    public required ActivityType ActivityType { get; set; }

    // CardMoved payload. Names and Finished are snapshots at move-time so old rows
    // still read correctly after a column is renamed, deleted or has Finished toggled.
    public int? FromColumnId { get; set; }
    public virtual Column? FromColumn { get; set; }
    public string? FromColumnName { get; set; }
    public int? ToColumnId { get; set; }
    public virtual Column? ToColumn { get; set; }
    public string? ToColumnName { get; set; }
    public bool? ToColumnFinished { get; set; }

    public static Activity CardMoved(int cardId, int userId, Column from, Column to) =>
        new()
        {
            ActivityType = ActivityType.CardMoved,
            Text = $"<b>moved</b> the card to <b>{to.Name}</b>",
            CardId = cardId,
            UserId = userId,
            FromColumnId = from.Id,
            FromColumnName = from.Name,
            ToColumnId = to.Id,
            ToColumnName = to.Name,
            ToColumnFinished = to.Finished,
        };
}
