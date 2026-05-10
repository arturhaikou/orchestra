using Microsoft.Extensions.AI;

namespace Orchestra.Application.Common.Interfaces;

public interface IMcpToolDescriptor
{
    string Name { get; }
    string? Description { get; }
    AIFunction AsAIFunction();
}
