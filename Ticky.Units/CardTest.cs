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
    public void GetNextRepeat_Daily_SameDay()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_Daily_NextDay()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_WeekDays_SameDay()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_WeekDays_NextDay()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_MonthDayNumber_SameDay()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_MonthDayNumber_NextDay()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_EveryXthDay_TimeBefore()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_EveryXthWeek_TimeBefore()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_EveryXthMonth_TimeBefore()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_EveryXthYear_TimeBefore()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_EveryXthDay_TimeAfter()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_EveryXthWeek_TimeAfter()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_EveryXthMonth_TimeAfter()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    [Test]
    public void GetNextRepeat_EveryXthYear_TimeAfter()
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

        Assert.That(_card!.RepeatInfo!.GetNextRepeat(), Is.EqualTo(nextRepeat));
    }

    // Stored schedules that no date matches (a trailing newline once passed validation) or that would overflow: the
    // search for the next date must give up instead of looping forever, which hung the repeat job and the board.
    [TestCase(RepeatType.WeekDays, "Sun\n", null)]
    [TestCase(RepeatType.WeekDays, "Funday", null)]
    [TestCase(RepeatType.MonthDayNumber, "32", null)]
    [TestCase(RepeatType.MonthDayNumber, null, null)]
    [TestCase(RepeatType.EveryXthDay, null, 0)]
    [TestCase(RepeatType.EveryXthYear, null, 1000)]
    public void GetNextRepeat_InvalidStoredSchedule_Null(RepeatType type, string? selected, int? number)
    {
        _card!.RepeatInfo = new RepeatInfo
        {
            Type = type,
            Selected = selected,
            Number = number,
            LastRepeat = new DateTime(2025, 1, 1),
            Time = TimeOnly.MinValue,
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card.RepeatInfo.GetNextRepeat(), Is.Null);
    }

    [Test]
    public void GetNextRepeat_StartNearTheEndOfTime_Null()
    {
        _card!.RepeatInfo = new RepeatInfo
        {
            Type = RepeatType.EveryXthYear,
            Number = 999,
            LastRepeat = new DateTime(9999, 12, 31),
            Time = TimeOnly.MinValue,
            CardPlacement = CardPlacement.Top
        };

        Assert.That(_card.RepeatInfo.GetNextRepeat(), Is.Null);
    }
}
