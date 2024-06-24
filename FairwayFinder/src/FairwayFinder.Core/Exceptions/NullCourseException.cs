namespace FairwayFinder.Core.Exceptions;

public class NullCourseException : Exception
{
    public NullCourseException()
    {
    }

    public NullCourseException(string message)
        : base(message)
    {
    }

    public NullCourseException(string message, Exception inner)
        : base(message, inner)
    {
    }
}