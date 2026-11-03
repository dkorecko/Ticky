namespace Ticky.Base.DTOs;

public record UserAccountError(UserAccountField Field, string Message);

/// <summary>The outcome of an account change: either errors to show, or which parts of the account changed.</summary>
public class UserAccountResult
{
    private readonly List<UserAccountError> _errors = [];

    public IReadOnlyList<UserAccountError> Errors => _errors;

    public bool Succeeded => _errors.Count == 0;

    public bool DisplayNameChanged { get; set; }

    public bool EmailChanged { get; set; }

    /// <summary>
    /// The password was replaced, which also renews the security stamp, so the user's existing sign-ins stop
    /// working.
    /// </summary>
    public bool PasswordChanged { get; set; }

    public bool AnythingChanged => DisplayNameChanged || EmailChanged || PasswordChanged;

    /// <summary>The change needs a fresher sign-in than the current one, so the user has to sign in again.</summary>
    public bool SignInRequired { get; set; }

    public void AddError(UserAccountField field, string message)
    {
        var error = new UserAccountError(field, message);

        if (!_errors.Contains(error))
            _errors.Add(error);
    }

    public static UserAccountResult Fail(UserAccountField field, string message)
    {
        var result = new UserAccountResult();
        result.AddError(field, message);
        return result;
    }
}
