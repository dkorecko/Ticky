namespace Ticky.Base.DTOs.Api;

/// <summary>Who the API acts as.</summary>
/// <param name="Scope">What the token may do. Requests signed in through the browser count as read and write.</param>
public record MeDTO(int Id, string DisplayName, string? Email, ApiTokenScope Scope);

/// <summary>Your work: what you're on, as the home page shows it.</summary>
/// <param name="Cards">
/// Open cards assigned to you, on boards you can still open: not in a finished column and not snoozed. Soonest
/// deadline first, then highest priority.
/// </param>
/// <param name="RunningTimers">Your timers that are still running, the longest-running first.</param>
public record WorkDTO(IReadOnlyList<CardSummaryDTO> Cards, IReadOnlyList<TimeRecordDTO> RunningTimers);

/// <summary>A project as listed.</summary>
/// <param name="IsAdmin">Whether you administer it: rename or delete it, add boards and manage its members.</param>
public record ProjectSummaryDTO(int Id, string Name, bool IsAdmin, int BoardCount);

/// <summary>A project with its boards and members.</summary>
/// <param name="IsAdmin">Whether you administer it: rename or delete it, add boards and manage its members.</param>
/// <param name="Members">The project's members, who can open all its boards.</param>
public record ProjectDetailDTO(
    int Id,
    string Name,
    bool IsAdmin,
    IReadOnlyList<BoardSummaryDTO> Boards,
    IReadOnlyList<MemberDTO> Members
);

/// <summary>Something that happened on a card.</summary>
/// <param name="Type">What kind of change it was.</param>
/// <param name="CardCode">The card's key, like <c>TKY-12</c>.</param>
/// <param name="UserId">Who did it.</param>
/// <param name="Text">What happened, in words, without the person's name, e.g. "moved the card from To do to Done".</param>
/// <param name="Details">
/// What the change recorded, by kind, e.g. <c>fromColumnId</c>, <c>toColumnId</c> and <c>toColumnFinished</c> for a
/// move. Names are copied at the time, so they stay readable after something is renamed or deleted.
/// </param>
public record ActivityDTO(
    int Id,
    ActivityType Type,
    int CardId,
    string CardCode,
    string CardName,
    int UserId,
    string UserName,
    DateTime CreatedAt,
    string Text,
    IReadOnlyDictionary<string, object?> Details
);

/// <summary>A page of activity, newest first.</summary>
/// <param name="NextBefore">
/// Pass as <c>before</c> to get the next, older page; null when there is nothing older.
/// </param>
public record ActivityPageDTO(IReadOnlyList<ActivityDTO> Items, int? NextBefore);
