using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace FairwayFinder.Core.Validation;

public class RequiredIfAttribute : ValidationAttribute
{
    private readonly string _dependentPropertyName;
    private readonly object _requiredValue;

    public RequiredIfAttribute(string dependentPropertyName, object requiredValue)
    {
        _dependentPropertyName = dependentPropertyName;
        _requiredValue = requiredValue;
    }

    protected override ValidationResult? IsValid(object? currentValue, ValidationContext validationContext)
    {
        // Get the dependent property info
        var dependentProperty = validationContext.ObjectType.GetProperty(_dependentPropertyName);
        if (dependentProperty == null)
        {
            return new ValidationResult($"Unknown property: {_dependentPropertyName}");
        }

        // Get the display name of the dependent property
        var dependentDisplayName = dependentProperty.GetCustomAttribute<DisplayAttribute>()?.Name ?? _dependentPropertyName;

        // Get the actual value of the dependent property
        var dependentValue = dependentProperty.GetValue(validationContext.ObjectInstance);

        // If the dependent property has the required value, the current field must be set
        if (dependentValue != null && dependentValue.Equals(_requiredValue))
        {
            if (currentValue == null || (currentValue is int intValue && intValue <= 0))
            {
                return new ValidationResult($"{validationContext.DisplayName} is required when {dependentDisplayName} is {_requiredValue.ToString()?.ToLower()}.");
            }
        }

        return ValidationResult.Success;
    }
}