using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Orchestra.Infrastructure.Tools.Models;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira;

public interface IJiraRichContentBuilder
{
    bool ContainsLocalImageRefs(string markdown);
    string StripLocalImageRefs(string markdown);
    Task<JsonElement> BuildAdfAsync(
        IJiraApiClient apiClient,
        string issueKey,
        string markdown,
        CancellationToken ct = default);
    Task<JsonElement> BuildAdfFromBlocksAsync(
        IJiraApiClient apiClient,
        string issueKey,
        IReadOnlyList<ContentBlock> blocks,
        CancellationToken ct = default);
}
