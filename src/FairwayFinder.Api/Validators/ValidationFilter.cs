using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class ValidationFilter<T> : IEndpointFilter where T : class
{
    private readonly IValidator<T> _validator;

    public ValidationFilter(IValidator<T> validator)
    {
        _validator = validator;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
            return await next(context);

        var result = await _validator.ValidateAsync(argument);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);

        return await next(context);
    }
}
