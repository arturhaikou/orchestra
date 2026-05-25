using System.ComponentModel;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Tools.Attributes;

namespace Orchestra.Infrastructure.Tools.Services;

[ToolCategory("Agent Questions", ProviderType.INTERNAL, "Ask the user questions during agent execution")]
public interface IAgentQuestionsToolService
{
    [ToolAction("ask_questions", "Ask the user one or more questions and wait for answers", DangerLevel.Safe)]
    [Description(
        "Ask the user one or more questions before continuing. Pass all questions in a single call. " +
        "Use type='Text' for free-form answers, 'Radio' for single-choice, 'Checkbox' for multi-choice. " +
        "Set allowCustom=true when the user might need an option not in the list. " +
        "After calling this tool, output the returned string verbatim and stop.")]
    Task<object> AskQuestionsAsync(
        [Description(
            "Array of questions to ask the user. Each entry: " +
            "'question' (required string), 'hint' (optional string), " +
            "'type' ('Text'|'Radio'|'Checkbox'), " +
            "'options' (string array, required for Radio/Checkbox), " +
            "'allowCustom' (bool, adds free-text Other… option to Radio/Checkbox).")]
        string questions);
}
