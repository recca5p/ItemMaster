using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using ItemMaster.Contracts;
using Xunit;

namespace ItemMaster.Lambda.Tests;

public class LambdaResponse
{
    public bool success { get; set; }
    public ProcessSkusResponse? data { get; set; }
    public string? traceId { get; set; }
}

public class FunctionTest
{
    static FunctionTest()
    {
        Environment.SetEnvironmentVariable("ITEMMASTER_TEST_MODE", "true");
    }

    [Fact]
    public async Task ProcessSkus_WithSpecificSkus_ReturnsCorrectCounts()
    {
        var function = new Function();
        var resp = await InvokeAsync(function,
            new ProcessSkusRequest { Skus = new List<string> { "SKU1", "SKU2", "SKU3" } });
        Assert.Equal(200, resp.StatusCode);

        var lambdaResponse = JsonSerializer.Deserialize<LambdaResponse>(resp.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(lambdaResponse);
        Assert.True(lambdaResponse!.success);
        Assert.NotNull(lambdaResponse.data);

        var parsed = lambdaResponse.data!;
        Assert.True(parsed.Success);
        Assert.Equal(3, parsed.ItemsProcessed); // We requested 3 SKUs, should get 3 items back from InMemory repo
        Assert.Equal(3, parsed.ItemsPublished);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public async Task EmptyBody_FetchesLatestItems()
    {
        var function = new Function();
        var context = new TestLambdaContext();
        var request = new APIGatewayProxyRequest { Body = string.Empty };
        var response = await function.FunctionHandler(request, context);
        Assert.Equal(200, response.StatusCode);

        var lambdaResponse = JsonSerializer.Deserialize<LambdaResponse>(response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(lambdaResponse);
        Assert.True(lambdaResponse!.success);
        Assert.NotNull(lambdaResponse.data);

        var parsed = lambdaResponse.data!;
        Assert.True(parsed.Success);
        // Empty body should fetch latest items (up to 20 from InMemorySnowflakeRepository)
        Assert.True(parsed.ItemsProcessed > 0, "Should fetch latest items when no SKUs provided");
        Assert.Equal(parsed.ItemsProcessed, parsed.ItemsPublished);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public async Task Base64EncodedBody_ParsesAndProcesses()
    {
        var function = new Function();
        var resp = await InvokeAsync(function, new ProcessSkusRequest { Skus = new List<string> { "B1", "B2" } }, true);
        Assert.Equal(200, resp.StatusCode);

        var lambdaResponse = JsonSerializer.Deserialize<LambdaResponse>(resp.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(lambdaResponse);
        Assert.True(lambdaResponse!.success);
        Assert.NotNull(lambdaResponse.data);

        var parsed = lambdaResponse.data!;
        Assert.True(parsed.Success);
        Assert.Equal(2, parsed.ItemsProcessed);
        Assert.Equal(2, parsed.ItemsPublished);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public async Task InvalidJson_FetchesLatestItems_InTestMode()
    {
        var function = new Function();
        var ctx = new TestLambdaContext();
        var request = new APIGatewayProxyRequest { Body = "{not-json" };
        var response = await function.FunctionHandler(request, ctx);
        Assert.Equal(200, response.StatusCode);

        var lambdaResponse = JsonSerializer.Deserialize<LambdaResponse>(response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(lambdaResponse);
        Assert.True(lambdaResponse!.success);
        Assert.NotNull(lambdaResponse.data);

        var parsed = lambdaResponse.data!;
        Assert.True(parsed.Success);
        // In test mode, invalid JSON creates empty request which fetches latest items
        Assert.True(parsed.ItemsProcessed > 0, "Should fetch latest items when JSON is invalid in test mode");
        Assert.Equal(parsed.ItemsProcessed, parsed.ItemsPublished);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public async Task ProcessSkus_WithSkusString_CommaDelimited()
    {
        var function = new Function();
        var request = new ProcessSkusRequest { SkusString = "SKU-A,SKU-B,SKU-C" };
        var resp = await InvokeAsync(function, request);
        Assert.Equal(200, resp.StatusCode);

        var lambdaResponse = JsonSerializer.Deserialize<LambdaResponse>(resp.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(lambdaResponse);
        Assert.True(lambdaResponse!.success);
        Assert.NotNull(lambdaResponse.data);

        var parsed = lambdaResponse.data!;
        Assert.True(parsed.Success);
        Assert.Equal(3, parsed.ItemsProcessed);
        Assert.Equal(3, parsed.ItemsPublished);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public async Task ProcessSkus_WithMixedSkuInputs_DeduplicatesCorrectly()
    {
        var function = new Function();
        var request = new ProcessSkusRequest
        {
            Skus = new List<string> { "X1", "X2" },
            SkusString = "X2,X3" // X2 is duplicate, should be deduplicated
        };
        var resp = await InvokeAsync(function, request);
        Assert.Equal(200, resp.StatusCode);

        var lambdaResponse = JsonSerializer.Deserialize<LambdaResponse>(resp.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(lambdaResponse);
        Assert.True(lambdaResponse!.success);
        Assert.NotNull(lambdaResponse.data);

        var parsed = lambdaResponse.data!;
        Assert.True(parsed.Success);
        Assert.Equal(3, parsed.ItemsProcessed); // Should be 3 unique SKUs: X1, X2, X3
        Assert.Equal(3, parsed.ItemsPublished);
        Assert.Equal(0, parsed.Failed);
    }

    private async Task<APIGatewayProxyResponse> InvokeAsync(Function function, ProcessSkusRequest request,
        bool base64 = false)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var body = json;
        if (base64)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            body = Convert.ToBase64String(bytes);
        }

        var apiRequest = new APIGatewayProxyRequest { Body = body, IsBase64Encoded = base64 };
        var context = new TestLambdaContext();
        return await function.FunctionHandler(apiRequest, context);
    }
}