namespace Orchestra.Application.Common.Exceptions;

public class IntegrationRequiredException : Exception
{
    public IntegrationRequiredException(string message)
        : base(message)
    {
    }
}
