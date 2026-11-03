using Ticky.Internal.Services.Hosted;

namespace Ticky.Units;

public class RepeatHostedServiceTest
{
    private static Card NewCard(string name, int index = 0) =>
        new() { Name = name, Index = index, Number = 0, ColumnId = 1, CreatedById = 1 };

    private static string[] Order(List<Card> cards) => cards.OrderBy(x => x.Index).Select(x => x.Name).ToArray();

    [Test]
    public void PlaceNewCard_Top_GoesFirstAndShiftsOthers()
    {
        var cards = new List<Card> { NewCard("A", 0), NewCard("B", 1) };

        RepeatHostedService.PlaceNewCard(cards, NewCard("New"), CardPlacement.Top);

        Assert.Multiple(() =>
        {
            Assert.That(Order(cards), Is.EqualTo(new[] { "New", "A", "B" }));
            Assert.That(cards.Select(x => x.Index).Order(), Is.EqualTo(new[] { 0, 1, 2 }));
        });
    }

    [Test]
    public void PlaceNewCard_Bottom_GoesLast()
    {
        var cards = new List<Card> { NewCard("A", 0), NewCard("B", 1) };

        RepeatHostedService.PlaceNewCard(cards, NewCard("New"), CardPlacement.Bottom);

        Assert.Multiple(() =>
        {
            Assert.That(Order(cards), Is.EqualTo(new[] { "A", "B", "New" }));
            Assert.That(cards.Select(x => x.Index).Order(), Is.EqualTo(new[] { 0, 1, 2 }));
        });
    }

    [TestCase(CardPlacement.Top)]
    [TestCase(CardPlacement.Bottom)]
    public void PlaceNewCard_EmptyColumn_GoesFirst(CardPlacement placement)
    {
        var cards = new List<Card>();

        RepeatHostedService.PlaceNewCard(cards, NewCard("New", index: 5), placement);

        Assert.That(cards.Single().Index, Is.Zero);
    }
}
