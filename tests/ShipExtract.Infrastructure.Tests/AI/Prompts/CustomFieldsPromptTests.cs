using FluentAssertions;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.AI;

namespace ShipExtract.Infrastructure.Tests.AI.Prompts;

/// <summary>Tests for the custom-fields section of <see cref="ExtractionPromptBuilder"/>.</summary>
public sealed class CustomFieldsPromptTests
{
    [Fact]
    public void BuildCustomFieldsSection_NullList_ReturnsEmpty()
    {
        var result = ExtractionPromptBuilder.BuildCustomFieldsSection(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildCustomFieldsSection_AllDisabled_ReturnsEmpty()
    {
        var fields = new List<CustomField>
        {
            new() { Name = "PO Number",      IsEnabled = false },
            new() { Name = "Invoice Number", IsEnabled = false },
        };

        var result = ExtractionPromptBuilder.BuildCustomFieldsSection(fields);

        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildCustomFieldsSection_OneEnabledField_ContainsFieldName()
    {
        var fields = new List<CustomField>
        {
            new()
            {
                Name           = "PO Number",
                ExtractionHint = "Purchase order number, often labelled PO# or PO Number",
                IsEnabled      = true
            }
        };

        var result = ExtractionPromptBuilder.BuildCustomFieldsSection(fields);

        result.Should().Contain("PO Number");
        result.Should().Contain("customFields");
    }

    [Fact]
    public void BuildCustomFieldsSection_MultipleFields_ContainsAllNames()
    {
        var fields = new List<CustomField>
        {
            new() { Name = "PO Number",      ExtractionHint = "hint1", IsEnabled = true,  SortOrder = 0 },
            new() { Name = "Invoice Number", ExtractionHint = "hint2", IsEnabled = true,  SortOrder = 1 },
            new() { Name = "Incoterms",      ExtractionHint = "hint3", IsEnabled = false, SortOrder = 2 },
        };

        var result = ExtractionPromptBuilder.BuildCustomFieldsSection(fields);

        result.Should().Contain("PO Number");
        result.Should().Contain("Invoice Number");
        result.Should().NotContain("Incoterms"); // disabled
    }
}
