namespace Ticky.Units;

public class CardTest
{
    private Card? _card;

    [SetUp]
    public void Setup()
    {
        _card = new()
        {
            Index = 0,
            Number = 1,
            ColumnId = 0,
            Name = Constants.NAME,
            CreatedById = 1,
            RepeatInfo = new RepeatInfo
            {
                Type = RepeatType.Daily,
                LastRepeat = DateTime.MinValue,
                Time = TimeOnly.MinValue,
                CardPlacement = CardPlacement.Top
            },
        };
    }

    [Test]
    public void CalculateNextRepeat_Daily_SameDay()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2025, 1, 1, 15, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.Daily,
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_Daily_NextDay()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2025, 1, 2, 9, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.Daily,
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_WeekDays_SameDay()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 9, 0, 0);
        var nextRepeat = new DateTime(2025, 1, 1, 11, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.WeekDays,
            Selected = "Mon,Tue,Wed",
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_WeekDays_NextDay()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2025, 1, 6, 9, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.WeekDays,
            Selected = "Mon,Tue,Wed",
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_MonthDayNumber_SameDay()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 9, 0, 0);
        var nextRepeat = new DateTime(2025, 1, 1, 11, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.MonthDayNumber,
            Selected = "1",
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_MonthDayNumber_NextDay()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2025, 1, 29, 9, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.MonthDayNumber,
            Selected = "1,29",
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_EveryXthDay_TimeBefore()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2025, 1, 3, 9, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.EveryXthDay,
            Number = 2,
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_EveryXthWeek_TimeBefore()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2025, 1, 15, 9, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.EveryXthWeek,
            Number = 2,
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_EveryXthMonth_TimeBefore()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2025, 3, 1, 9, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.EveryXthMonth,
            Number = 2,
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_EveryXthYear_TimeBefore()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2026, 1, 1, 9, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.EveryXthYear,
            Number = 1,
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_EveryXthDay_TimeAfter()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2025, 1, 3, 13, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.EveryXthDay,
            Number = 2,
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_EveryXthWeek_TimeAfter()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2025, 1, 15, 13, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.EveryXthWeek,
            Number = 2,
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_EveryXthMonth_TimeAfter()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2025, 3, 1, 13, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.EveryXthMonth,
            Number = 2,
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void CalculateNextRepeat_EveryXthYear_TimeAfter()
    {
        var lastRepeat = new DateTime(2025, 1, 1, 11, 0, 0);
        var nextRepeat = new DateTime(2026, 1, 1, 13, 0, 0);

        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.EveryXthYear,
            Number = 1,
            LastRepeat = lastRepeat,
            Time = TimeOnly.FromDateTime(nextRepeat),
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card!.CalculateNextRepeat(lastRepeat), Is.EqualTo(nextRepeat));
    }
}
