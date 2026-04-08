namespace FairwayFinder.Api.Exceptions;

public sealed class NotFoundException : HttpResponseException
{
    public NotFoundException(string entityName, object id)
        : base(
            StatusCodes.Status404NotFound,
            $"{entityName} with ID '{id}' was not found.",
            value: new
            {
                error = $"{entityName}NotFound",
                message = $"{entityName} with ID '{id}' was not found.",
                id
            })
    {
    }
}
