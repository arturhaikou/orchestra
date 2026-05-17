using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Agents.Templates;

public class BuiltInAgentTemplateRegistry : IBuiltInAgentTemplateRegistry
{
    private readonly Dictionary<string, BuiltInAgentTemplate> _templates = new();

    public BuiltInAgentTemplateRegistry()
    {
        Register(CreateCodeReviewTemplate());
        Register(CreateAgenticSearchTemplate());
        Register(CreateCodingAgentTemplate());
    }

    public IReadOnlyList<BuiltInAgentTemplate> GetAll()
    {
        return _templates.Values.ToList().AsReadOnly();
    }

    public BuiltInAgentTemplate? GetByIdentifier(string identifier)
    {
        _templates.TryGetValue(identifier, out var template);
        return template;
    }

    private void Register(BuiltInAgentTemplate template)
    {
        if (!_templates.TryAdd(template.Identifier, template))
            throw new InvalidOperationException($"Duplicate template identifier: {template.Identifier}");
    }

    private static BuiltInAgentTemplate CreateCodeReviewTemplate()
    {
        return new BuiltInAgentTemplate(
            Identifier: "code-review",
            Version: 1,
            DisplayName: "Code Review Agent",
            Role: "Automated code reviewer",
            Capabilities: new[] { "Code Review" },
            RequiredIntegrationType: IntegrationType.CODE_SOURCE,
            ToolActionMethodNames: new[] { "review_pull_request", "review_merge_request" },
            LockedFields: new HashSet<string> { "name", "role", "capabilities", "tools" },
            EditableFields: new[] { "projectPrinciples" },
            GuideTemplate: "Create a ticket and provide a {providerLabel} link. The agent will automatically review the code changes based on your project principles.",
            ProviderLabelMap: new Dictionary<ProviderType, string>
            {
                { ProviderType.GITHUB, "Pull Request" },
                { ProviderType.GITLAB, "Merge Request" }
            },
            ProviderToolMethodMap: new Dictionary<ProviderType, string>
            {
                { ProviderType.GITHUB, "review_pull_request" },
                { ProviderType.GITLAB, "review_merge_request" }
            });
    }

    private static BuiltInAgentTemplate CreateAgenticSearchTemplate()
    {
        return new BuiltInAgentTemplate(
            Identifier: "agentic-search",
            Version: 1,
            DisplayName: "Agentic Search Agent",
            Role: "Codebase search and exploration specialist",
            Capabilities: new[] { "Codebase Search", "Exploration", "Summarization" },
            RequiredIntegrationType: null,
            ToolActionMethodNames: Array.Empty<string>(),
            LockedFields: new HashSet<string> { "name", "role", "capabilities", "tools" },
            EditableFields: Array.Empty<string>(),
            GuideTemplate: "Assign a ticket describing what to search for in the codebase. The agent will explore the code and return a comprehensive summary.",
            ProviderLabelMap: null,
            ProviderToolMethodMap: null,
            IsCliAgent: true,
            DefaultCustomInstructions:
                """
                You are an expert codebase exploration agent with read-only access to the repository.
                Your mission is to thoroughly search the codebase and provide comprehensive, well-structured summaries.

                Guidelines:
                - Use read tools (read_file, list_dir, search_files, grep) to explore the repository.
                - Never write, edit, create, or delete any files.
                - Never execute shell commands beyond what is needed for searching.
                - Start by understanding the high-level structure, then drill into relevant areas.
                - Cite exact file paths and line numbers when referencing code.
                - Synthesize findings into a clear, concise, and actionable summary.
                - If the topic spans multiple modules, describe relationships and data flow.
                """);
    }

    private static BuiltInAgentTemplate CreateCodingAgentTemplate()
    {
        return new BuiltInAgentTemplate(
            Identifier: "coding",
            Version: 1,
            DisplayName: "Coding Agent",
            Role: "Expert software engineer and implementation specialist",
            Capabilities: new[] { "Code Generation", "Code Editing", "Refactoring", "Bug Fixing", "Test Writing" },
            RequiredIntegrationType: null,
            ToolActionMethodNames: Array.Empty<string>(),
            LockedFields: new HashSet<string> { "name", "role", "capabilities", "tools" },
            EditableFields: Array.Empty<string>(),
            GuideTemplate: "Assign a ticket describing the coding task. The agent will implement, edit, test, and verify changes directly in the codebase.",
            ProviderLabelMap: null,
            ProviderToolMethodMap: null,
            IsCliAgent: true,
            IsReadOnlyCli: false,
            DefaultCustomInstructions:
                """
                You are an elite software engineer with full read/write access to the codebase.
                Your mission is to implement, modify, and maintain code to the highest professional standards.

                Capabilities available to you:
                - Read, create, edit, and delete files
                - Execute shell commands (build, test, lint, format, git operations)
                - Navigate and understand the entire repository structure
                - Run and verify tests before completing any task

                Working principles:
                - Always explore the codebase first to understand existing patterns, conventions, and architecture before making changes.
                - Follow the project's existing naming conventions, folder structure, and coding style.
                - Write clean, self-documenting code — prefer meaningful names over inline comments.
                - Keep functions small and focused (single responsibility).
                - Make atomic, minimal-scope changes; do not introduce unrelated modifications.
                - Always verify your changes compile and tests pass before reporting completion.
                - If requirements are ambiguous, state your assumptions clearly before proceeding.

                Process for every task:
                1. Explore relevant files and understand the existing implementation.
                2. Plan the required changes at a high level.
                3. Implement step-by-step, verifying each change.
                4. Run build and tests to confirm correctness.
                5. Provide a concise summary of what was changed and why.
                """);
    }
}
