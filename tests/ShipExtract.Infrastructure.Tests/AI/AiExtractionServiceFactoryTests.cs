using Shouldly;
using Moq;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.AI;
using ShipExtract.Infrastructure.Settings;

namespace ShipExtract.Infrastructure.Tests.AI;

/// <summary>Unit tests for <see cref="AiExtractionServiceFactory"/>.</summary>
public sealed class AiExtractionServiceFactoryTests
{
    private static AiExtractionServiceFactory CreateFactory(AiProvider provider)
    {
        var settings = new AppSettings { AiProvider = provider };

        // We can't easily create real Anthropic/Ollama services in unit tests,
        // so we use mock IAiExtractionService wrappers via concrete test doubles.
        var anthropic = CreateFakeAnthropicService();
        var ollama    = CreateFakeOllamaService(settings);

        return new AiExtractionServiceFactory(anthropic, ollama, settings);
    }

    private static AnthropicExtractionService CreateFakeAnthropicService()
    {
        var callerMock     = new Mock<IAnthropicCaller>();
        var loggerMock     = new Mock<ILoggingService>();
        var credMock       = new Mock<ICredentialService>();
        var anthropicSetts = new AnthropicSettings { Model = "test", MaxTokens = 100 };
        return new AnthropicExtractionService(
            callerMock.Object, anthropicSetts, loggerMock.Object, credMock.Object);
    }

    private static OllamaExtractionService CreateFakeOllamaService(AppSettings settings)
    {
        var loggerMock = new Mock<ILoggingService>();
        return new OllamaExtractionService(new System.Net.Http.HttpClient(), settings, loggerMock.Object);
    }

    [Fact]
    public void GetService_WhenAnthropicConfigured_ReturnsAnthropicService()
    {
        var factory = CreateFactory(AiProvider.Anthropic);
        var svc     = factory.GetService();
        svc.ShouldBeOfType<AnthropicExtractionService>();
    }

    [Fact]
    public void GetService_WhenOllamaConfigured_ReturnsOllamaService()
    {
        var factory = CreateFactory(AiProvider.Ollama);
        var svc     = factory.GetService();
        svc.ShouldBeOfType<OllamaExtractionService>();
    }

    [Fact]
    public void GetService_ExplicitOllamaProvider_ReturnsOllamaService()
    {
        var factory = CreateFactory(AiProvider.Anthropic); // default Anthropic
        var svc     = factory.GetService(AiProvider.Ollama);
        svc.ShouldBeOfType<OllamaExtractionService>();
    }
}
