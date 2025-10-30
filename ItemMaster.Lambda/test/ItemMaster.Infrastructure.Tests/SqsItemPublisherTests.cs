using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using ItemMaster.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class SqsItemPublisherTests
{
    private readonly Mock<ILogger<SqsItemPublisher>> _mockLogger;
    private readonly Mock<IOptions<SqsItemPublisherOptions>> _mockOptions;
    private readonly Mock<IAmazonSQS> _mockSqsClient;
    private readonly SqsItemPublisherOptions _options;
    private readonly SqsItemPublisher _publisher;

    public SqsItemPublisherTests()
    {
        _mockSqsClient = new Mock<IAmazonSQS>();
        _mockLogger = new Mock<ILogger<SqsItemPublisher>>();
        _mockOptions = new Mock<IOptions<SqsItemPublisherOptions>>();

        _options = new SqsItemPublisherOptions
        {
            QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
            MaxRetries = 3,
            BaseDelayMs = 100,
            CircuitBreakerMinimumThroughput = 5,
            CircuitBreakerSamplingDuration = 30,
            CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(60)
        };

        _mockOptions.Setup(x => x.Value).Returns(_options);
        _publisher = new SqsItemPublisher(_mockSqsClient.Object, _mockOptions.Object, _mockLogger.Object);
    }

    public static IEnumerable<object[]> GetPublishUnifiedItemsTestData()
    {
        // Test case 1: Single item success
        yield return new object[]
        {
            new List<UnifiedItemMaster>
            {
                new() { Sku = "TEST-001", Name = "Test Item 1" }
            },
            1, // expected successful count
            0, // expected failed count
            true // should succeed
        };

        // Test case 2: Multiple items success
        yield return new object[]
        {
            new List<UnifiedItemMaster>
            {
                new() { Sku = "TEST-001", Name = "Test Item 1" },
                new() { Sku = "TEST-002", Name = "Test Item 2" },
                new() { Sku = "TEST-003", Name = "Test Item 3" }
            },
            3, // expected successful count
            0, // expected failed count
            true // should succeed
        };

        // Test case 3: Large batch (more than 10 items)
        yield return new object[]
        {
            Enumerable.Range(1, 25).Select(i => new UnifiedItemMaster
            {
                Sku = $"TEST-{i:D3}",
                Name = $"Test Item {i}"
            }).ToList(),
            25, // expected successful count
            0, // expected failed count
            true // should succeed
        };

        // Test case 4: Empty list
        yield return new object[]
        {
            new List<UnifiedItemMaster>(),
            0, // expected successful count
            0, // expected failed count
            true // should succeed
        };
    }

    [Theory]
    [MemberData(nameof(GetPublishUnifiedItemsTestData))]
    public async Task PublishUnifiedItemsAsync_WithValidItems_ShouldPublishSuccessfully(
        List<UnifiedItemMaster> items,
        int expectedSuccessful,
        int expectedFailed,
        bool shouldSucceed)
    {
        // Arrange
        var traceId = "test-trace-123";
        var cancellationToken = CancellationToken.None;

        _mockSqsClient.Setup(x => x.SendMessageBatchAsync(
                It.IsAny<SendMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageBatchResponse
            {
                Successful = new List<SendMessageBatchResultEntry>(),
                Failed = new List<BatchResultErrorEntry>()
            });

        // Act
        var result = await _publisher.PublishUnifiedItemsAsync(items, traceId, cancellationToken);

        // Assert
        if (shouldSucceed)
            result.IsSuccess.Should().BeTrue();
        else
            result.IsSuccess.Should().BeFalse();

        if (items.Any())
            _mockSqsClient.Verify(x => x.SendMessageBatchAsync(
                    It.IsAny<SendMessageBatchRequest>(),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
    }

    [Fact]
    public async Task PublishUnifiedItemsAsync_WhenSqsThrowsException_ShouldReturnFailure()
    {
        // Arrange
        var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "TEST-001", Name = "Test Item" }
        };
        var traceId = "test-trace-123";
        var cancellationToken = CancellationToken.None;
        var expectedException = new Exception("SQS service unavailable");

        _mockSqsClient.Setup(x => x.SendMessageBatchAsync(
                It.IsAny<SendMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        var result = await _publisher.PublishUnifiedItemsAsync(items, traceId, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to publish 1 out of 1 unified items to SQS");
    }

    [Fact]
    public async Task PublishUnifiedItemsAsync_WithPartialFailures_ShouldReturnFailureResult()
    {
        // Arrange
        var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "TEST-001", Name = "Test Item 1" },
            new() { Sku = "TEST-002", Name = "Test Item 2" }
        };
        var traceId = "test-trace-123";
        var cancellationToken = CancellationToken.None;

        _mockSqsClient.Setup(x => x.SendMessageBatchAsync(
                It.IsAny<SendMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SendMessageBatchRequest request, CancellationToken _) =>
            {
                var firstEntryId = request.Entries.First().Id;
                var secondEntryId = request.Entries.Skip(1).First().Id;

                return new SendMessageBatchResponse
                {
                    Successful = new List<SendMessageBatchResultEntry>
                    {
                        new() { Id = firstEntryId }
                    },
                    Failed = new List<BatchResultErrorEntry>
                    {
                        new() { Id = secondEntryId, Code = "TestError", Message = "Test failure" }
                    }
                };
            });

        // Act
        var result = await _publisher.PublishUnifiedItemsAsync(items, traceId, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to publish");
    }

    [Fact]
    public async Task PublishUnifiedItemsAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "TEST-001", Name = "Test Item" }
        };
        var traceId = "test-trace-123";
        var cancellationToken = new CancellationToken(true);

        _mockSqsClient.Setup(x => x.SendMessageBatchAsync(
                It.IsAny<SendMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _publisher.PublishUnifiedItemsAsync(items, traceId, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SQS publish error");
    }

    [Fact]
    public async Task PublishUnifiedItemsAsync_WithNullTraceId_ShouldHandleGracefully()
    {
        // Arrange
        var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "TEST-001", Name = "Test Item" }
        };
        string? traceId = null;
        var cancellationToken = CancellationToken.None;

        _mockSqsClient.Setup(x => x.SendMessageBatchAsync(
                It.IsAny<SendMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageBatchResponse
            {
                Successful = new List<SendMessageBatchResultEntry>
                {
                    new() { Id = "1" }
                },
                Failed = new List<BatchResultErrorEntry>()
            });

        // Act
        var result = await _publisher.PublishUnifiedItemsAsync(items, traceId, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(25)]
    public async Task PublishUnifiedItemsAsync_WithDifferentBatchSizes_ShouldHandleCorrectly(int itemCount)
    {
        // Arrange
        var items = Enumerable.Range(1, itemCount)
            .Select(i => new UnifiedItemMaster { Sku = $"TEST-{i:D3}", Name = $"Test Item {i}" })
            .ToList();
        var traceId = "test-trace-123";
        var cancellationToken = CancellationToken.None;

        _mockSqsClient.Setup(x => x.SendMessageBatchAsync(
                It.IsAny<SendMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SendMessageBatchRequest request, CancellationToken _) =>
            {
                return new SendMessageBatchResponse
                {
                    Successful = request.Entries.Select(e => new SendMessageBatchResultEntry { Id = e.Id }).ToList(),
                    Failed = new List<BatchResultErrorEntry>()
                };
            });

        // Act
        var result = await _publisher.PublishUnifiedItemsAsync(items, traceId, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify correct number of batch calls (SQS batch size is max 10)
        var expectedBatches = (int)Math.Ceiling(itemCount / 10.0);
        if (itemCount > 0)
            _mockSqsClient.Verify(x => x.SendMessageBatchAsync(
                    It.IsAny<SendMessageBatchRequest>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(expectedBatches));
    }
}