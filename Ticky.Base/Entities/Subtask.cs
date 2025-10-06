namespace Ticky.Base.Entities;

public class Subtask : AbstractDbEntity, IOrderable, IAssignable
{
    public required int Index { get; set; }
    public required string Text { get; set; }
    public bool Completed { get; set; }
    public required int CardId { get; set; }
    public virtual Card Card { get; set; } = null!;
    public virtual List<User> Assignees { get; set; } = [];
}
