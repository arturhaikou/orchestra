using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Exceptions;

public class McpConnectionException : Exception
{
    public McpConnectionErrorCode ErrorCode { get; }

    public McpConnectionException(McpConnectionErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public McpConnectionException(McpConnectionErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
