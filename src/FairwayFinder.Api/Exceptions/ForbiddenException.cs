namespace FairwayFinder.Api.Exceptions;

public sealed class ForbiddenException : HttpResponseException
{
    public ForbiddenException(string entityName, object id)
        : base(
            StatusCodes.Status403Forbidden,
            $"You do not have access to {entityName} with ID '{id}'.",
            value: new
            {
                error = "Forbidden",
                message = $"You do not have access to {entityName} with ID '{id}'.",
                id
            })
    {
    }
}
