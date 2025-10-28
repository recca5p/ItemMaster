using FluentAssertions;
using ItemMaster.Infrastructure;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class SqsItemPublisherOptionsTests
{
    public static IEnumerable<object[]> GetSqsItemPublisherOptionsTestData()
    {
        // Test case 1: Default configuration
        yield return new object[]
        {
            new SqsItemPublisherOptions
            {
                QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
                MaxRetries = 3,
                BaseDelayMs = 100,
                CircuitBreakerMinimumThroughput = 5,
                CircuitBreakerSamplingDuration = 30,
                CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(60)
            },
            "Default configuration"
        };

        // Test case 2: High-throughput configuration
        yield return new object[]
        {
            new SqsItemPublisherOptions
            {
                QueueUrl = "https://sqs.us-west-2.amazonaws.com/987654321/high-throughput-queue",
                MaxRetries = 5,
                BaseDelayMs = 50,
                CircuitBreakerMinimumThroughput = 20,
                CircuitBreakerSamplingDuration = 60,
                CircuitBreakerDurationOfBreak = TimeSpan.FromMinutes(2)
            },
            "High-throughput configuration"
        };

        // Test case 3: Conservative configuration
        yield return new object[]
        {
            new SqsItemPublisherOptions
            {
                QueueUrl = "https://sqs.eu-west-1.amazonaws.com/555666777/conservative-queue",
                MaxRetries = 1,
                BaseDelayMs = 500,
                CircuitBreakerMinimumThroughput = 2,
                CircuitBreakerSamplingDuration = 10,
                CircuitBreakerDurationOfBreak = TimeSpan.FromMinutes(5)
            },
            "Conservative configuration"
        };
    }

    [Theory]
    [MemberData(nameof(GetSqsItemPublisherOptionsTestData))]
    public void SqsItemPublisherOptions_WithDifferentConfigurations_ShouldInitializeCorrectly(
        SqsItemPublisherOptions expectedOptions,
        string scenario)
    {
        // Arrange & Act
        var actualOptions = new SqsItemPublisherOptions
        {
            QueueUrl = expectedOptions.QueueUrl,
            MaxRetries = expectedOptions.MaxRetries,
            BaseDelayMs = expectedOptions.BaseDelayMs,
            CircuitBreakerMinimumThroughput = expectedOptions.CircuitBreakerMinimumThroughput,
            CircuitBreakerSamplingDuration = expectedOptions.CircuitBreakerSamplingDuration,
            CircuitBreakerDurationOfBreak = expectedOptions.CircuitBreakerDurationOfBreak
        };

        // Assert
        actualOptions.Should().BeEquivalentTo(expectedOptions, scenario);
    }

    [Fact]
    public void SqsItemPublisherOptions_DefaultConstructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var options = new SqsItemPublisherOptions();

        // Assert
        options.QueueUrl.Should().BeEmpty();
        options.MaxRetries.Should().Be(2);
        options.BaseDelayMs.Should().Be(1000);
        options.CircuitBreakerMinimumThroughput.Should().Be(3);
        options.CircuitBreakerSamplingDuration.Should().Be(60);
        options.CircuitBreakerDurationOfBreak.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData("https://sqs.us-east-1.amazonaws.com/123456789/queue-1")]
    [InlineData("https://sqs.us-west-2.amazonaws.com/987654321/queue-2")]
    [InlineData("https://sqs.eu-west-1.amazonaws.com/555666777/queue-3")]
    public void SqsItemPublisherOptions_QueueUrl_ShouldBeSettableAndGettable(string queueUrl)
    {
        // Arrange & Act
        var options = new SqsItemPublisherOptions
        {
            QueueUrl = queueUrl
        };

        // Assert
        options.QueueUrl.Should().Be(queueUrl);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void SqsItemPublisherOptions_MaxRetries_ShouldBeSettableAndGettable(int maxRetries)
    {
        // Arrange & Act
        var options = new SqsItemPublisherOptions
        {
            MaxRetries = maxRetries
        };

        // Assert
        options.MaxRetries.Should().Be(maxRetries);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void SqsItemPublisherOptions_BaseDelayMs_ShouldBeSettableAndGettable(int baseDelayMs)
    {
        // Arrange & Act
        var options = new SqsItemPublisherOptions
        {
            BaseDelayMs = baseDelayMs
        };

        // Assert
        options.BaseDelayMs.Should().Be(baseDelayMs);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void SqsItemPublisherOptions_CircuitBreakerMinimumThroughput_ShouldBeSettableAndGettable(
        int minimumThroughput)
    {
        // Arrange & Act
        var options = new SqsItemPublisherOptions
        {
            CircuitBreakerMinimumThroughput = minimumThroughput
        };

        // Assert
        options.CircuitBreakerMinimumThroughput.Should().Be(minimumThroughput);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void SqsItemPublisherOptions_CircuitBreakerSamplingDuration_ShouldBeSettableAndGettable(
        int samplingDuration)
    {
        // Arrange & Act
        var options = new SqsItemPublisherOptions
        {
            CircuitBreakerSamplingDuration = samplingDuration
        };

        // Assert
        options.CircuitBreakerSamplingDuration.Should().Be(samplingDuration);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(300)]
    public void SqsItemPublisherOptions_CircuitBreakerDurationOfBreak_ShouldBeSettableAndGettable(
        int durationInSeconds)
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(durationInSeconds);

        // Act
        var options = new SqsItemPublisherOptions
        {
            CircuitBreakerDurationOfBreak = duration
        };

        // Assert
        options.CircuitBreakerDurationOfBreak.Should().Be(duration);
    }

    [Fact]
    public void SqsItemPublisherOptions_AllProperties_ShouldBeSettableAndGettable()
    {
        // Arrange
        var queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue";
        var maxRetries = 3;
        var baseDelayMs = 100;
        var minimumThroughput = 5;
        var samplingDuration = 30;
        var durationOfBreak = TimeSpan.FromMinutes(1);

        // Act
        var options = new SqsItemPublisherOptions
        {
            QueueUrl = queueUrl,
            MaxRetries = maxRetries,
            BaseDelayMs = baseDelayMs,
            CircuitBreakerMinimumThroughput = minimumThroughput,
            CircuitBreakerSamplingDuration = samplingDuration,
            CircuitBreakerDurationOfBreak = durationOfBreak
        };

        // Assert
        options.QueueUrl.Should().Be(queueUrl);
        options.MaxRetries.Should().Be(maxRetries);
        options.BaseDelayMs.Should().Be(baseDelayMs);
        options.CircuitBreakerMinimumThroughput.Should().Be(minimumThroughput);
        options.CircuitBreakerSamplingDuration.Should().Be(samplingDuration);
        options.CircuitBreakerDurationOfBreak.Should().Be(durationOfBreak);
    }
}
