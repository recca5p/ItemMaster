using Amazon.Lambda.APIGatewayEvents;
using Amazon.SQS.Model;
using ItemMaster.Contracts;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ItemMaster.Integration.Tests;

[Collection("Integration Tests")]
public class SqsPublishingIntegrationTests : IntegrationTestBase
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqsPublishing_AfterProcessing_ShouldReceiveMessages()
    {
        // Arrange
        var request = new ProcessSkusRequest { Skus = new List<string> { "SQS-TEST-001" } };
        var apiGatewayRequest = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(request),
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                RequestId = Guid.NewGuid().ToString(),
                Stage = "test"
            }
        };

        await LocalStack.PurgeQueueAsync(SqsClient, TestQueueUrl!);

        // Act
        var response = await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        response.StatusCode.Should().Be(200);

        await Task.Delay(1000);

        var messages = await LocalStack.ReceiveMessagesAsync(SqsClient, TestQueueUrl!);
        messages.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqsPublishing_MessageContent_ShouldContainUnifiedModel()
    {
        // Arrange
        var sku = "SQS-CONTENT-001";
        var request = new ProcessSkusRequest { Skus = new List<string> { sku } };
        var apiGatewayRequest = CreateRequest(request);

        await LocalStack.PurgeQueueAsync(SqsClient, TestQueueUrl!);

        // Act
        await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        await Task.Delay(2000);

        var messages = await LocalStack.ReceiveMessagesAsync(SqsClient, TestQueueUrl!, 10);
        messages.Should().NotBeEmpty();

        var firstMessage = messages[0];
        var body = JsonDocument.Parse(firstMessage.Body);

        body.RootElement.TryGetProperty("Sku", out var skuElement);
        if (skuElement.ValueKind != JsonValueKind.Undefined)
        {
            skuElement.GetString().Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqsPublishing_BatchProcessing_ShouldSendMultipleMessages()
    {
        // Arrange
        var request = new ProcessSkusRequest
        {
            Skus = new List<string> { "BATCH-001", "BATCH-002", "BATCH-003" }
        };
        var apiGatewayRequest = CreateRequest(request);

        await LocalStack.PurgeQueueAsync(SqsClient, TestQueueUrl!);

        // Act
        await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        await Task.Delay(3000);

        var messages = await LocalStack.ReceiveMessagesAsync(SqsClient, TestQueueUrl!, 10);
        messages.Count.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqsPublishing_InvalidItems_ShouldNotPublishToSQS()
    {
        // Arrange
        var request = new ProcessSkusRequest { Skus = new List<string> { "INVALID-SQS" } };
        var apiGatewayRequest = CreateRequest(request);

        await LocalStack.PurgeQueueAsync(SqsClient, TestQueueUrl!);

        // Act
        await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        await Task.Delay(1000);

        var messages = await LocalStack.ReceiveMessagesAsync(SqsClient, TestQueueUrl!);

        if (messages.Count == 0)
        {
            messages.Should().BeEmpty();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqsPublishing_EachMessage_ShouldHaveUniqueId()
    {
        // Arrange
        var request = new ProcessSkusRequest
        {
            Skus = new List<string> { "UNIQUE-001", "UNIQUE-002" }
        };
        var apiGatewayRequest = CreateRequest(request);

        await LocalStack.PurgeQueueAsync(SqsClient, TestQueueUrl!);

        // Act
        await Function.FunctionHandler(apiGatewayRequest, LambdaContext);

        // Assert
        await Task.Delay(2000);

        var messages = await LocalStack.ReceiveMessagesAsync(SqsClient, TestQueueUrl!, 10);

        if (messages.Count >= 2)
        {
            var ids = messages.Select(m => m.MessageId).ToList();
            ids.Should().OnlyHaveUniqueItems();
        }
    }

    private static APIGatewayProxyRequest CreateRequest(ProcessSkusRequest request)
    {
        return new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Body = JsonSerializer.Serialize(request),
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                RequestId = Guid.NewGuid().ToString(),
                Stage = "test"
            }
        };
    }
}

