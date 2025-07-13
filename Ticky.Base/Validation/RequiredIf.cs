namespace Ticky.Base.Validation;

public class RequiredIfAttribute : ValidationAttribute
{
    private readonly string _propertyName;
    private readonly object[] _allowedValues;

    public RequiredIfAttribute(string propertyName, params object[] allowedValues)
    {
        _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _allowedValues = allowedValues;
    }

    public override string FormatErrorMessage(string name)
    {
        var errorMessage = $"Property {name} is required.";
        return ErrorMessage ?? errorMessage;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (validationContext.MemberName is null)
            throw new Exception("What");

        var property = validationContext.ObjectType.GetProperty(_propertyName);

        if (property == null)
        {
            throw new NotSupportedException(
                $"Can't find {_propertyName} on searched type: {validationContext.ObjectType.Name}"
            );
        }

        var requiredIfTypeActualValue = property.GetValue(validationContext.ObjectInstance);

        if (requiredIfTypeActualValue == null && _allowedValues != null)
        {
            return ValidationResult.Success;
        }

        if (
            requiredIfTypeActualValue == null
            || _allowedValues!.Any(x => requiredIfTypeActualValue.Equals(x))
        )
        {
            return value == null
                ? new ValidationResult(
                    FormatErrorMessage(validationContext.DisplayName),
                    [validationContext.MemberName]
                )
                : ValidationResult.Success;
        }

        return ValidationResult.Success;
    }
}
