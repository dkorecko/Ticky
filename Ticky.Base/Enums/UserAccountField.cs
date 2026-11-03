namespace Ticky.Base.Enums;

/// <summary>The form field an account change error belongs to, so the page can show it next to that field.</summary>
public enum UserAccountField
{
    /// <summary>Not tied to one field, e.g. missing permissions.</summary>
    General,
    DisplayName,
    Email,
    CurrentPassword,
    NewPassword
}
