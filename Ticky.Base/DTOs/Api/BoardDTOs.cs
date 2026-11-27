namespace Ticky.Base.DTOs.Api;

/// <summary>A board as listed.</summary>
/// <param name="Code">What its cards' keys start with, like <c>TKY</c> in <c>TKY-12</c>.</param>
/// <param name="IsAdmin">Whether you administer the board: change it, its columns, labels and members.</param>
/// <param name="IsFavorite">Whether you marked it as a favorite; the home page lists those first.</param>
public record BoardSummaryDTO(
    int Id,
    string Name,
    string Description,
    string Code,
    int ProjectId,
    string ProjectName,
    bool IsAdmin,
    bool IsFavorite
)
{
    /// <summary>The board in the web app.</summary>
    public string Url => $"{Constants.BASE_URL}{Constants.Mappings.BOARD_PATH}/{Id}";
}

/// <summary>A board with its columns, labels and members.</summary>
/// <param name="Code">What its cards' keys start with, like <c>TKY</c> in <c>TKY-12</c>.</param>
/// <param name="IsAdmin">Whether you administer the board: change it, its columns, labels and members.</param>
/// <param name="IsFavorite">Whether you marked it as a favorite; the home page lists those first.</param>
/// <param name="DisableSortingAnimations">A board setting: drag and drop without animations.</param>
/// <param name="Columns">In board order, left to right.</param>
/// <param name="Members">Everyone who can open the board, and so can be assigned to its cards.</param>
public record BoardDetailDTO(
    int Id,
    string Name,
    string Description,
    string Code,
    int ProjectId,
    string ProjectName,
    bool IsAdmin,
    bool IsFavorite,
    bool DisableSortingAnimations,
    IReadOnlyList<ColumnDTO> Columns,
    IReadOnlyList<LabelDTO> Labels,
    IReadOnlyList<MemberDTO> Members
)
{
    /// <summary>The board in the web app.</summary>
    public string Url => $"{Constants.BASE_URL}{Constants.Mappings.BOARD_PATH}/{Id}";
}

/// <param name="Index">Its position on the board, from 0.</param>
/// <param name="Finished">Cards in this column count as done, e.g. in Your work and the burndown chart.</param>
/// <param name="MaxCards">The most cards the column may hold; 0 for no limit.</param>
/// <param name="NewCardPlacement">Whether new cards go to the top or the bottom.</param>
/// <param name="Collapsed">Shown narrow, without its cards, for everyone.</param>
/// <param name="CardCount">How many cards it holds, snoozed ones included.</param>
public record ColumnDTO(
    int Id,
    int BoardId,
    string Name,
    int Index,
    bool Finished,
    int MaxCards,
    CardPlacement NewCardPlacement,
    bool Collapsed,
    int CardCount
);

/// <param name="BackgroundColor">Like <c>#DC2626</c>.</param>
/// <param name="TextColor">Like <c>#FFFFFF</c>.</param>
public record LabelDTO(int Id, string Name, string BackgroundColor, string TextColor);

/// <summary>Where a member's access comes from.</summary>
public enum MembershipSource
{
    /// <summary>A membership of the board itself.</summary>
    Board,

    /// <summary>A membership of the board's project, which opens all its boards.</summary>
    Project
}

/// <summary>Someone who can open a board or project.</summary>
/// <param name="UserId">Their user id, e.g. to assign them to cards.</param>
/// <param name="IsAdmin">
/// Whether they administer it. On a board, a board membership decides; without one, the project membership does.
/// </param>
/// <param name="Source">Where their access comes from; the board's own membership wins.</param>
public record MemberDTO(int UserId, string DisplayName, string? Email, bool IsAdmin, MembershipSource Source);
