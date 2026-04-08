namespace FairwayFinder.Api.Exceptions;

public class HttpResponseException(int statusCode, string message, object? value = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public object? Value { get; } = value;
}
