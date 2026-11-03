using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ticky.Internal.Data;
using Ticky.Internal.Helpers;

namespace Ticky.Units;

public class ActivityTest
{
    #region Model

    [Test]
    public void EveryActivityType_HasExactlyOneClass()
    {
        var mapped = DataContext.ActivityClasses.Select(x => ((Activity)Activator.CreateInstance(x)!).ActivityType).ToList();

        Assert.That(mapped, Is.Unique);
        Assert.That(mapped, Is.EquivalentTo(Enum.GetValues<ActivityType>()));
    }

    [Test]
    public void EveryActivityClass_CanBeExportedWithItsKind()
    {
        foreach (var type in DataContext.ActivityClasses)
        {
            var activity = (Activity)Activator.CreateInstance(type)!;
            var json = JsonSerializer.Serialize(activity);

            Assert.That(json, Does.Contain($"\"kind\":\"{activity.ActivityType}\""), type.Name);
        }
    }

    [Test]
    public void EveryActivityClass_HasText()
    {
        foreach (var type in DataContext.ActivityClasses.Where(x => x != typeof(GenericActivity)))
            Assert.That(((Activity)Activator.CreateInstance(type)!).ToHtml(), Is.Not.Empty, type.Name);
    }

    [Test]
    public void Text_EscapesValues()
    {
        var activity = new LabelAddedActivity { LabelName = "<script>R&D</script>" };

        Assert.That(activity.ToHtml(), Is.EqualTo("<b>added</b> label <b>&lt;script&gt;R&amp;D&lt;/script&gt;</b>"));
    }

    [Test]
    public void Text_LegacyTextWins()
    {
        var activity = new LabelAddedActivity { LabelName = "Bug", LegacyText = "<b>something</b> else" };

        Assert.That(activity.ToHtml(), Is.EqualTo("<b>something</b> else"));
    }

    #endregion

    #region Converting old text

    // One sample of every text template activities have ever been stored with.
    private static readonly (string Text, ActivityType Type)[] LegacySamples =
    [
        ("<b>moved</b> the card to <b>Done</b>", ActivityType.CardMoved),
        ("<b>changed</b> the title", ActivityType.TitleChanged),
        ("<b>changed</b> the description", ActivityType.DescriptionChanged),
        ("<b>posted</b> a comment", ActivityType.CommentPosted),
        ("<b>joined</b> the card", ActivityType.AssigneeAdded),
        ("<b>left</b> the card", ActivityType.AssigneeRemoved),
        ("<b>added</b> the user <b>Bob</b> as an assignee", ActivityType.AssigneeAdded),
        ("<b>removed</b> the user <b>Bob</b> from the assignees", ActivityType.AssigneeRemoved),
        ("<b>added</b> the user <b>Bob</b> as a subtask assignee", ActivityType.SubtaskAssigneeAdded),
        ("<b>removed</b> the user <b>Bob</b> from the subtask assignees", ActivityType.SubtaskAssigneeRemoved),
        ("<b>added</b> label <b>Bug</b>", ActivityType.LabelAdded),
        ("<b>removed</b> label <b>Bug</b>", ActivityType.LabelRemoved),
        ("<b>changed</b> the priority to <b>High</b>", ActivityType.PriorityChanged),
        ("<b>set</b> the deadline for <b>5.11.2026 12:00</b>", ActivityType.DeadlineSet),
        ("<b>deleted</b> the deadline", ActivityType.DeadlineRemoved),
        ("<b>added</b> a reminder for <b>5.11.2026 08:30</b>", ActivityType.ReminderAdded),
        ("<b>deleted</b> the reminder at <b>5.11.2026 08:30</b>", ActivityType.ReminderRemoved),
        ("<b>added</b> subtask <b>Write tests</b>", ActivityType.SubtaskAdded),
        ("<b>edited</b> subtask <b>Write tests</b> to <b>Write more tests</b>", ActivityType.SubtaskEdited),
        ("<b>marked</b> the subtask <b>Write tests</b> as <b>completed</b>", ActivityType.SubtaskCompletionChanged),
        ("<b>marked</b> the subtask <b>Write tests</b> as <b>to be done</b>", ActivityType.SubtaskCompletionChanged),
        ("<b>deleted</b> subtask <b>Write tests</b>", ActivityType.SubtaskDeleted),
        ("<b>uploaded</b> file named <b>notes.txt</b>", ActivityType.AttachmentUploaded),
        ("<b>deleted</b> file named <b>notes.txt</b>", ActivityType.AttachmentDeleted),
        ("<b>added</b> a time record for <b>1h 5m 0s</b>", ActivityType.TimeRecordAdded),
        ("<b>changed</b> the time record from <b>1h</b> to <b>45m</b>", ActivityType.TimeRecordChanged),
        ("<b>deleted</b> time record of <b>45m 0s</b>", ActivityType.TimeRecordDeleted),
        ("<b>added</b> a linked issue <b>TST-2</b> with <b>blocks</b> relationship", ActivityType.LinkAdded),
        ("<b>deleted</b> a linked issue <b>TST-2</b> with <b>blocks</b> relationship", ActivityType.LinkRemoved),
        ("<b>snoozed</b> this card until <b>6.11.2026 07:00</b>", ActivityType.Snoozed),
        ("<b>unsnoozed</b> this card", ActivityType.Unsnoozed),
        ("<b>set up card repeat</b> for this card for <b>Daily</b>", ActivityType.RepeatSet),
        ("<b>turned repeating off</b> for this card", ActivityType.RepeatDisabled),
        ("<b>was repeated</b> from card <b>TST-2</b>", ActivityType.RepeatedFrom),
        ("<b>created</b> repeat card <b>TST-2</b>", ActivityType.RepeatCardCreated),
    ];

    private SqliteConnection _connection = null!;
    private DbContextOptions<DataContext> _options = null!;
    private int _annId, _bobId, _cardId, _otherCardId, _doneColumnId, _labelId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<DataContext>().UseSqlite(_connection).Options;

        using var db = new DataContext(_options);
        db.Database.EnsureCreated();

        var ann = new User { DisplayName = "Ann", UserName = "ann@test" };
        var bob = new User { DisplayName = "Bob", UserName = "bob@test" };
        db.Users.AddRange(ann, bob);
        var project = new Project { Name = "Project" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var board = new Board { Name = "Board", Description = "", Code = "TST", ProjectId = project.Id };
        db.Boards.Add(board);
        await db.SaveChangesAsync();

        var todo = new Column { Name = "To do", BoardId = board.Id, Index = 0 };
        var done = new Column { Name = "Done", BoardId = board.Id, Index = 1, Finished = true };
        var label = new Label { Name = "Bug", BoardId = board.Id, TextColor = System.Drawing.Color.White, BackgroundColor = System.Drawing.Color.Red };
        db.AddRange(todo, done, label);
        await db.SaveChangesAsync();

        var card = new Card { Name = "Card", Number = 1, Index = 0, ColumnId = todo.Id, CreatedById = ann.Id };
        var otherCard = new Card { Name = "Other", Number = 2, Index = 1, ColumnId = todo.Id, CreatedById = ann.Id };
        db.Cards.AddRange(card, otherCard);
        await db.SaveChangesAsync();

        (_annId, _bobId, _cardId, _otherCardId, _doneColumnId, _labelId) = (ann.Id, bob.Id, card.Id, otherCard.Id, done.Id, label.Id);
    }

    [TearDown]
    public void TearDown() => _connection.Dispose();

    /// <summary>Stores rows as the migration leaves them (Generic, with text), then runs the startup conversion.</summary>
    private Task<List<Activity>> ConvertAsync(params string[] texts) => ConvertAsync(NullLogger.Instance, texts);

    private async Task<List<Activity>> ConvertAsync(ILogger logger, params string[] texts)
    {
        using (var db = new DataContext(_options))
        {
            db.Activities.AddRange(texts.Select(text => new GenericActivity { LegacyText = text, CardId = _cardId, UserId = _annId }));
            await db.SaveChangesAsync();
        }

        using (var db = new DataContext(_options))
            await LegacyActivityConverter.ConvertAsync(db, logger, batchSize: 7);

        using var result = new DataContext(_options);
        return await result.Activities.OrderBy(x => x.Id).ToListAsync();
    }

    [Test]
    public async Task Convert_EveryTemplate_GetsItsTypeAndReadsTheSame()
    {
        var activities = await ConvertAsync(LegacySamples.Select(x => x.Text).ToArray());

        Assert.Multiple(() =>
        {
            for (int i = 0; i < LegacySamples.Length; i++)
            {
                var (text, type) = LegacySamples[i];
                Assert.That(activities[i].ActivityType, Is.EqualTo(type), text);
                Assert.That(activities[i].LegacyText, Is.Null, $"not converted: {text}");
                Assert.That(activities[i].ToHtml(), Is.EqualTo(text));
            }
        });
    }

    [Test]
    public async Task Convert_FillsDataAndLinksWhatStillExists()
    {
        var activities = await ConvertAsync(
            "<b>moved</b> the card to <b>Done</b>",
            "<b>added</b> label <b>Bug</b>",
            "<b>added</b> the user <b>Bob</b> as an assignee",
            "<b>joined</b> the card",
            "<b>added</b> a linked issue <b>TST-2</b> with <b>blocks</b> relationship",
            "<b>changed</b> the time record from <b>1h</b> to <b>45m</b>",
            "<b>set</b> the deadline for <b>5.11.2026 12:00</b>",
            "<b>set up card repeat</b> for this card for <b>Every x weeks</b>"
        );

        Assert.Multiple(() =>
        {
            var moved = (CardMovedActivity)activities[0];
            Assert.That(moved.ToColumnName, Is.EqualTo("Done"));
            Assert.That(moved.ToColumnId, Is.EqualTo(_doneColumnId));
            Assert.That(moved.FromColumnName, Is.Null);

            Assert.That(((LabelAddedActivity)activities[1]).LabelId, Is.EqualTo(_labelId));
            Assert.That(((AssigneeAddedActivity)activities[2]).AssigneeId, Is.EqualTo(_bobId));

            var joined = (AssigneeAddedActivity)activities[3];
            Assert.That(joined.AssigneeId, Is.EqualTo(_annId));
            Assert.That(joined.AssigneeName, Is.EqualTo("Ann"));

            var link = (LinkAddedActivity)activities[4];
            Assert.That(link.RelatedCardId, Is.EqualTo(_otherCardId));
            Assert.That(link.Category, Is.EqualTo("blocks"));

            var time = (TimeRecordChangedActivity)activities[5];
            Assert.That(time.OldDuration, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(time.Duration, Is.EqualTo(TimeSpan.FromMinutes(45)));

            Assert.That(((DeadlineSetActivity)activities[6]).Deadline, Is.EqualTo(new DateTime(2026, 11, 5, 12, 0, 0)));
            Assert.That(((RepeatSetActivity)activities[7]).RepeatType, Is.EqualTo(RepeatType.EveryXthWeek));
        });
    }

    [Test]
    public async Task Convert_DecodesEscapedAndRawNames()
    {
        var activities = await ConvertAsync("<b>added</b> label <b>R&amp;D</b>", "<b>removed</b> label <b>R&D</b>");

        Assert.That(activities.Cast<LabelActivity>().Select(x => x.LabelName), Is.All.EqualTo("R&D"));
    }

    [Test]
    public async Task Convert_MissingItem_KeepsNameWithoutLink()
    {
        var activities = await ConvertAsync("<b>removed</b> label <b>Deleted label</b>", "<b>was repeated</b> from card <b>OLD-9</b>");

        Assert.Multiple(() =>
        {
            Assert.That(((LabelRemovedActivity)activities[0]).LabelName, Is.EqualTo("Deleted label"));
            Assert.That(((LabelRemovedActivity)activities[0]).LabelId, Is.Null);
            Assert.That(((RepeatedFromActivity)activities[1]).RelatedCardId, Is.Null);
            Assert.That(activities.Select(x => x.LegacyText), Is.All.Null);
        });
    }

    [Test]
    public async Task Convert_UnreadableText_KeepsShowingIt()
    {
        var activities = await ConvertAsync("<b>did</b> something unknown", "<b>set</b> the deadline for <b>someday</b>");

        Assert.Multiple(() =>
        {
            Assert.That(activities[0], Is.TypeOf<GenericActivity>());
            Assert.That(activities[1], Is.TypeOf<DeadlineSetActivity>());
            Assert.That(activities.Select(x => x.LegacyText), Is.All.Not.Null);
            Assert.That(activities[1].ToHtml(), Is.EqualTo("<b>set</b> the deadline for <b>someday</b>"));
        });
    }

    [Test]
    public async Task Convert_NameThatBrokeTheMarkup_KeepsText()
    {
        // Before names were escaped, this label name ended its own bold tag and would be read as just "a".
        const string text = "<b>added</b> label <b>a</b>b</b>";

        var activity = (LabelAddedActivity)(await ConvertAsync(text)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(activity.LegacyText, Is.EqualTo(text));
            Assert.That(activity.LabelName, Is.Null);
            Assert.That(activity.ToHtml(), Is.EqualTo(text));
        });
    }

    [Test]
    public async Task Convert_RunsAgainWithoutChanges()
    {
        await ConvertAsync("<b>added</b> label <b>Bug</b>");

        using (var db = new DataContext(_options))
            await LegacyActivityConverter.ConvertAsync(db, NullLogger.Instance);

        using var check = new DataContext(_options);
        var activity = (LabelAddedActivity)await check.Activities.SingleAsync();
        Assert.That(activity.LabelName, Is.EqualTo("Bug"));
    }

    [Test]
    public async Task Convert_LogsTextsThatCouldNotBeConverted()
    {
        var logger = new ListLogger();

        await ConvertAsync(
            logger,
            "<b>added</b> label <b>Bug</b>",
            "<b>did</b> something unknown",
            "<b>did</b> something unknown",
            "<b>added</b> label <b>a</b>b</b>"
        );

        var warning = logger.Messages.Single(x => x.Level == LogLevel.Warning).Text;

        Assert.Multiple(() =>
        {
            Assert.That(warning, Does.StartWith("3 activities could not be converted"));
            Assert.That(warning, Does.Contain("[Generic] 2x <b>did</b> something unknown"));
            Assert.That(warning, Does.Contain("[LabelAdded] 1x <b>added</b> label <b>a</b>b</b>"));
            Assert.That(warning, Does.Not.Contain("Bug"));
        });
    }

    #endregion

    private class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Text)> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add((logLevel, formatter(state, exception)));
    }
}
