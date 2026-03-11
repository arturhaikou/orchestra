using System;
using Orchestra.Application.Tickets.Services;
using Orchestra.Application.Tickets.Common;
using Orchestra.Application.Common.Interfaces;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tickets;

/// <summary>
/// Unit tests for <see cref="TicketIdParsingService"/>.
/// </summary>
public class TicketIdParsingServiceTests
{
    private readonly ITicketIdParsingService _sut = new TicketIdParsingService();

    [Fact]
    public void Parse_ValidInternalGuid_ReturnsInternalTypeAndGuid()
    {
        // Arrange
        var guid = TestConstants.TestTicketId;
        var guidString = guid.ToString();

        // Act
        var result = _sut.Parse(guidString);

        // Assert
        Assert.Equal(TicketIdType.Internal, result.Type);
        Assert.Equal(guid, result.InternalId);
        Assert.Null(result.IntegrationId);
        Assert.Null(result.ExternalTicketId);
    }

    [Fact]
    public void Parse_ValidCompositeId_ReturnsExternalTypeAndComponents()
    {
        // Arrange
        var integrationId = TestConstants.TestIntegrationId;
        var externalTicketId = "EXT-12345";
        var composite = $"{integrationId}:{externalTicketId}";

        // Act
        var result = _sut.Parse(composite);

        // Assert
        Assert.Equal(TicketIdType.External, result.Type);
        Assert.Null(result.InternalId);
        Assert.Equal(integrationId, result.IntegrationId);
        Assert.Equal(externalTicketId, result.ExternalTicketId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespace_ThrowsArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => _sut.Parse(input!));
    }

    [Fact]
    public void Parse_InvalidGuid_ThrowsArgumentException()
    {
        var invalidGuid = "not-a-guid";
        Assert.Throws<ArgumentException>(() => _sut.Parse(invalidGuid));
    }

    [Fact]
    public void Parse_InvalidCompositeFormat_ThrowsArgumentException()
    {
        var invalidComposite = "not-a-guid: "; // invalid integrationId and empty externalTicketId
        Assert.Throws<ArgumentException>(() => _sut.Parse(invalidComposite));
    }

    [Fact]
    public void IsCompositeFormat_TrueForComposite_FalseOtherwise()
    {
        var composite = $"{TestConstants.TestIntegrationId}:EXT-1";
        var guid = TestConstants.TestTicketId.ToString();
        Assert.True(_sut.IsCompositeFormat(composite));
        Assert.False(_sut.IsCompositeFormat(guid));
        Assert.False(_sut.IsCompositeFormat(null!));
        Assert.False(_sut.IsCompositeFormat("   "));
    }

    [Fact]
    public void IsGuidFormat_TrueForGuid_FalseOtherwise()
    {
        var guid = TestConstants.TestTicketId.ToString();
        var composite = $"{TestConstants.TestIntegrationId}:EXT-1";
        Assert.True(_sut.IsGuidFormat(guid));
        Assert.False(_sut.IsGuidFormat(composite));
        Assert.False(_sut.IsGuidFormat(null!));
        Assert.False(_sut.IsGuidFormat("   "));
    }
}
