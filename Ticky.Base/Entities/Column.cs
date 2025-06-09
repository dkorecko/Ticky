namespace Ticky.Base.Entities;

public class Column : AbstractDbEntity, IOrderable
{
    public required string Name { get; set; }
    public required int BoardId { get; set; }
    public required int Index { get; set; }

    [Display(Name = "Max Cards (0 = unlimited)")]
    public int MaxCards { get; set; }

    [Display(Name = "Count cards within this column as finished")]
    public bool Finished { get; set; }
    public bool Collapsed { get; set; }

    [Display(Name = "Automatic card ordering")]
    public OrderRule OrderRule { get; set; }
    public virtual Board Board { get; set; } = null!;
    public virtual List<Card> Cards { get; set; } = [];
}
