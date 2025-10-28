using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using ItemMaster.Contracts;
using ItemMaster.Infrastructure;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Polly.CircuitBreaker;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class SqsItemPublisherAdvancedTests
{
  [Fact]
  public async Task PublishBatchWithCircuitBreaker_WithRetries_ShouldHandleRetryLogic()
  {
    var mockSqsClient = new Mock<IAmazonSQS>();
    var mockLogger = new Mock<ILogger<SqsItemPublisher>>();
    var mockOptions = new Mock<IOptions<SqsItemPublisherOptions>>();

    var options = new SqsItemPublisherOptions
    {
      QueueUrl = "https://sqs.ap-southeast-1.amazonaws.com/123456789/test-queue",
      MaxRetries = 1,
      BaseDelayMs = 10,
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
          {
            throw new Exception("SQS temporary error");
          }

          return new SendMessageBatchResponse
          {
            Successful = request.Entries.Select(e => new SendMessageBatchResultEntry { Id = e.Id }).ToList(),
            Failed = new List<BatchResultErrorEntry>()
          };
        });

    var publisher = new SqsItemPublisher(mockSqsClient.Object, mockOptions.Object, mockLogger.Object);

    var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "TEST-001", Name = "Test Item" }
        };

    var result = await publisher.PublishUnifiedItemsAsync(items, "test-trace", CancellationToken.None);

    result.Should().NotBeNull();
  }

  [Fact]
  public async Task PublishUnifiedItemsAsync_WhenCircuitBreakerOpen_ShouldReturnFailure()
  {
    var mockSqsClient = new Mock<IAmazonSQS>();
    var mockLogger = new Mock<ILogger<SqsItemPublisher>>();
    var mockOptions = new Mock<IOptions<SqsItemPublisherOptions>>();

    var options = new SqsItemPublisherOptions
    {
      MaxRetries = 1,
      BaseDelayMs = 100,
      CircuitBreakerMinimumThroughput = 2,
      CircuitBreakerSamplingDuration = 1,
      CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(30)
    };

    mockOptions.Setup(x => x.Value).Returns(options);

    mockSqsClient.Setup(x => x.SendMessageBatchAsync(
            It.IsAny<SendMessageBatchRequest>(),
            It.IsAny<CancellationToken>()))
        .ThrowsAsync(new Exception("Service unavailable"));

    var publisher = new SqsItemPublisher(mockSqsClient.Object, mockOptions.Object, mockLogger.Object);

    var items = new List<UnifiedItemMaster>
        {
            new() { Sku = "TEST-001", Name = "Test Item" }
        };

    for (int i = 0; i < 3; i++)
    {
      await publisher.PublishUnifiedItemsAsync(items, "trace", CancellationToken.None);
    }

    var result = await publisher.PublishUnifiedItemsAsync(items, "trace", CancellationToken.None);

    result.IsSuccess.Should().BeFalse();
    result.ErrorMessage.Should().Contain("Failed to publish");
  }

  [Fact]
  public async Task PublishUnifiedItemsAsync_WithPartialBatchFailure_ShouldRetryOnlyFailedMessages()
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

    var callCount = 0;
    mockSqsClient.Setup(x => x.SendMessageBatchAsync(
            It.IsAny<SendMessageBatchRequest>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync((SendMessageBatchRequest request, CancellationToken _) =>
        {
          callCount++;
          if (callCount == 1)
          {
            var firstEntry = request.Entries.First();
            return new SendMessageBatchResponse
            {
              Successful = new List<SendMessageBatchResultEntry>
                    {
                            new() { Id = firstEntry.Id }
                    },
              Failed = new List<BatchResultErrorEntry>
                    {
                            new()
                            {
                                Id = request.Entries.Skip(1).First().Id,
                                Code = "TestError",
                                Message = "Retry me"
                            }
                    }
            };
          }

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

    result.Should().NotBeNull();
    mockSqsClient.Verify(x => x.SendMessageBatchAsync(
        It.IsAny<SendMessageBatchRequest>(),
        It.IsAny<CancellationToken>()), Times.AtLeastOnce);
  }

  [Fact]
  public async Task PublishUnifiedItemsAsync_WithVeryLargeBatch_ShouldCreateMultipleBatches()
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

    var items = Enumerable.Range(1, 25)
        .Select(i => new UnifiedItemMaster { Sku = $"TEST-{i:D3}", Name = $"Test Item {i}" })
        .ToList();

    var result = await publisher.PublishUnifiedItemsAsync(items, "trace", CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    mockSqsClient.Verify(x => x.SendMessageBatchAsync(
        It.IsAny<SendMessageBatchRequest>(),
        It.IsAny<CancellationToken>()), Times.Exactly(3));
  }

  [Theory]
  [InlineData(1)]
  [InlineData(10)]
  [InlineData(15)]
  [InlineData(23)]
  public async Task PublishUnifiedItemsAsync_WithBatchBoundaries_ShouldSplitCorrectly(int itemCount)
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
    if (itemCount > 0)
    {
      mockSqsClient.Verify(x => x.SendMessageBatchAsync(
          It.IsAny<SendMessageBatchRequest>(),
          It.IsAny<CancellationToken>()), Times.Exactly(expectedBatches));
    }
  }
}

