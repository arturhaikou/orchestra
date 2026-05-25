using Orchestra.Domain.Enums;

namespace Orchestra.Application.Agents.DTOs;

/// <summary>
/// A single question item produced by the agent and shown to the user.
/// </summary>
/// <param name="Question">The question text shown to the user.</param>
/// <param name="Hint">Optional secondary label shown below the question.</param>
/// <param name="Type">Controls which UI input is rendered.</param>
/// <param name="Options">Required for Radio and Checkbox types. Ignored for Text.</param>
/// <param name="AllowCustom">
/// When true on Radio or Checkbox, adds an implicit "Other…" option that exposes
/// a free-text field. The typed value is stored as a plain string (radio) or appended
/// to the selected array (checkbox).
/// </param>
public record QuestionItem(
    string Question,
    string? Hint,
    QuestionType Type,
    string[]? Options,
    bool AllowCustom);
