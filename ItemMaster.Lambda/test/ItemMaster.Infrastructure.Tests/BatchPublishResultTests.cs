using Amazon.SQS.Model;
using FluentAssertions;
using ItemMaster.Infrastructure;
using Xunit;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class BatchPublishResultTests
{
    public static IEnumerable<object[]> GetBatchPublishResultTestData()
    {
        // Test case 1: All successful messages
        yield return new object[]
        {
            new List<SendMessageBatchRequestEntry>
            {
                new() { Id = "msg1", MessageBody = "body1" },
                new() { Id = "msg2", MessageBody = "body2" }
            },
            new List<SendMessageBatchRequestEntry>(),
            new List<BatchResultErrorEntry>(),
            false,
            null,
            "All successful"
        };

        // Test case 2: All failed messages
        yield return new object[]
        {
            new List<SendMessageBatchRequestEntry>(),
            new List<SendMessageBatchRequestEntry>
            {
                new() { Id = "msg1", MessageBody = "body1" },
                new() { Id = "msg2", MessageBody = "body2" }
            },
            new List<BatchResultErrorEntry>
            {
                new() { Id = "msg1", Code = "Error1", Message = "Failed message 1" },
                new() { Id = "msg2", Code = "Error2", Message = "Failed message 2" }
            },
            false,
            null,
            "All failed"
        };

        // Test case 3: Mixed success and failure
        yield return new object[]
        {
            new List<SendMessageBatchRequestEntry>
            {
                new() { Id = "msg1", MessageBody = "body1" }
            },
            new List<SendMessageBatchRequestEntry>
            {
                new() { Id = "msg2", MessageBody = "body2" }
            },
            new List<BatchResultErrorEntry>
            {
                new() { Id = "msg2", Code = "Error2", Message = "Failed message 2" }
            },
            false,
            null,
            "Mixed results"
        };

        // Test case 4: Circuit breaker tripped
        yield return new object[]
        {
            new List<SendMessageBatchRequestEntry>(),
            new List<SendMessageBatchRequestEntry>
            {
                new() { Id = "msg1", MessageBody = "body1" }
            },
            new List<BatchResultErrorEntry>(),
            true,
            new Exception("Circuit breaker open"),
            "Circuit breaker tripped"
        };
    }

    [Theory]
    [MemberData(nameof(GetBatchPublishResultTestData))]
    public void BatchPublishResult_WithDifferentScenarios_ShouldInitializeCorrectly(
        List<SendMessageBatchRequestEntry> successfulMessages,
        List<SendMessageBatchRequestEntry> failedMessages,
        List<BatchResultErrorEntry> sqsFailures,
        bool circuitBreakerTripped,
        Exception? exception,
        string scenario)
    {
        // Arrange & Act
        var result = new BatchPublishResult
        {
            SuccessfulMessages = successfulMessages,
            FailedMessages = failedMessages,
            SqsFailures = sqsFailures,
            CircuitBreakerTripped = circuitBreakerTripped,
            Exception = exception
        };

        // Assert
        result.SuccessfulMessages.Should().BeEquivalentTo(successfulMessages, scenario);
        result.FailedMessages.Should().BeEquivalentTo(failedMessages, scenario);
        result.SqsFailures.Should().BeEquivalentTo(sqsFailures, scenario);
        result.CircuitBreakerTripped.Should().Be(circuitBreakerTripped, scenario);
        result.Exception.Should().Be(exception, scenario);
    }

    [Fact]
    public void BatchPublishResult_DefaultConstructor_ShouldInitializeWithEmptyCollections()
    {
        // Arrange & Act
        var result = new BatchPublishResult();

        // Assert
        result.SuccessfulMessages.Should().NotBeNull().And.BeEmpty();
        result.FailedMessages.Should().NotBeNull().And.BeEmpty();
        result.SqsFailures.Should().NotBeNull().And.BeEmpty();
        result.CircuitBreakerTripped.Should().BeFalse();
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void BatchPublishResult_Properties_ShouldBeSettableAndGettable()
    {
        // Arrange
        var result = new BatchPublishResult();
        var successfulMsg = new SendMessageBatchRequestEntry { Id = "success1", MessageBody = "body1" };
        var failedMsg = new SendMessageBatchRequestEntry { Id = "failed1", MessageBody = "body2" };
        var sqsError = new BatchResultErrorEntry { Id = "failed1", Code = "TestError", Message = "Test failure" };
        var testException = new InvalidOperationException("Test exception");

        // Act
        result.SuccessfulMessages.Add(successfulMsg);
        result.FailedMessages.Add(failedMsg);
        result.SqsFailures.Add(sqsError);
        result.CircuitBreakerTripped = true;
        result.Exception = testException;

        // Assert
        result.SuccessfulMessages.Should().Contain(successfulMsg);
        result.FailedMessages.Should().Contain(failedMsg);
        result.SqsFailures.Should().Contain(sqsError);
        result.CircuitBreakerTripped.Should().BeTrue();
        result.Exception.Should().Be(testException);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(5, 0, false)]
    [InlineData(0, 3, true)]
    [InlineData(2, 1, true)]
    public void BatchPublishResult_HasFailures_ShouldReturnCorrectValue(
        int successfulCount, 
        int failedCount, 
        bool expectedHasFailures)
    {
        // Arrange
        var result = new BatchPublishResult();
        
        for (int i = 0; i < successfulCount; i++)
        {
            result.SuccessfulMessages.Add(new SendMessageBatchRequestEntry 
            { 
                Id = $"success{i}", 
                MessageBody = $"body{i}" 
            });
        }
        
        for (int i = 0; i < failedCount; i++)
        {
            result.FailedMessages.Add(new SendMessageBatchRequestEntry 
            { 
                Id = $"failed{i}", 
                MessageBody = $"body{i}" 
            });
        }

        // Act
        var hasFailures = result.FailedMessages.Any() || result.CircuitBreakerTripped || result.Exception != null;

        // Assert
        hasFailures.Should().Be(expectedHasFailures);
    }
}
