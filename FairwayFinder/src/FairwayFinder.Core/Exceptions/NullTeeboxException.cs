namespace FairwayFinder.Core.Exceptions;

public class NullTeeboxException : Exception
{
    public NullTeeboxException()
    {
    }

    public NullTeeboxException(string message)
        : base(message)
    {
    }

    public NullTeeboxException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
