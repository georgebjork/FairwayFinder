using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace FairwayFinder.Web.Validation;

public class RequiredIfAttribute : ValidationAttribute
{
    private readonly string _dependentProperty;
    private readonly object _targetValue;

    public RequiredIfAttribute(string dependentProperty, object targetValue)
    {
        _dependentProperty = dependentProperty;
        _targetValue = targetValue;
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        // Get the dependent property's value
        PropertyInfo dependentPropertyInfo = validationContext.ObjectType.GetProperty(_dependentProperty);
        if (dependentPropertyInfo == null)
        {
            return new ValidationResult($"Unknown property: {_dependentProperty}");
        }
        object dependentValue = dependentPropertyInfo.GetValue(validationContext.ObjectInstance, null);

        // Check if the dependent value matches the target value
        if (object.Equals(dependentValue, _targetValue))
        {
            // If so, the current value is required
            if (value == null)
            {
                return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} is required when {_dependentProperty} is {_targetValue}.");
            }
        }

        return ValidationResult.Success;
    }
}