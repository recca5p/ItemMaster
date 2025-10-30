using FluentAssertions;
using ItemMaster.Application.Services;
using ItemMaster.Contracts;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ItemMaster.Application.Tests;

public class ProcessSkusUseCaseTests
{
    private readonly Mock<ILogger<ProcessSkusUseCase>> _mockLogger;
    private readonly Mock<ISkuProcessingOrchestrator> _mockOrchestrator;
    private readonly ProcessSkusUseCase _useCase;

    public ProcessSkusUseCaseTests()
    {
        _mockOrchestrator = new Mock<ISkuProcessingOrchestrator>();
        _mockLogger = new Mock<ILogger<ProcessSkusUseCase>>();

        _useCase = new ProcessSkusUseCase(
            _mockOrchestrator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ProcessSkusRequest { Skus = new List<string> { "TEST-001" } };
        var expectedResponse = new ProcessSkusResponse();
        _mockOrchestrator.Setup(x => x.ProcessSkusAsync(It.IsAny<ProcessSkusRequest>(), It.IsAny<RequestSource>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProcessSkusResponse>.Success(expectedResponse));

        // Act
        var result = await _useCase.ExecuteAsync(request, RequestSource.ApiGateway);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        ProcessSkusRequest? request = null;

        // Act
        var act = async () => await _useCase.ExecuteAsync(request!, RequestSource.ApiGateway);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomTraceId_ShouldUseProvidedId()
    {
        // Arrange
        var request = new ProcessSkusRequest { Skus = new List<string> { "TEST-001" } };
        var customTraceId = "custom-trace-123";
        var expectedResponse = new ProcessSkusResponse();
        _mockOrchestrator.Setup(x => x.ProcessSkusAsync(It.IsAny<ProcessSkusRequest>(), It.IsAny<RequestSource>(),
                customTraceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProcessSkusResponse>.Success(expectedResponse));

        // Act
        var result = await _useCase.ExecuteAsync(request, RequestSource.Sqs, customTraceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockOrchestrator.Verify(
            x => x.ProcessSkusAsync(request, RequestSource.Sqs, customTraceId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullTraceId_ShouldGenerateNewId()
    {
        // Arrange
        var request = new ProcessSkusRequest { Skus = new List<string> { "TEST-001" } };
        var expectedResponse = new ProcessSkusResponse();
        _mockOrchestrator.Setup(x => x.ProcessSkusAsync(It.IsAny<ProcessSkusRequest>(), It.IsAny<RequestSource>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProcessSkusResponse>.Success(expectedResponse));

        // Act
        var result = await _useCase.ExecuteAsync(request, RequestSource.ApiGateway);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockOrchestrator.Verify(
            x => x.ProcessSkusAsync(It.IsAny<ProcessSkusRequest>(), It.IsAny<RequestSource>(),
                It.Is<string>(s => !string.IsNullOrEmpty(s)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrchestratorThrows_ShouldReturnFailure()
    {
        // Arrange
        var request = new ProcessSkusRequest { Skus = new List<string> { "TEST-001" } };
        _mockOrchestrator.Setup(x => x.ProcessSkusAsync(It.IsAny<ProcessSkusRequest>(), It.IsAny<RequestSource>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Orchestrator error"));

        // Act
        var result = await _useCase.ExecuteAsync(request, RequestSource.Sqs);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Processing failed: Orchestrator error");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_ShouldPassItThrough()
    {
        // Arrange
        var request = new ProcessSkusRequest { Skus = new List<string> { "TEST-001" } };
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var expectedResponse = new ProcessSkusResponse();
        _mockOrchestrator.Setup(x => x.ProcessSkusAsync(It.IsAny<ProcessSkusRequest>(), It.IsAny<RequestSource>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProcessSkusResponse>.Success(expectedResponse));

        // Act
        var result = await _useCase.ExecuteAsync(request, RequestSource.ApiGateway, cancellationToken: cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockOrchestrator.Verify(
            x => x.ProcessSkusAsync(It.IsAny<ProcessSkusRequest>(), It.IsAny<RequestSource>(), It.IsAny<string>(),
                cts.Token), Times.Once);
    }

    [Theory]
    [InlineData(RequestSource.Unknown)]
    [InlineData(RequestSource.ApiGateway)]
    [InlineData(RequestSource.Sqs)]
    public async Task ExecuteAsync_WithDifferentRequestSources_ShouldPassCorrectSource(RequestSource source)
    {
        // Arrange
        var request = new ProcessSkusRequest { Skus = new List<string> { "TEST-001" } };
        var expectedResponse = new ProcessSkusResponse();
        _mockOrchestrator.Setup(x => x.ProcessSkusAsync(It.IsAny<ProcessSkusRequest>(), source, It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ProcessSkusResponse>.Success(expectedResponse));

        // Act
        var result = await _useCase.ExecuteAsync(request, source);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockOrchestrator.Verify(
            x => x.ProcessSkusAsync(request, source, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}