namespace Ticky.Base.DTOs.Api;

/// <summary>A card in a list: enough to find it and see where it stands. Get the card for everything else.</summary>
/// <param name="Code">The key people know it by, like <c>TKY-12</c>.</param>
/// <param name="Index">Its position in the column, from 0 at the top.</param>
/// <param name="SnoozedUntil">While set, the card is hidden from the board until then.</param>
public record CardSummaryDTO(
    int Id,
    int Number,
    string Code,
    string Name,
    int BoardId,
    string BoardName,
    int ColumnId,
    string ColumnName,
    int Index,
    CardPriority Priority,
    DateTime? Deadline,
    bool Flagged,
    DateTime? SnoozedUntil,
    IReadOnlyList<int> AssigneeIds,
    IReadOnlyList<int> LabelIds,
    int SubtaskCount,
    int SubtaskCompletedCount,
    int AttachmentCount,
    int CommentCount
)
{
    /// <summary>The card in the web app, to hand to people.</summary>
    public string Url => ApiUrls.Card(BoardId, Id);
}

/// <summary>Everything about one card.</summary>
/// <param name="Code">The key people know it by, like <c>TKY-12</c>.</param>
/// <param name="Index">Its position in the column, from 0 at the top.</param>
/// <param name="SnoozedUntil">While set, the card is hidden from the board until then.</param>
/// <param name="Repeat">If the card repeats, how; null otherwise.</param>
/// <param name="Comments">Newest first.</param>
/// <param name="Links">Related cards, possibly on other boards you can open.</param>
/// <param name="TimeRecords">Time tracked on the card by everyone; a record without an end is a running timer.</param>
public record CardDetailDTO(
    int Id,
    int Number,
    string Code,
    string Name,
    string Description,
    int BoardId,
    string BoardName,
    int ColumnId,
    string ColumnName,
    int Index,
    CardPriority Priority,
    DateTime? Deadline,
    bool Flagged,
    DateTime? SnoozedUntil,
    RepeatDTO? Repeat,
    DateTime CreatedAt,
    int CreatedById,
    IReadOnlyList<CardAssigneeDTO> Assignees,
    IReadOnlyList<LabelDTO> Labels,
    IReadOnlyList<SubtaskDTO> Subtasks,
    IReadOnlyList<CommentDTO> Comments,
    IReadOnlyList<ReminderDTO> Reminders,
    IReadOnlyList<AttachmentDTO> Attachments,
    IReadOnlyList<CardLinkDTO> Links,
    IReadOnlyList<TimeRecordDTO> TimeRecords
)
{
    /// <summary>The card in the web app, to hand to people.</summary>
    public string Url => ApiUrls.Card(BoardId, Id);
}

public record CardAssigneeDTO(int Id, string DisplayName);

/// <param name="Index">Its position in the card's list, from 0.</param>
public record SubtaskDTO(int Id, string Text, bool Completed, int Index, IReadOnlyList<int> AssigneeIds);

public record CommentDTO(int Id, string Text, DateTime CreatedAt, int CreatedById, string CreatedByName);

/// <param name="At">When the card's assignees and creator get an e-mail about it (if the instance sends e-mail).</param>
public record ReminderDTO(int Id, DateTime At);

/// <param name="Name">The file's name as uploaded.</param>
public record AttachmentDTO(int Id, int CardId, string Name, DateTime CreatedAt)
{
    /// <summary>Whether the web app shows a preview of it.</summary>
    public bool IsImage => Constants.Attachments.IMAGE_FILE_EXTENSIONS.Contains(Path.GetExtension(Name));

    /// <summary>Downloads the file. Send your token with the request, as with every other endpoint.</summary>
    public string DownloadUrl => $"{Constants.BASE_URL}{Constants.Api.BasePath}/cards/{CardId}/attachments/{Id}/content";
}

/// <summary>A link from this card to another, e.g. "is blocked by" TKY-7. The other card has the opposite link.</summary>
/// <param name="Id">The link's id, to remove it.</param>
/// <param name="Category">The relationship, read as "this card [category] the other card".</param>
public record CardLinkDTO(int Id, string Category, int CardId, string CardCode, string CardName, int BoardId);

/// <param name="EndedAt">Null while the timer is running.</param>
public record TimeRecordDTO(int Id, int CardId, int UserId, string UserName, DateTime StartedAt, DateTime? EndedAt)
{
    /// <summary>How long it ran, in whole seconds, as <c>hh:mm:ss</c>; for a running timer, until now.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(Math.Floor(((EndedAt ?? DateTime.Now) - StartedAt).TotalSeconds));
}

/// <summary>How a card repeats: on its schedule, a copy of it is created.</summary>
/// <param name="Time">The time of day copies are created, as <c>hh:mm:ss</c>.</param>
/// <param name="WeekDays">For <c>WeekDays</c>: the days, like <c>["Mon", "Thu"]</c>.</param>
/// <param name="MonthDays">For <c>MonthDayNumber</c>: the days of the month, like <c>[1, 15]</c>.</param>
/// <param name="Interval">For the <c>EveryXth…</c> types: every how many days, weeks, months or years.</param>
/// <param name="Placement">Whether copies go to the top or the bottom of their column.</param>
/// <param name="TargetColumnId">The column copies are created in; null for the board's first column.</param>
/// <param name="LastRepeat">When the last copy was made, or when repeating started.</param>
/// <param name="NextRepeat">When the next copy will be made; null if the settings are incomplete.</param>
public record RepeatDTO(
    RepeatType Type,
    TimeOnly Time,
    IReadOnlyList<string>? WeekDays,
    IReadOnlyList<int>? MonthDays,
    int? Interval,
    CardPlacement Placement,
    int? TargetColumnId,
    DateTime LastRepeat,
    DateTime? NextRepeat
);

/// <summary>Links into the web app and the API, for responses.</summary>
public static class ApiUrls
{
    public static string Card(int boardId, int cardId) =>
        $"{Constants.BASE_URL}{Constants.Mappings.BOARD_PATH}/{boardId}/{cardId}";
}
