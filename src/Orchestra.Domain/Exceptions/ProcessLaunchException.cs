namespace Orchestra.Domain.Exceptions;

public class ProcessLaunchException : Exception
{
    public string Command { get; }

    public ProcessLaunchException(string command)
        : base($"The process '{command}' failed to start.")
    {
        Command = command;
    }

    public ProcessLaunchException(string command, Exception innerException)
        : base($"The process '{command}' failed to start.", innerException)
    {
        Command = command;
    }
}
