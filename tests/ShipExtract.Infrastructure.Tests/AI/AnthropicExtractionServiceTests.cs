using Shouldly;
using Moq;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.AI;
using ShipExtract.Infrastructure.Settings;

namespace ShipExtract.Infrastructure.Tests.AI;

/// <summary>Unit tests for <see cref="AnthropicExtractionService"/> JSON handling.</summary>
public sealed class AnthropicExtractionServiceTests
{
    [Fact]
    public async Task InvalidJson_ReturnsFailed_WithoutThrowing()
    {
        // Arrange: mock the IAnthropicCaller to return invalid JSON on every call
        var callerMock = new Mock<IAnthropicCaller>();
        callerMock
            .Setup(c => c.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("NOT JSON AT ALL");

        var loggerMock     = new Mock<ILoggingService>();
        var credentialMock = new Mock<ICredentialService>();
        credentialMock.Setup(c => c.GetApiKey()).Returns((string?)null);

        var settings = new AnthropicSettings { Model = "claude-test", MaxTokens = 128 };

        var sut = new AnthropicExtractionService(
            callerMock.Object, settings, loggerMock.Object, credentialMock.Object);

        // Act
        var result = await sut.ExtractAsync("some document text", DocumentType.Unknown);

        // Assert
        result.Success.ShouldBeFalse();
        result.Record.ShouldBeNull();
        // The service must not throw — reaching this line confirms it
    }
}
