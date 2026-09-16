namespace FairwayFinder.Api.Exceptions;

/// <summary>
/// The request is valid but conflicts with the current state of the resource — for example
/// writing a hole into a round that has already been posted.
/// </summary>
public sealed class ConflictException : HttpResponseException
{
    public ConflictException(string message)
        : base(StatusCodes.Status409Conflict, message, value: new { error = "Conflict", message })
    {
    }
}
