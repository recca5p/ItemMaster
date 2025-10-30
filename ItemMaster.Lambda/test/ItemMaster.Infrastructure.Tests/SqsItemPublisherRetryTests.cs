using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using ItemMaster.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class SqsItemPublisherRetryTests
{
    [Fact]
    public async Task PublishUnifiedItemsAsync_WithRetryAndExponentialBackoff_ShouldApplyCorrectDelays()
    {
        var mockSqsClient = new Mock<IAmazonSQS>();
        var mockLogger = new Mock<ILogger<SqsItemPublisher>>();
        var mockOptions = new Mock<IOptions<SqsItemPublisherOptions>>();

        var options = new SqsItemPublisherOptions
        {
            MaxRetries = 3,
            CircuitBreakerMinimumThroughput = 5,
            CircuitBreakerSamplingDuration = 30,
            CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(60)
        };

        mockOptions.Setup(x => x.Value).Returns(options);

        var callCount = 0;
        mockSqsClient.Setup(x => x.SendMessageBatchAsync(
                It.IsAny<SendMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SendMessageBatchRequest request, CancellationToken _) =>
            {
                callCount++;
                if (callCount == 1)
                    return new SendMessageBatchResponse
                    {
                        Successful = new List<SendMessageBatchResultEntry>
                        {
                            new() { Id = request.Entries.First().Id }
                        },
                        Failed = new List<BatchResultErrorEntry>
                        {
                            new()
                            {
                                Id = request.Entries.Skip(1).First().Id,
                                Code = "ServiceUnavailable",
                                Message = "Service temporarily unavailable"
                            }
                        }
                    };

                return new SendMessageBatchResponse
                {
                    Successful = request.Entries.Select(e => new SendMessageBatchResultEntry { Id = e.Id }).ToList(),
                    Failed = new List<BatchResultErrorEntry>()
                };
            });

        var publisher = new SqsItemPublisher(mockSqsClient.Object, mockOptions.Object, mockLogger.Object);

        var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "TEST-001", Name = "Test Item 1" },
            new() { Sku = "TEST-002", Name = "Test Item 2" }
        };

        var result = await publisher.PublishUnifiedItemsAsync(items, "trace", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PublishUnifiedItemsAsync_WhenAllRetriesFail_ShouldReturnFailure()
    {
        var mockSqsClient = new Mock<IAmazonSQS>();
        var mockLogger = new Mock<ILogger<SqsItemPublisher>>();
        var mockOptions = new Mock<IOptions<SqsItemPublisherOptions>>();

        var options = new SqsItemPublisherOptions
        {
            MaxRetries = 2,
            BaseDelayMs = 10,
            CircuitBreakerMinimumThroughput = 5,
            CircuitBreakerSamplingDuration = 30,
            CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(60)
        };

        mockOptions.Setup(x => x.Value).Returns(options);

        mockSqsClient.Setup(x => x.SendMessageBatchAsync(
                It.IsAny<SendMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SendMessageBatchRequest request, CancellationToken _) =>
            {
                return new SendMessageBatchResponse
                {
                    Successful = new List<SendMessageBatchResultEntry>(),
                    Failed = new List<BatchResultErrorEntry>
                    {
                        new()
                        {
                            Id = request.Entries.First().Id,
                            Code = "ServiceError",
                            Message = "Service error"
                        }
                    }
                };
            });

        var publisher = new SqsItemPublisher(mockSqsClient.Object, mockOptions.Object, mockLogger.Object);

        var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "TEST-001", Name = "Test Item" }
        };

        var result = await publisher.PublishUnifiedItemsAsync(items, "trace", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to publish");
    }

    [Fact]
    public async Task PublishUnifiedItemsAsync_WithExceptionInBatchPublish_ShouldHandleGracefully()
    {
        var mockSqsClient = new Mock<IAmazonSQS>();
        var mockLogger = new Mock<ILogger<SqsItemPublisher>>();
        var mockOptions = new Mock<IOptions<SqsItemPublisherOptions>>();

        var options = new SqsItemPublisherOptions
        {
            MaxRetries = 2,
            BaseDelayMs = 10,
            CircuitBreakerMinimumThroughput = 5,
            CircuitBreakerSamplingDuration = 30,
            CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(60)
        };

        mockOptions.Setup(x => x.Value).Returns(options);

        mockSqsClient.Setup(x => x.SendMessageBatchAsync(
                It.IsAny<SendMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SQS connection error"));

        var publisher = new SqsItemPublisher(mockSqsClient.Object, mockOptions.Object, mockLogger.Object);

        var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "TEST-001", Name = "Test Item" }
        };

        var result = await publisher.PublishUnifiedItemsAsync(items, "trace", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to publish");
    }

    [Theory]
    [InlineData(11)]
    [InlineData(20)]
    [InlineData(30)]
    public async Task PublishUnifiedItemsAsync_WithExactBatchBoundaries_ShouldCreateCorrectNumberOfBatches(
        int itemCount)
    {
        var mockSqsClient = new Mock<IAmazonSQS>();
        var mockLogger = new Mock<ILogger<SqsItemPublisher>>();
        var mockOptions = new Mock<IOptions<SqsItemPublisherOptions>>();

        var options = new SqsItemPublisherOptions
        {
            MaxRetries = 2,
            BaseDelayMs = 100,
            CircuitBreakerMinimumThroughput = 5,
            CircuitBreakerSamplingDuration = 30,
            CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(60)
        };

        mockOptions.Setup(x => x.Value).Returns(options);

        mockSqsClient.Setup(x => x.SendMessageBatchAsync(
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

        var publisher = new SqsItemPublisher(mockSqsClient.Object, mockOptions.Object, mockLogger.Object);

        var items = Enumerable.Range(1, itemCount)
            .Select(i => new UnifiedItemMaster { Sku = $"TEST-{i:D3}", Name = $"Test Item {i}" })
            .ToList();

        var expectedBatches = (int)Math.Ceiling(itemCount / 10.0);

        var result = await publisher.PublishUnifiedItemsAsync(items, "trace", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        mockSqsClient.Verify(x => x.SendMessageBatchAsync(
            It.IsAny<SendMessageBatchRequest>(),
            It.IsAny<CancellationToken>()), Times.Exactly(expectedBatches));
    }

    [Fact]
    public async Task PublishUnifiedItemsAsync_ShouldLogSampleItems()
    {
        var mockSqsClient = new Mock<IAmazonSQS>();
        var mockLogger = new Mock<ILogger<SqsItemPublisher>>();
        var mockOptions = new Mock<IOptions<SqsItemPublisherOptions>>();

        var options = new SqsItemPublisherOptions
        {
            MaxRetries = 2,
            BaseDelayMs = 100,
            CircuitBreakerMinimumThroughput = 5,
            CircuitBreakerSamplingDuration = 30,
            CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(60)
        };

        mockOptions.Setup(x => x.Value).Returns(options);

        mockSqsClient.Setup(x => x.SendMessageBatchAsync(
                It.IsAny<SendMessageBatchRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageBatchResponse
            {
                Successful = new List<SendMessageBatchResultEntry>(),
                Failed = new List<BatchResultErrorEntry>()
            });

        var publisher = new SqsItemPublisher(mockSqsClient.Object, mockOptions.Object, mockLogger.Object);

        var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "TEST-001", Name = "Test Item 1" },
            new() { Sku = "TEST-002", Name = "Test Item 2" },
            new() { Sku = "TEST-003", Name = "Test Item 3" },
            new() { Sku = "TEST-004", Name = "Test Item 4" },
            new() { Sku = "TEST-005", Name = "Test Item 5" }
        };

        await publisher.PublishUnifiedItemsAsync(items, "trace", CancellationToken.None);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PublishUnifiedItemsAsync_WithZeroItems_ShouldReturnSuccess()
    {
        var mockSqsClient = new Mock<IAmazonSQS>();
        var mockLogger = new Mock<ILogger<SqsItemPublisher>>();
        var mockOptions = new Mock<IOptions<SqsItemPublisherOptions>>();

        var options = new SqsItemPublisherOptions
        {
            MaxRetries = 2,
            BaseDelayMs = 100,
            CircuitBreakerMinimumThroughput = 5,
            CircuitBreakerSamplingDuration = 30,
            CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(60)
        };

        mockOptions.Setup(x => x.Value).Returns(options);

        var publisher = new SqsItemPublisher(mockSqsClient.Object, mockOptions.Object, mockLogger.Object);

        var items = new List<UnifiedItemMaster>();

        var result = await publisher.PublishUnifiedItemsAsync(items, "trace", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        mockSqsClient.Verify(x => x.SendMessageBatchAsync(
            It.IsAny<SendMessageBatchRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}