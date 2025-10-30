using Amazon.Lambda.Core;

namespace ItemMaster.Integration.Tests;

public class TestLambdaContext : ILambdaContext
{
    public int RemainingTimeInMillis { get; } = 60000;
    public string AwsRequestId { get; } = "test-request-id-" + Guid.NewGuid();
    public string FunctionName { get; } = "test-function";
    public string FunctionVersion { get; } = "$LATEST";
    public int MemoryLimitInMB { get; } = 512;
    public ILambdaLogger Logger { get; } = new TestLambdaLogger();
    public string InvokedFunctionArn { get; } = "arn:aws:lambda:ap-southeast-1:123456789012:function:test-function";
    public string LogGroupName { get; } = "/aws/lambda/test-function";
    public string LogStreamName { get; } = "2024/01/01/[LATEST]test-stream";
    public ICognitoIdentity Identity { get; } = null!;
    public IClientContext ClientContext { get; } = null!;

    public TimeSpan RemainingTime => TimeSpan.FromMilliseconds(RemainingTimeInMillis);
}

public class TestLambdaLogger : ILambdaLogger
{
    public void Log(string message)
    {
        Console.WriteLine($"[TEST-LOG] {message}");
    }

    public void LogLine(string message)
    {
        Console.WriteLine($"[TEST-LOG] {message}");
    }

    public void Log(string format, params object[] args)
    {
        Console.WriteLine($"[TEST-LOG] {string.Format(format, args)}");
    }
}