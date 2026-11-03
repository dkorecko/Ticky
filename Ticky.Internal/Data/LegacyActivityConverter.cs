using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Ticky.Base.Converters;

namespace Ticky.Internal.Data;

/// <summary>
/// Turns activities stored as HTML text (before activities were typed) into typed data. First each row gets its type
/// from the template its text was made from, then the values are read back out, e.g. the label name from
/// "&lt;b&gt;added&lt;/b&gt; label &lt;b&gt;Bug&lt;/b&gt;", and the text is dropped.
/// </summary>
/// <remarks>
/// Safe to run on every start: it only looks at rows that still have text. Rows it cannot read keep their text and
/// keep showing it.
/// </remarks>
public static partial class LegacyActivityConverter
{
    // Every activity text started with one fixed template; "*" stands for a value.
    private static readonly (ActivityType Type, string[] Templates)[] Templates =
    [
        (ActivityType.CardMoved, ["<b>moved</b> the card to <b>*"]),
        (ActivityType.TitleChanged, ["<b>changed</b> the title"]),
        (ActivityType.DescriptionChanged, ["<b>changed</b> the description"]),
        (ActivityType.CommentPosted, ["<b>posted</b> a comment"]),
        (ActivityType.AssigneeAdded, ["<b>joined</b> the card", "<b>added</b> the user <b>*</b> as an assignee"]),
        (ActivityType.AssigneeRemoved, ["<b>left</b> the card", "<b>removed</b> the user <b>*</b> from the assignees"]),
        (ActivityType.SubtaskAssigneeAdded, ["<b>added</b> the user <b>*</b> as a subtask assignee"]),
        (ActivityType.SubtaskAssigneeRemoved, ["<b>removed</b> the user <b>*</b> from the subtask assignees"]),
        (ActivityType.LabelAdded, ["<b>added</b> label <b>*"]),
        (ActivityType.LabelRemoved, ["<b>removed</b> label <b>*"]),
        (ActivityType.PriorityChanged, ["<b>changed</b> the priority to <b>*"]),
        (ActivityType.DeadlineSet, ["<b>set</b> the deadline for <b>*"]),
        (ActivityType.DeadlineRemoved, ["<b>deleted</b> the deadline"]),
        (ActivityType.ReminderAdded, ["<b>added</b> a reminder for <b>*"]),
        (ActivityType.ReminderRemoved, ["<b>deleted</b> the reminder at <b>*"]),
        (ActivityType.SubtaskAdded, ["<b>added</b> subtask <b>*"]),
        (ActivityType.SubtaskEdited, ["<b>edited</b> subtask <b>*"]),
        (ActivityType.SubtaskCompletionChanged, ["<b>marked</b> the subtask <b>*"]),
        (ActivityType.SubtaskDeleted, ["<b>deleted</b> subtask <b>*"]),
        (ActivityType.AttachmentUploaded, ["<b>uploaded</b> file named <b>*"]),
        (ActivityType.AttachmentDeleted, ["<b>deleted</b> file named <b>*"]),
        (ActivityType.TimeRecordAdded, ["<b>added</b> a time record for <b>*"]),
        (ActivityType.TimeRecordChanged, ["<b>changed</b> the time record from <b>*"]),
        (ActivityType.TimeRecordDeleted, ["<b>deleted</b> time record of <b>*"]),
        (ActivityType.LinkAdded, ["<b>added</b> a linked issue <b>*"]),
        (ActivityType.LinkRemoved, ["<b>deleted</b> a linked issue <b>*"]),
        (ActivityType.Snoozed, ["<b>snoozed</b> this card until <b>*"]),
        (ActivityType.Unsnoozed, ["<b>unsnoozed</b> this card"]),
        (ActivityType.RepeatSet, ["<b>set up card repeat</b> for this card for <b>*"]),
        (ActivityType.RepeatDisabled, ["<b>turned repeating off</b> for this card"]),
        (ActivityType.RepeatedFrom, ["<b>was repeated</b> from card <b>*"]),
        (ActivityType.RepeatCardCreated, ["<b>created</b> repeat card <b>*"]),
    ];

    private static readonly (ActivityType Type, Regex Pattern)[] TemplatePatterns = Templates
        .SelectMany(x => x.Templates.Select(t => (x.Type, new Regex(
            $"^{Regex.Escape(t).Replace(@"\*", ".*")}$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant))))
        .ToArray();

    /// <summary>The type of activity an old text was made from, or Generic if it matches no template.</summary>
    public static ActivityType DetectType(string text) =>
        TemplatePatterns.FirstOrDefault(x => x.Pattern.IsMatch(text)).Type;

    /// <summary>
    /// Converts old text activities to typed data. Rows that can't be converted keep their text and are retried on every
    /// start, so they convert once support for them is added. Their texts are logged so they can be looked into.
    /// </summary>
    public static async Task ConvertAsync(DataContext db, ILogger logger, int batchSize = 500)
    {
        var unconverted = await AssignTypesAsync(db, batchSize);
        var pending = db.Activities.Where(x => x.LegacyText != null && x.ActivityType != ActivityType.Generic);
        var converted = 0;

        if (await pending.AnyAsync())
        {
            var lookups = await LegacyActivityLookups.LoadAsync(db);
            var lastId = 0;

            while (true)
            {
                var batch = await pending.Where(x => x.Id > lastId).OrderBy(x => x.Id).Take(batchSize).ToListAsync();

                if (batch.Count == 0)
                    break;

                foreach (var activity in batch)
                {
                    if (TryConvert(activity, lookups))
                    {
                        converted++;
                    }
                    else
                    {
                        // Leave the row exactly as it was, so it keeps showing its text.
                        var entry = db.Entry(activity);
                        entry.CurrentValues.SetValues(entry.OriginalValues);
                        unconverted.Add((activity.ActivityType, (string)entry.OriginalValues[nameof(Activity.LegacyText)]!));
                    }
                }

                lastId = batch[^1].Id;
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }

            logger.LogInformation("Converted {Converted} activities from text to data.", converted);
        }

        if (unconverted.Count > 0)
            logger.LogWarning("{Count} activities could not be converted and keep showing their stored text:{Texts}", unconverted.Count, Describe(unconverted));
    }

    private const int MAX_LOGGED_TEXTS = 50;

    // One line per distinct text, most common first, e.g. "  [Generic] 3x <b>did</b> something".
    private static string Describe(List<(ActivityType Type, string Text)> unconverted)
    {
        var groups = unconverted
            .GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key.Text, StringComparer.Ordinal)
            .ToList();

        var lines = groups.Take(MAX_LOGGED_TEXTS).Select(x => $"{Environment.NewLine}  [{x.Key.Type}] {x.Count()}x {x.Key.Text}");
        var more = groups.Count > MAX_LOGGED_TEXTS ? $"{Environment.NewLine}  ... and {groups.Count - MAX_LOGGED_TEXTS} more distinct texts" : "";

        return string.Concat(lines) + more;
    }

    // Old rows all come in as Generic. EF can't change an entity's class, so the type is set with a bulk update and
    // the rows are loaded again as their real class afterwards. Returns the texts that match no known kind.
    private static async Task<List<(ActivityType, string)>> AssignTypesAsync(DataContext db, int batchSize)
    {
        var untyped = await db.Activities
            .Where(x => x.LegacyText != null && x.ActivityType == ActivityType.Generic)
            .Select(x => new { x.Id, x.LegacyText })
            .ToListAsync();

        var detected = untyped.Select(x => (x.Id, Text: x.LegacyText!, Type: DetectType(x.LegacyText!))).ToList();

        var byType = detected
            .Where(x => x.Type != ActivityType.Generic)
            .GroupBy(x => x.Type, x => x.Id);

        foreach (var group in byType)
        {
            foreach (var ids in group.Chunk(batchSize))
            {
                await db.Activities
                    .Where(x => ids.Contains(x.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActivityType, group.Key));
            }
        }

        return detected.Where(x => x.Type == ActivityType.Generic).Select(x => (x.Type, x.Text)).ToList();
    }

    // Only drops the text when the new data reads exactly the same. That catches names that broke the old markup,
    // e.g. an unescaped "a</b>b" that would otherwise be read as "a".
    private static bool TryConvert(Activity activity, LegacyActivityLookups lookups)
    {
        var text = activity.LegacyText!;

        if (!TryFill(activity, text, lookups))
            return false;

        activity.LegacyText = null;

        if (WebUtility.HtmlDecode(activity.ToHtml()) == WebUtility.HtmlDecode(text))
            return true;

        activity.LegacyText = text;
        return false;
    }

    /// <summary>Fills the activity's data from its old text. False if the text does not have the expected shape.</summary>
    public static bool TryFill(Activity activity, string text, LegacyActivityLookups lookups)
    {
        // The first bold part is the verb, the rest are the values, in template order.
        var parts = BoldPart().Matches(text).Select(x => WebUtility.HtmlDecode(x.Groups[1].Value)).ToList();
        var values = parts.Skip(1).ToList();
        string? Value(int index) => index < values.Count ? values[index] : null;
        var boardId = lookups.BoardOfCard(activity.CardId);

        switch (activity)
        {
            case CardMovedActivity x when Value(0) is { } to:
                x.ToColumnName = to;
                x.ToColumnId = lookups.Column(boardId, to);
                return true;

            case TitleChangedActivity or DescriptionChangedActivity or CommentPostedActivity or DeadlineRemovedActivity
                or UnsnoozedActivity or RepeatDisabledActivity:
                return true;

            case AssigneeActivity x when values.Count == 0:
                // "joined" / "left": the person changed their own assignment.
                x.AssigneeId = x.UserId;
                x.AssigneeName = lookups.UserName(x.UserId);
                return true;
            case AssigneeActivity x when Value(0) is { } name:
                x.AssigneeName = name;
                x.AssigneeId = lookups.User(name);
                return true;

            case SubtaskAssigneeActivity x when Value(0) is { } name:
                x.AssigneeName = name;
                x.AssigneeId = lookups.User(name);
                return true;

            case LabelActivity x when Value(0) is { } name:
                x.LabelName = name;
                x.LabelId = lookups.Label(boardId, name);
                return true;

            case PriorityChangedActivity x when Enum.TryParse<CardPriority>(Value(0), out var priority):
                x.Priority = priority;
                return true;

            case DeadlineSetActivity x when TryParseDate(Value(0), out var deadline):
                x.Deadline = deadline;
                return true;
            case SnoozedActivity x when TryParseDate(Value(0), out var until):
                x.Until = until;
                return true;
            case ReminderActivity x when TryParseDate(Value(0), out var at):
                x.At = at;
                return true;

            case SubtaskEditedActivity x when Value(0) is { } oldText && Value(1) is { } newText:
                x.OldSubtaskText = oldText;
                x.SubtaskText = newText;
                return true;
            case SubtaskCompletionChangedActivity x when Value(0) is { } subtask && Value(1) is "completed" or "to be done":
                x.SubtaskText = subtask;
                x.Completed = Value(1) == "completed";
                return true;
            case SubtaskAddedActivity or SubtaskDeletedActivity when Value(0) is { } subtask:
                ((SubtaskActivity)activity).SubtaskText = subtask;
                return true;

            case AttachmentActivity x when Value(0) is { } fileName:
                x.FileName = fileName;
                return true;

            case TimeRecordChangedActivity x when TryParseDuration(Value(0), out var oldDuration)
                && TryParseDuration(Value(1), out var newDuration):
                x.OldDuration = oldDuration;
                x.Duration = newDuration;
                return true;
            case TimeRecordAddedActivity or TimeRecordDeletedActivity when TryParseDuration(Value(0), out var duration):
                ((TimeRecordActivity)activity).Duration = duration;
                return true;

            case LinkActivity x when Value(0) is { } code && Value(1) is { } category:
                x.RelatedCardCode = code;
                x.RelatedCardId = lookups.Card(code);
                x.Category = category;
                return true;
            case RelatedCardActivity x when Value(0) is { } code:
                x.RelatedCardCode = code;
                x.RelatedCardId = lookups.Card(code);
                return true;

            case RepeatSetActivity x when Value(0) is { } name
                && Enum.GetValues<RepeatType>().Cast<RepeatType?>().FirstOrDefault(t => t!.Value.GetDisplayName() == name) is { } type:
                x.RepeatType = type;
                return true;

            default:
                return false;
        }
    }

    private static bool TryParseDate(string? value, out DateTime date) =>
        DateTime.TryParseExact(value, ["d.M.yyyy HH:mm", "d.M.yyyy H:mm", "d.M.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    // Durations were written like "1h 5m 0s" (or "45m" when edited).
    private static bool TryParseDuration(string? value, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        if (value is null)
            return false;

        if (value.Trim().Length == 0)
            return true;

        try
        {
            duration = value.Trim().ConvertToTimeSpan();
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or OverflowException)
        {
            return false;
        }
    }

    [GeneratedRegex("<b>(.*?)</b>", RegexOptions.Singleline)]
    private static partial Regex BoldPart();
}

/// <summary>
/// Finds the ids behind names found in old activity text. Each lookup returns null when nothing or more than one thing
/// matches; the name itself is always kept, so a missing id only loses the link.
/// </summary>
public class LegacyActivityLookups
{
    private readonly Dictionary<int, int> _boardOfCard;
    private readonly Dictionary<int, string> _userNames;
    private readonly ILookup<string, int> _usersByName;
    private readonly ILookup<(int BoardId, string Name), int> _columns;
    private readonly ILookup<(int BoardId, string Name), int> _labels;
    private readonly ILookup<string, int> _cardsByCode;

    public LegacyActivityLookups(
        IEnumerable<(int CardId, int BoardId, string BoardCode, int Number)> cards,
        IEnumerable<(int Id, string DisplayName)> users,
        IEnumerable<(int Id, int BoardId, string Name)> columns,
        IEnumerable<(int Id, int BoardId, string Name)> labels
    )
    {
        var cardList = cards.ToList();
        var userList = users.ToList();

        _boardOfCard = cardList.ToDictionary(x => x.CardId, x => x.BoardId);
        _cardsByCode = cardList.ToLookup(x => $"{x.BoardCode}-{x.Number}", x => x.CardId, StringComparer.OrdinalIgnoreCase);
        _userNames = userList.ToDictionary(x => x.Id, x => x.DisplayName);
        _usersByName = userList.ToLookup(x => x.DisplayName, x => x.Id);
        _columns = columns.ToLookup(x => (x.BoardId, x.Name), x => x.Id);
        _labels = labels.ToLookup(x => (x.BoardId, x.Name), x => x.Id);
    }

    public static async Task<LegacyActivityLookups> LoadAsync(DataContext db) =>
        new(
            (await db.Cards.Select(x => new { x.Id, x.Column.BoardId, x.Column.Board.Code, x.Number }).ToListAsync())
                .Select(x => (x.Id, x.BoardId, x.Code, x.Number)),
            (await db.Users.Select(x => new { x.Id, x.DisplayName }).ToListAsync()).Select(x => (x.Id, x.DisplayName)),
            (await db.Columns.Select(x => new { x.Id, x.BoardId, x.Name }).ToListAsync()).Select(x => (x.Id, x.BoardId, x.Name)),
            (await db.Labels.Select(x => new { x.Id, x.BoardId, x.Name }).ToListAsync()).Select(x => (x.Id, x.BoardId, x.Name))
        );

    public int? BoardOfCard(int cardId) => _boardOfCard.TryGetValue(cardId, out var boardId) ? boardId : null;

    public string? UserName(int userId) => _userNames.GetValueOrDefault(userId);

    public int? User(string displayName) => Single(_usersByName[displayName]);

    public int? Column(int? boardId, string name) => boardId is null ? null : Single(_columns[(boardId.Value, name)]);

    public int? Label(int? boardId, string name) => boardId is null ? null : Single(_labels[(boardId.Value, name)]);

    /// <summary>A card by its code, e.g. "TCK-12".</summary>
    public int? Card(string code) => Single(_cardsByCode[code]);

    private static int? Single(IEnumerable<int> matches)
    {
        var list = matches.Take(2).ToList();
        return list.Count == 1 ? list[0] : null;
    }
}
