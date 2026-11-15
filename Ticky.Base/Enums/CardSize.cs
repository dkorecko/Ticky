namespace Ticky.Base.Enums;

/// <summary>
/// How large columns and cards are drawn on boards, chosen by each user for their own screen.
/// Numbered by size, so sorting the values gives them smallest first; Default stays 0 as the default for users.
/// </summary>
public enum CardSize
{
    Compact = -1,
    Default = 0,
    Large = 1
}
