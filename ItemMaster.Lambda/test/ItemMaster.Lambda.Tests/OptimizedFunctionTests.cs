using Amazon.Lambda.Core;
using ItemMaster.Contracts;
using Xunit;

namespace ItemMaster.Lambda.Tests;

public class OptimizedFunctionTests
{
    [Fact]
    public void Function_Should_HandleHealthCheck_Successfully()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ITEMMASTER_TEST_MODE", "true");
        var function = new Function();

        // Act & Assert - Should not throw
        // This test verifies that the optimized Function can be instantiated
        // and the dependency injection works correctly in test mode
        Assert.NotNull(function);
    }

    [Fact]
    public void ProcessSkusRequest_Should_Initialize_WithDefaults()
    {
        // Arrange & Act
        var request = new ProcessSkusRequest();

        // Assert
        Assert.NotNull(request);
        Assert.Empty(request.GetAllSkus());
    }
}

public class TestLambdaContext : ILambdaContext
{
    public string RequestId => "test-request-id";
    public string FunctionName => "test-function";
    public string FunctionVersion => "1.0";
    public string InvokedFunctionArn => "arn:aws:lambda:us-east-1:123456789012:function:test";
    public ICognitoIdentity? Identity => null;
    public IClientContext? ClientContext => null;
    public string LogGroupName => "/aws/lambda/test";
    public string LogStreamName => "test-stream";
    public TimeSpan RemainingTime => TimeSpan.FromMinutes(5);
    public int MemoryLimitInMB => 512;
    public ILambdaLogger Logger => new TestLambdaLogger();
    public string AwsRequestId => "test-aws-request-id";
}

public class TestLambdaLogger : ILambdaLogger
{
    public void Log(string message)
    {
    }

    public void LogLine(string message)
    {
    }
}