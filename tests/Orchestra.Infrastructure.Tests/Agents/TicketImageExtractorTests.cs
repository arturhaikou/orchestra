using Orchestra.Infrastructure.Agents;

namespace Orchestra.Infrastructure.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="TicketImageExtractor"/> covering markdown image syntax parsing.
/// Validates correct extraction of image paths including those with spaces, encoded characters,
/// and various markdown formats.
/// </summary>
public class TicketImageExtractorTests
{
    private readonly TicketImageExtractor _sut = new();

    [Fact]
    public void Extract_WithNullMarkdown_ReturnsEmpty()
    {
        var result = _sut.Extract(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_WithEmptyMarkdown_ReturnsEmpty()
    {
        var result = _sut.Extract(string.Empty);
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_WithWhitespaceOnlyMarkdown_ReturnsEmpty()
    {
        var result = _sut.Extract("   \n\t  ");
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_WithNoImages_ReturnsEmpty()
    {
        var markdown = "Just some text without any images.";
        var result = _sut.Extract(markdown);
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_WithSingleImagePath_ReturnsPath()
    {
        var markdown = "Here is an image: ![alt text](file:///D:/images/screenshot.png)";
        var result = _sut.Extract(markdown);
        
        Assert.Single(result);
        Assert.Equal("file:///D:/images/screenshot.png", result[0]);
    }

    [Fact]
    public void Extract_WithImagePathContainingSpaces_ReturnsFullPath()
    {
        // Bug case: file:/// paths with literal spaces must be extracted in full
        var markdown = "Screenshot: ![QA Report](file:///C:/Users/TestUser/Reports/screenshot report.png)";
        var result = _sut.Extract(markdown);

        Assert.Single(result);
        Assert.Equal("file:///C:/Users/TestUser/Reports/screenshot report.png", result[0]);
    }

    [Fact]
    public void Extract_WithImagePathContainingEncodedSpaces_ReturnsFullPath()
    {
        // Paths with %20-encoded spaces
        var markdown = "Screenshot: ![QA Report](file:///C:/Users/TestUser/Reports/screenshot%20report.png)";
        var result = _sut.Extract(markdown);

        Assert.Single(result);
        Assert.Equal("file:///C:/Users/TestUser/Reports/screenshot%20report.png", result[0]);
    }

    [Fact]
    public void Extract_WithJiraUrl_ReturnsFullUrl()
    {
        // Jira attachment URL
        var markdown = "See attachment: ![issue screenshot](https://jira.example.com/jira/secure/attachment/12345/screenshot.png)";
        var result = _sut.Extract(markdown);

        Assert.Single(result);
        Assert.Equal("https://jira.example.com/jira/secure/attachment/12345/screenshot.png", result[0]);
    }

    [Fact]
    public void Extract_WithJiraUrlAndQueryString_ReturnsFullUrlWithQuery()
    {
        // Jira URL with query parameters
        var markdown = "Image: ![test](https://jira.example.com/secure/attachment/12345/img.jpg?width=400&height=300)";
        var result = _sut.Extract(markdown);

        Assert.Single(result);
        Assert.Equal("https://jira.example.com/secure/attachment/12345/img.jpg?width=400&height=300", result[0]);
    }

    [Fact]
    public void Extract_WithMultipleImages_ReturnsAllPaths()
    {
        var markdown = @"
Here is first image: ![first](file:///D:/images/first.png)

Then a second one: ![second](file:///D:/images/second.jpg)

And a third: ![third](file:///D:/images/third.gif)
";
        var result = _sut.Extract(markdown);

        Assert.Equal(3, result.Count);
        Assert.Contains("file:///D:/images/first.png", result);
        Assert.Contains("file:///D:/images/second.jpg", result);
        Assert.Contains("file:///D:/images/third.gif", result);
    }

    [Fact]
    public void Extract_WithMultipleImagesIncludingSpaces_ReturnsAllPaths()
    {
        var markdown = @"
Screenshot 1: ![Report A](file:///C:/temp/reports/report a.png)

Screenshot 2: ![Report B](file:///C:/temp/reports/report b.png)
";
        var result = _sut.Extract(markdown);

        Assert.Equal(2, result.Count);
        Assert.Contains("file:///C:/temp/reports/report a.png", result);
        Assert.Contains("file:///C:/temp/reports/report b.png", result);
    }

    [Fact]
    public void Extract_WithDuplicateImages_ReturnsDeduplicated()
    {
        var markdown = @"
First mention: ![screenshot](file:///D:/images/screenshot.png)

Second mention of same: ![same file](file:///D:/images/screenshot.png)

Different case: ![Screenshot](file:///D:/images/screenshot.png)
";
        var result = _sut.Extract(markdown);

        // Should be deduplicated case-insensitively
        Assert.Single(result);
        Assert.Equal("file:///D:/images/screenshot.png", result[0]);
    }

    [Fact]
    public void Extract_WithImagePathContainingSpecialCharacters_ReturnsPath()
    {
        var markdown = "Image: ![chart](file:///D:/data/chart-v2_final@2x.png)";
        var result = _sut.Extract(markdown);

        Assert.Single(result);
        Assert.Equal("file:///D:/data/chart-v2_final@2x.png", result[0]);
    }

    [Fact]
    public void Extract_WithEmptyAltText_ReturnsPath()
    {
        var markdown = "Image: ![](file:///D:/images/photo.png)";
        var result = _sut.Extract(markdown);

        Assert.Single(result);
        Assert.Equal("file:///D:/images/photo.png", result[0]);
    }

    [Fact]
    public void Extract_WithMissingAltTextBrackets_DoesNotMatch()
    {
        // Invalid markdown format: missing [ ]
        var markdown = "Invalid: ![file:///D:/images/photo.png)";
        var result = _sut.Extract(markdown);

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_WithInlineCodeContainingImageSyntax_StillMatches()
    {
        // The regex matches markdown image syntax even if it appears inside backticks,
        // because the regex doesn't understand markdown formatting layers.
        // In practice, if this appears in stored markdown, it's likely intentional content.
        var markdown = "To reference an image use: `![alt](path.png)`";
        var result = _sut.Extract(markdown);

        // Our regex extracts the image path because it sees valid ![alt](source) syntax
        Assert.Single(result);
        Assert.Equal("path.png", result[0]);
    }

    [Fact]
    public void Extract_WithTrailingSpaceBeforeClosingParen_TrimsCorrectly()
    {
        // Regex should handle whitespace before closing paren
        var markdown = "Image: ![alt](file:///D:/images/photo.png   )";
        var result = _sut.Extract(markdown);

        Assert.Single(result);
        // Should trim the trailing spaces
        Assert.Equal("file:///D:/images/photo.png", result[0]);
    }

    [Fact]
    public void Extract_WithNewlineInsideMarkdownImage_StillMatches()
    {
        // Multi-line markdown image syntax is now supported by our regex.
        // The regex [^)]+? captures content across lines, then .Trim() cleans up whitespace.
        var markdown = @"Image: ![alt](
file:///D:/images/photo.png
)";
        var result = _sut.Extract(markdown);

        // The regex captures the source with newlines, then trim removes them
        Assert.Single(result);
        Assert.Equal("file:///D:/images/photo.png", result[0]);
    }

    [Fact]
    public void Extract_WithComplexRealWorldExample_ExtractsAllImages()
    {
        var markdown = @"## QA Test Report

**Test Date:** 2026-06-02

### Screenshots

**Step 1: Initial State**
![01-workspace-menu.png](file:///C:/test/artifacts/01-workspace-menu.png)

**Step 2: Delete Confirmation**
![02-delete-confirmation.png](file:///C:/test/artifacts/02-delete-confirmation.png)

**Step 3: Result**
![03-workspace-creation-page.png](file:///C:/test/artifacts/03-workspace-creation-page.png)
";
        var result = _sut.Extract(markdown);

        Assert.Equal(3, result.Count);
        Assert.Contains("file:///C:/test/artifacts/01-workspace-menu.png", result);
        Assert.Contains("file:///C:/test/artifacts/02-delete-confirmation.png", result);
        Assert.Contains("file:///C:/test/artifacts/03-workspace-creation-page.png", result);
    }
}
