namespace Ticky.Base.DTOs;

/// <summary>How a change went. Each caller answers in its own way: the UI with a notification, the API with a status code.</summary>
public enum OperationStatus
{
    Succeeded,

    /// <summary>The thing doesn't exist, or the user can't open it. The two look the same, so ids don't leak.</summary>
    NotFound,

    /// <summary>The user can open it but may not make this change, e.g. a member changing board settings.</summary>
    Forbidden,

    /// <summary>A value isn't valid; <see cref="IOperationResult.Field"/> names it.</summary>
    Invalid,

    /// <summary>The change clashes with the current state, e.g. a full column or an existing member.</summary>
    Conflict
}

/// <summary>What every operation result has, so callers can handle failures in one place.</summary>
public interface IOperationResult
{
    OperationStatus Status { get; }

    /// <summary>Why it failed, written for people. Null on success.</summary>
    string? Message { get; }

    /// <summary>The value the failure is about, for <see cref="OperationStatus.Invalid"/>, e.g. <c>Name</c>.</summary>
    string? Field { get; }

    bool Succeeded => Status == OperationStatus.Succeeded;
}

/// <summary>The outcome of a change that returns nothing.</summary>
public sealed record OperationResult(OperationStatus Status, string? Message = null, string? Field = null)
    : IOperationResult
{
    public static OperationResult Success { get; } = new(OperationStatus.Succeeded);

    public bool Succeeded => Status == OperationStatus.Succeeded;

    public static OperationResult NotFound(string message) => new(OperationStatus.NotFound, message);

    public static OperationResult Forbidden(string message) => new(OperationStatus.Forbidden, message);

    public static OperationResult Invalid(string field, string message) => new(OperationStatus.Invalid, message, field);

    public static OperationResult Conflict(string message) => new(OperationStatus.Conflict, message);

    /// <summary>
    /// The check every name and text gets: null if <paramref name="text"/> (already trimmed) has something in it,
    /// otherwise Invalid for <paramref name="field"/>, e.g. "The name can't be empty." for a name of spaces.
    /// </summary>
    public static OperationResult? RequireText(string text, string field, string what = "name") =>
        text.Length == 0 ? Invalid(field, $"The {what} can't be empty.") : null;
}

/// <summary>The outcome of a change that returns a value on success, e.g. what it created.</summary>
public sealed record OperationResult<T>(OperationStatus Status, T? Value = default, string? Message = null, string? Field = null)
    : IOperationResult
{
    public bool Succeeded => Status == OperationStatus.Succeeded;

    public static implicit operator OperationResult<T>(T value) => new(OperationStatus.Succeeded, value);

    /// <summary>Passes a failure on unchanged.</summary>
    public static implicit operator OperationResult<T>(OperationResult failure) =>
        failure.Succeeded
            ? throw new InvalidOperationException("A success needs a value.")
            : new(failure.Status, default, failure.Message, failure.Field);
}
