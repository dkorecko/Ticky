namespace Ticky.Base.Entities;

public class Column : AbstractDbEntity, IOrderable, IDeletable
{
    public required string Name { get; set; }
    public required int BoardId { get; set; }
    public required int Index { get; set; }

    [Display(Name = "Max Cards (0 = unlimited)")]
    public int MaxCards { get; set; }

    [Display(Name = "Count cards within this column as finished")]
    public bool Finished { get; set; }
    public bool Collapsed { get; set; }

    /// <remarks>
    /// New columns start as <see cref="OrderRule.Migrated"/>: on startup, <c>DataMigrator</c> sets the card placement
    /// of every other column from this rule, which would undo a placement chosen since.
    /// </remarks>
    [Obsolete("This property is deprecated and will be removed in next major release.")]
    [Display(Name = "Automatic card ordering")]
    public OrderRule OrderRule { get; set; } = OrderRule.Migrated;
    public virtual Board Board { get; set; } = null!;
    public virtual List<Card> Cards { get; set; } = [];

    [Display(Name = "New card placement")]
    public CardPlacement NewCardPlacement { get; set; } = CardPlacement.Bottom;

    /// <summary>Whether the column holds its maximum number of cards, so no more can be added. Needs <see cref="Cards"/> loaded.</summary>
    public bool IsFull() => MaxCards != 0 && Cards.Count >= MaxCards;
}
