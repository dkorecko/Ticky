namespace Ticky.Base.Validation;

public class IsValidTimeSpan() : ValidationAttribute
{
    private const string ERROR_MESSAGE = "This field must be in the 0h 0m 0s format.";

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var errorMessage = ErrorMessage ?? ERROR_MESSAGE;

        if (value == null)
            return ValidationResult.Success;

        if (validationContext.MemberName is null)
            throw new Exception("What");

        if (value is not string str)
            return new ValidationResult(errorMessage, [validationContext.MemberName]);

        try
        {
            str.ConvertToTimeSpan();
        }
        catch
        {
            return new ValidationResult(errorMessage, [validationContext.MemberName]);
        }

        return ValidationResult.Success;
    }
}
