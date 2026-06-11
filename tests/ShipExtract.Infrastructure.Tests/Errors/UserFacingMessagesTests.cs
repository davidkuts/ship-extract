using FluentAssertions;
using ShipExtract.Domain.Enums;
using ShipExtract.Infrastructure.Errors;

namespace ShipExtract.Infrastructure.Tests.Errors;

/// <summary>Tests for <see cref="UserFacingMessages"/>.</summary>
public sealed class UserFacingMessagesTests
{
    [Fact]
    public void GetMessage_AllKnownCodes_ReturnNonEmpty()
    {
        foreach (var code in Enum.GetValues<ExtractionErrorCode>())
        {
            UserFacingMessages.GetMessage(code)
                .Should().NotBeNullOrWhiteSpace(
                    because: $"every error code must have a user-facing message (missing: {code})");
        }
    }

    [Fact]
    public void GetMessage_PdfReadFailure_ContainsPdf()
    {
        var msg = UserFacingMessages.GetMessage(ExtractionErrorCode.PdfReadFailure);
        msg.Should().ContainEquivalentOf("PDF");
    }
}
