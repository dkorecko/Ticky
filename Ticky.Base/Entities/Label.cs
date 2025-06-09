namespace Ticky.Base.Entities
{
    public class Label : AbstractDbEntity
    {
        public required string Text { get; set; }

        public required Color TextColor { get; set; }

        public required Color BackgroundColor { get; set; }

        public required int BoardId { get; set; }

        public virtual Board Board { get; set; } = null!;

        public virtual List<Card> OnCards { get; set; } = [];
    }
}
