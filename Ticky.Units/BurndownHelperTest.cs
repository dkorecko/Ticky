using Ticky.Internal.Helpers;

namespace Ticky.Units;

public class BurndownHelperTest
{
    [Test]
    public void GetOpenPerDay_NoCards_ReturnsEmpty()
    {
        var result = BurndownHelper.GetOpenPerDay([], [], new DateTime(2025, 1, 5));

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetOpenPerDay_CountsCreatedMinusCompletedPerDay()
    {
        var created = new List<DateTime>
        {
            new(2025, 1, 1, 9, 0, 0),
            new(2025, 1, 1, 17, 0, 0),
            new(2025, 1, 3, 12, 0, 0),
        };
        var completed = new List<DateTime> { new(2025, 1, 2, 23, 59, 0), new(2025, 1, 4, 8, 0, 0) };

        var result = BurndownHelper.GetOpenPerDay(created, completed, new DateTime(2025, 1, 5, 10, 0, 0));

        Assert.That(result.Select(x => x.Date), Is.EqualTo(Enumerable.Range(1, 5).Select(d => new DateTime(2025, 1, d))));
        Assert.That(result.Select(x => x.Open), Is.EqualTo(new[] { 2, 1, 2, 1, 1 }));
    }

    [Test]
    public void GetOpenPerDay_SingleDay_ReturnsOnePoint()
    {
        var day = new DateTime(2025, 1, 1, 10, 0, 0);

        var result = BurndownHelper.GetOpenPerDay([day], [], day);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Open, Is.EqualTo(1));
    }

    [Test]
    public void GetOpenPerDay_NeverNegative()
    {
        var created = new List<DateTime> { new(2025, 1, 2) };
        var completed = new List<DateTime> { new(2025, 1, 2), new(2025, 1, 2) };

        var result = BurndownHelper.GetOpenPerDay(created, completed, new DateTime(2025, 1, 3));

        Assert.That(result.Select(x => x.Open), Is.All.EqualTo(0));
    }

    [Test]
    public void RenderSvg_NoPoints_ReturnsEmpty()
    {
        Assert.That(BurndownHelper.RenderSvg([]), Is.Empty);
    }

    [Test]
    [SetCulture("nl-BE")]
    public void RenderSvg_CommaDecimalCulture_UsesDotsInCoordinates()
    {
        // A max of 7 over the 192px chart height gives fractional y coordinates
        var points = new List<(DateTime Date, int Open)>
        {
            (new DateTime(2025, 1, 1), 7),
            (new DateTime(2025, 1, 2), 5),
            (new DateTime(2025, 1, 3), 2),
        };

        var svg = BurndownHelper.RenderSvg(points);

        Assert.Multiple(() =>
        {
            Assert.That(svg, Does.Contain("points=\"48.0,16.0 396.0,70.9 744.0,153.1\""));
            Assert.That(svg, Does.Contain("cx=\"744.0\" cy=\"153.1\""));
            Assert.That(svg, Does.Contain(">01/01<"));
            Assert.That(svg, Does.Not.Match(@"=""\d+,\d"""));
        });
    }
}
