using System.Diagnostics.CodeAnalysis;
using Amazon.SQS;
using ItemMaster.Lambda;
using Xunit;

namespace ItemMaster.Integration.Tests;

[ExcludeFromCodeCoverage]
[Collection("Integration Tests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected Function Function { get; private set; } = null!;
    protected IAmazonSQS SqsClient { get; private set; } = null!;
    protected string? TestQueueUrl { get; private set; }
    protected LocalStackHelper LocalStack { get; private set; } = null!;
    protected TestLambdaContext LambdaContext { get; private set; } = new();

    public async Task InitializeAsync()
    {
        LocalStack = new LocalStackHelper();
        SqsClient = LocalStack.CreateSqsClient();

        TestQueueUrl = await LocalStack.CreateTestQueueAsync(SqsClient, "itemmaster-test-queue");

        Function = new Function();
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrEmpty(TestQueueUrl)) await LocalStack.PurgeQueueAsync(SqsClient, TestQueueUrl);

        SqsClient?.Dispose();
        await Task.CompletedTask;
    }
}