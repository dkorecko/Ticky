namespace Ticky.Units;

public class IndexHelperTest
{
    private static Card NewCard(string name, int index = 0, int id = 0) =>
        new() { Id = id, Name = name, Index = index, Number = 0, ColumnId = 1, CreatedById = 1 };

    private static string[] Order(List<Card> cards) => cards.OrderBy(x => x.Index).Select(x => x.Name).ToArray();

    private static List<Card> Cards(params string[] names) => names.Select((name, i) => NewCard(name, i, i + 1)).ToList();

    [Test]
    public void Place_Top_GoesFirstAndShiftsOthers()
    {
        var cards = new List<Card> { NewCard("A", 0), NewCard("B", 1) };

        cards.Place(NewCard("New"), CardPlacement.Top);

        Assert.Multiple(() =>
        {
            Assert.That(Order(cards), Is.EqualTo(new[] { "New", "A", "B" }));
            Assert.That(cards.Select(x => x.Index).Order(), Is.EqualTo(new[] { 0, 1, 2 }));
        });
    }

    [Test]
    public void Place_Bottom_GoesLast()
    {
        var cards = new List<Card> { NewCard("A", 0), NewCard("B", 1) };

        cards.Place(NewCard("New"), CardPlacement.Bottom);

        Assert.Multiple(() =>
        {
            Assert.That(Order(cards), Is.EqualTo(new[] { "A", "B", "New" }));
            Assert.That(cards.Select(x => x.Index).Order(), Is.EqualTo(new[] { 0, 1, 2 }));
        });
    }

    [TestCase(CardPlacement.Top)]
    [TestCase(CardPlacement.Bottom)]
    public void Place_EmptyColumn_GoesFirst(CardPlacement placement)
    {
        var cards = new List<Card>();

        cards.Place(NewCard("New", index: 5), placement);

        Assert.That(cards.Single().Index, Is.Zero);
    }

    [Test]
    public void MoveBefore_Upwards_InsertsBeforeTarget()
    {
        var cards = Cards("A", "B", "C", "D");

        cards.MoveBefore(cards[3], cards[1]);

        Assert.That(Order(cards), Is.EqualTo(new[] { "A", "D", "B", "C" }));
        Assert.That(cards.Select(x => x.Index).Order(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
    }

    [Test]
    public void MoveBefore_Downwards_InsertsBeforeTarget()
    {
        var cards = Cards("A", "B", "C", "D");

        cards.MoveBefore(cards[0], cards[3]);

        Assert.That(Order(cards), Is.EqualTo(new[] { "B", "C", "A", "D" }));
    }

    [Test]
    public void MoveBefore_NoTarget_GoesLast()
    {
        var cards = Cards("A", "B", "C");

        cards.MoveBefore(cards[0], null);

        Assert.That(Order(cards), Is.EqualTo(new[] { "B", "C", "A" }));
    }

    [Test]
    public void MoveBefore_Itself_StaysPut()
    {
        var cards = Cards("A", "B", "C");

        cards.MoveBefore(cards[1], cards[1]);

        Assert.That(Order(cards), Is.EqualTo(new[] { "A", "B", "C" }));
    }

    [Test]
    public void MoveBefore_FromAnotherList_NumbersItIn()
    {
        var cards = Cards("A", "B");
        var incoming = NewCard("New", index: 7, id: 99);

        cards.MoveBefore(incoming, cards[1]);
        cards.Add(incoming);

        Assert.That(Order(cards), Is.EqualTo(new[] { "A", "New", "B" }));
        Assert.That(cards.Select(x => x.Index).Order(), Is.EqualTo(new[] { 0, 1, 2 }));
    }
}
