using System;
using Orchestra.Domain.Entities;
using Xunit;

namespace Orchestra.Infrastructure.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="TicketComment"/> domain entity — MaxLength domain validation guards.
/// Covers FR-011 BDD Scenarios 1–3.
/// </summary>
public class TicketCommentEntityTests
{
    private static readonly Guid ValidTicketId = Guid.NewGuid();
    private const string ValidContent = "This is a valid comment.";

    // ──────────────────────────────────────────────────────────────────────────
    // TicketComment.Create — Author length guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ThrowsArgumentException_WhenAuthorExceeds255Characters()
    {
        // Arrange — Scenario 1: author of 256 characters
        var oversizedAuthor = new string('A', 256);

        // Act
        var act = () => TicketComment.Create(ValidTicketId, oversizedAuthor, ValidContent);

        // Assert
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Contains("255", ex.Message);
    }

    [Fact]
    public void Create_Succeeds_WhenAuthorIsExactly255Characters()
    {
        // Arrange — Scenario 2: author at the exact boundary must not throw
        var maxAuthor = new string('A', 255);

        // Act
        var comment = TicketComment.Create(ValidTicketId, maxAuthor, ValidContent);

        // Assert
        Assert.NotNull(comment);
        Assert.Equal(maxAuthor, comment.Author);
    }

    [Fact]
    public void Create_ThrowsArgumentException_WhenAuthorIsEmpty()
    {
        // Arrange — Scenario 3: existing null/whitespace guard must remain in place
        var emptyAuthor = string.Empty;

        // Act
        var act = () => TicketComment.Create(ValidTicketId, emptyAuthor, ValidContent);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }
}
