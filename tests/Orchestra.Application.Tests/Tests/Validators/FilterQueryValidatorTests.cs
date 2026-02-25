using Orchestra.Domain.Validators;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Validators;

public class FilterQueryValidatorTests
{
    // ===== Jira Filter Query Validation Tests =====

    [Fact]
    public void ValidateJiraFilterQuery_ValidSimpleQuery_Succeeds()
    {
        // Arrange
        var filterQuery = "project = PROJ";

        // Act & Assert (should not throw)
        FilterQueryValidator.ValidateJiraFilterQuery(filterQuery);
    }

    [Fact]
    public void ValidateJiraFilterQuery_ValidComplexQuery_Succeeds()
    {
        // Arrange
        var filterQuery = "project = WEB AND status = \"To Do\" AND type = Bug";

        // Act & Assert (should not throw)
        FilterQueryValidator.ValidateJiraFilterQuery(filterQuery);
    }

    [Fact]
    public void ValidateJiraFilterQuery_ValidWithOr_Succeeds()
    {
        // Arrange
        var filterQuery = "project = PROJ OR assignee = currentUser()";

        // Act & Assert (should not throw)
        FilterQueryValidator.ValidateJiraFilterQuery(filterQuery);
    }

    [Fact]
    public void ValidateJiraFilterQuery_ValidWithNot_Succeeds()
    {
        // Arrange
        var filterQuery = "project = PROJ AND NOT resolution = Unresolved";

        // Act & Assert (should not throw)
        FilterQueryValidator.ValidateJiraFilterQuery(filterQuery);
    }

    [Fact]
    public void ValidateJiraFilterQuery_NullQuery_ThrowsArgumentException()
    {
        // Arrange
        string filterQuery = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("required and cannot be empty", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_EmptyQuery_ThrowsArgumentException()
    {
        // Arrange
        var filterQuery = "";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("required and cannot be empty", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_WhitespaceOnlyQuery_ThrowsArgumentException()
    {
        // Arrange
        var filterQuery = "   ";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("required and cannot be empty", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_MissingProjectKeyword_ThrowsArgumentException()
    {
        // Arrange
        var filterQuery = "assignee = currentUser() AND status = Open";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'project' keyword exactly once", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_MultipleProjectKeywords_ThrowsArgumentException()
    {
        // Arrange
        var filterQuery = "project = PROJ1 AND project = PROJ2";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'project' keyword exactly once", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_ProjectInValue_FailsCorrectly()
    {
        // Arrange - "project" appears only once as a keyword (in "myproject" it's part of a word)
        var filterQuery = "myproject = PROJ";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'project' keyword exactly once", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_CaseSensitive_UppercaseProject_Fails()
    {
        // Arrange - Strict case-sensitive validation: PROJECT (uppercase) should fail
        var filterQuery = "PROJECT = PROJ";

        // Act & Assert - should fail strict case-sensitive matching
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'project' keyword exactly once", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_ProjectWithDifferentCase_Fails()
    {
        // Arrange - Strict case-sensitive validation
        var filterQuery = "PROJECT = WEB AND assignee = currentUser()";

        // Act & Assert - should fail due to uppercase PROJECT
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'project' keyword exactly once", ex.Message);
    }

    // ===== Confluence Filter Query Validation Tests =====

    [Fact]
    public void ValidateConfluenceFilterQuery_ValidSimpleQuery_Succeeds()
    {
        // Arrange
        var filterQuery = "space = ENG";

        // Act & Assert (should not throw)
        FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_ValidComplexQuery_Succeeds()
    {
        // Arrange
        var filterQuery = "space = ENG AND type = page AND text ~ production";

        // Act & Assert (should not throw)
        FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_ValidWithOr_Succeeds()
    {
        // Arrange
        var filterQuery = "space = MYSPACE OR creator = currentUser()";

        // Act & Assert (should not throw)
        FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_ValidWithNot_Succeeds()
    {
        // Arrange
        var filterQuery = "space = DOCS AND NOT status = archived";

        // Act & Assert (should not throw)
        FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_NullQuery_ThrowsArgumentException()
    {
        // Arrange
        string filterQuery = null;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("required and cannot be empty", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_EmptyQuery_ThrowsArgumentException()
    {
        // Arrange
        var filterQuery = "";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("required and cannot be empty", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_WhitespaceOnlyQuery_ThrowsArgumentException()
    {
        // Arrange
        var filterQuery = "  \t\n  ";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("required and cannot be empty", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_MissingSpaceKeyword_ThrowsArgumentException()
    {
        // Arrange
        var filterQuery = "type = page AND title ~ documentation";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("'space' keyword exactly once", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_MultipleSpaceKeywords_ThrowsArgumentException()
    {
        // Arrange
        var filterQuery = "space = SPACE1 AND space = SPACE2";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("'space' keyword exactly once", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_SpaceInValue_FailsCorrectly()
    {
        // Arrange - "space" appears only once as a keyword (in "myspace" it's part of a word)
        var filterQuery = "myspace = DOCS";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("'space' keyword exactly once", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_SpaceWithDifferentCase_Fails()
    {
        // Arrange - Strict case-sensitive validation
        var filterQuery = "SPACE = ENGINEERING AND type = page";

        // Act & Assert - should fail due to uppercase SPACE
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("'space' keyword exactly once", ex.Message);
    }

    // ===== Edge Case Tests =====

    [Fact]
    public void ValidateJiraFilterQuery_ProjectAsPartOfFieldName_FailsCorrectly()
    {
        // Arrange - "project" is part of "customproject" field name
        var filterQuery = "customproject = VALUE";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'project' keyword exactly once", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_ProjectWithLeadingWhitespace_Succeeds()
    {
        // Arrange
        var filterQuery = "  project = PROJ";

        // Act & Assert (should not throw)
        FilterQueryValidator.ValidateJiraFilterQuery(filterQuery);
    }

    [Fact]
    public void ValidateJiraFilterQuery_ProjectWithTrailingWhitespace_Succeeds()
    {
        // Arrange
        var filterQuery = "project = PROJ  ";

        // Act & Assert (should not throw)
        FilterQueryValidator.ValidateJiraFilterQuery(filterQuery);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_ProjectAsPartOfFieldName_FailsCorrectly()
    {
        // Arrange - "space" is part of "myspace" field name
        var filterQuery = "myspace.key = DOCS";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("'space' keyword exactly once", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_WithParentheses_Succeeds()
    {
        // Arrange
        var filterQuery = "(space = DOCS OR space = ENG) AND type = page";

        // Act & Assert - should fail because space appears twice
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("'space' keyword exactly once", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_WithInOperator_ThrowsArgumentException()
    {
        // Arrange - "in" operator allows multiple projects
        var filterQuery = "project in (PROJ1, PROJ2)";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'in' or 'not in' operators", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_WithSingleValueInOperator_ThrowsArgumentException()
    {
        // Arrange - even single value in "in" operator is not allowed
        var filterQuery = "project in (PROJ)";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'in' or 'not in' operators", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_WithNotInOperator_ThrowsArgumentException()
    {
        // Arrange - "not in" operator also disallowed
        var filterQuery = "project not in (PROJ1, PROJ2)";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'in' or 'not in' operators", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_WithInOperatorCaseLower_ThrowsArgumentException()
    {
        // Arrange - case-insensitive operator detection
        var filterQuery = "project in (PROJ)";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'in' or 'not in' operators", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_WithInOperatorCaseUpper_ThrowsArgumentException()
    {
        // Arrange - case-insensitive operator detection
        var filterQuery = "project IN (PROJ)";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'in' or 'not in' operators", ex.Message);
    }

    [Fact]
    public void ValidateJiraFilterQuery_WithInOperatorMixedCase_ThrowsArgumentException()
    {
        // Arrange - case-insensitive operator detection
        var filterQuery = "project In (PROJ)";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateJiraFilterQuery(filterQuery));
        Assert.Contains("'in' or 'not in' operators", ex.Message);
    }

    // ===== In/Not In Operator Tests for Confluence =====

    [Fact]
    public void ValidateConfluenceFilterQuery_WithInOperator_ThrowsArgumentException()
    {
        // Arrange - "in" operator allows multiple spaces
        var filterQuery = "space in (SPACE1, SPACE2)";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("'in' or 'not in' operators", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_WithSingleValueInOperator_ThrowsArgumentException()
    {
        // Arrange - even single value in "in" operator is not allowed
        var filterQuery = "space in (MYSPACE)";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("'in' or 'not in' operators", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_WithNotInOperator_ThrowsArgumentException()
    {
        // Arrange - "not in" operator also disallowed
        var filterQuery = "space not in (SPACE1, SPACE2)";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("'in' or 'not in' operators", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_WithInOperatorCaseUpper_ThrowsArgumentException()
    {
        // Arrange - case-insensitive operator detection
        var filterQuery = "space IN (MYSPACE)";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("'in' or 'not in' operators", ex.Message);
    }

    [Fact]
    public void ValidateConfluenceFilterQuery_WithNotInOperatorMixedCase_ThrowsArgumentException()
    {
        // Arrange - case-insensitive operator detection
        var filterQuery = "space NOT IN (MYSPACE)";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            FilterQueryValidator.ValidateConfluenceFilterQuery(filterQuery));
        Assert.Contains("'in' or 'not in' operators", ex.Message);
    }
}
