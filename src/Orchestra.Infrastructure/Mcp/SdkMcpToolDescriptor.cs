using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Mcp;

internal sealed class SdkMcpToolDescriptor : IMcpToolDescriptor
{
    private readonly McpClientTool _tool;

    public SdkMcpToolDescriptor(McpClientTool tool)
    {
        _tool = tool;
    }

    public string Name => _tool.Name;

    public string? Description => _tool.Description;

    public AIFunction AsAIFunction() => _tool;
}
