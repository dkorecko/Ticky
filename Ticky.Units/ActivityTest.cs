namespace Ticky.Units;

public class ActivityTest
{
    private static Column CreateColumn(int id, string name, bool finished) =>
        new()
        {
            Id = id,
            Name = name,
            BoardId = 1,
            Index = 0,
            Finished = finished,
        };

    [Test]
    public void CardMoved_SetsTypeAndSnapshotsColumns()
    {
        var from = CreateColumn(10, "In progress", false);
        var to = CreateColumn(11, "Done", true);

        var activity = Activity.CardMoved(5, 7, from, to);

        Assert.Multiple(() =>
        {
            Assert.That(activity.ActivityType, Is.EqualTo(ActivityType.CardMoved));
            Assert.That(activity.CardId, Is.EqualTo(5));
            Assert.That(activity.UserId, Is.EqualTo(7));
            Assert.That(activity.FromColumnId, Is.EqualTo(10));
            Assert.That(activity.FromColumnName, Is.EqualTo("In progress"));
            Assert.That(activity.ToColumnId, Is.EqualTo(11));
            Assert.That(activity.ToColumnName, Is.EqualTo("Done"));
            Assert.That(activity.ToColumnFinished, Is.True);
        });
    }

    [Test]
    public void CardMoved_KeepsExistingTimelineText()
    {
        var activity = Activity.CardMoved(1, 1, CreateColumn(1, "Todo", false), CreateColumn(2, "Doing", false));

        Assert.That(activity.Text, Is.EqualTo("<b>moved</b> the card to <b>Doing</b>"));
    }

    [Test]
    public void CardMoved_SnapshotDoesNotFollowLaterColumnChanges()
    {
        var to = CreateColumn(2, "Done", true);
        var activity = Activity.CardMoved(1, 1, CreateColumn(1, "Todo", false), to);

        to.Name = "Archive";
        to.Finished = false;

        Assert.Multiple(() =>
        {
            Assert.That(activity.ToColumnName, Is.EqualTo("Done"));
            Assert.That(activity.ToColumnFinished, Is.True);
        });
    }

    [Test]
    public void NewActivity_DefaultsToGenericWithoutMovePayload()
    {
        var activity = new Activity { Text = "<b>changed</b> the title", UserId = 1, CardId = 1 };

        Assert.Multiple(() =>
        {
            Assert.That(activity.ActivityType, Is.EqualTo(ActivityType.Generic));
            Assert.That(activity.FromColumnId, Is.Null);
            Assert.That(activity.ToColumnId, Is.Null);
            Assert.That(activity.ToColumnFinished, Is.Null);
        });
    }

    [Test]
    public void ActivityType_PersistedValuesAreStable()
    {
        // Rows store the int value; renumbering would silently relabel existing history.
        Assert.Multiple(() =>
        {
            Assert.That((int)ActivityType.Generic, Is.EqualTo(0));
            Assert.That((int)ActivityType.CardMoved, Is.EqualTo(1));
        });
    }
}
