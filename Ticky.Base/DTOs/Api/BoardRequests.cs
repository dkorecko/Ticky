namespace Ticky.Base.DTOs.Api;

public class CreateProjectRequest
{
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }
}

public class UpdateProjectRequest
{
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }
}

public class CreateBoardRequest
{
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>1–5 upper-case letters that the cards' keys start with, like <c>TKY</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string Code { get; set; }
}

/// <summary>Partial update: a missing or null field is left unchanged.</summary>
public class UpdateBoardRequest
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    /// <summary>1–5 upper-case letters. Existing cards' keys change with it.</summary>
    public string? Code { get; set; }

    public bool? DisableSortingAnimations { get; set; }
}

public class CloneBoardRequest
{
    /// <summary>A project you administer.</summary>
    public required int TargetProjectId { get; set; }

    /// <summary>The copy's code: 1–5 upper-case letters.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string Code { get; set; }
}

public class CreateColumnRequest
{
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }

    /// <summary>Cards in the column count as done.</summary>
    public bool Finished { get; set; }

    /// <summary>The most cards the column may hold; 0 (the default) for no limit.</summary>
    [Range(0, int.MaxValue)]
    public int MaxCards { get; set; }

    /// <summary>Where new cards go: <c>Bottom</c> (the default) or <c>Top</c>.</summary>
    public CardPlacement NewCardPlacement { get; set; } = CardPlacement.Bottom;
}

/// <summary>Partial update: a missing or null field is left unchanged.</summary>
public class UpdateColumnRequest
{
    public string? Name { get; set; }

    public bool? Finished { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxCards { get; set; }

    public CardPlacement? NewCardPlacement { get; set; }

    /// <summary>Show the column narrow, without its cards, for everyone.</summary>
    public bool? Collapsed { get; set; }
}

public class MoveColumnRequest
{
    /// <summary>The column to put it before, or null to put it last.</summary>
    public int? BeforeColumnId { get; set; }
}

public class CreateLabelRequest
{
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }

    /// <summary>Like <c>#DC2626</c>.</summary>
    [Required, RegularExpression(HEX_COLOR, ErrorMessage = "Use a color like #DC2626.")]
    public required string BackgroundColor { get; set; }

    /// <summary>Like <c>#FFFFFF</c>.</summary>
    [Required, RegularExpression(HEX_COLOR, ErrorMessage = "Use a color like #FFFFFF.")]
    public required string TextColor { get; set; }

    internal const string HEX_COLOR = "^#[0-9A-Fa-f]{6}$";
}

/// <summary>Partial update: a missing or null field is left unchanged.</summary>
public class UpdateLabelRequest
{
    public string? Name { get; set; }

    /// <summary>Like <c>#DC2626</c>.</summary>
    [RegularExpression(CreateLabelRequest.HEX_COLOR, ErrorMessage = "Use a color like #DC2626.")]
    public string? BackgroundColor { get; set; }

    /// <summary>Like <c>#FFFFFF</c>.</summary>
    [RegularExpression(CreateLabelRequest.HEX_COLOR, ErrorMessage = "Use a color like #FFFFFF.")]
    public string? TextColor { get; set; }
}

public class AddMemberRequest
{
    /// <summary>The e-mail address of the person's Ticky account.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string Email { get; set; }
}

public class UpdateMemberRequest
{
    public required bool IsAdmin { get; set; }
}

/// <summary>Which cards of a board to list. Every filter is optional; together they all have to match.</summary>
public class BoardCardsQuery
{
    /// <summary>Text in the card's name, key (like <c>TKY-12</c>) or description.</summary>
    public string? Q { get; set; }

    /// <summary>Only cards in this column.</summary>
    public int? ColumnId { get; set; }

    /// <summary>Cards assigned to any of these users. Repeat the parameter for several.</summary>
    public List<int>? AssigneeIds { get; set; }

    /// <summary>Also (or with no assignee ids, only) cards nobody is assigned to.</summary>
    public bool Unassigned { get; set; }

    /// <summary>Cards with any of these labels.</summary>
    public List<int>? LabelIds { get; set; }

    /// <summary>Cards with none of these labels.</summary>
    public List<int>? ExcludeLabelIds { get; set; }

    /// <summary>Leave out cards in finished columns.</summary>
    public bool ExcludeFinished { get; set; }

    /// <summary>Snoozed cards: <c>Include</c> (the default), <c>Exclude</c> as the board does, or <c>Only</c>.</summary>
    public SnoozedFilter Snoozed { get; set; } = SnoozedFilter.Include;
}

public enum SnoozedFilter
{
    Include,
    Exclude,
    Only
}
