namespace Ticky.Base.Enums;

/// <summary>What an API token may do on top of the user's own rights. Read only is the default.</summary>
public enum ApiTokenScope
{
    /// <summary>Only GET endpoints: boards, cards and their details.</summary>
    ReadOnly = 0,

    /// <summary>Everything the user can do through the API, including creating and changing cards.</summary>
    ReadWrite = 1
}
