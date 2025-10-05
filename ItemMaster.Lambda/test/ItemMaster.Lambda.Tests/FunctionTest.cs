using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;
using ItemMaster.Contracts;

namespace ItemMaster.Lambda.Tests;

public class FunctionTest
{
    [Fact]
    public async Task ProcessSkus_ReturnsLoggedCount()
    {
        var function = new Function();
        var context = new TestLambdaContext();
        var req = new ProcessSkusRequest { Skus = new List<string> { "SKU1", "SKU2", "SKU3" } };
        var request = new APIGatewayProxyRequest
        {
            Body = JsonSerializer.Serialize(req, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };

        var response = await function.FunctionHandler(request, context);

        Assert.Equal(200, response.StatusCode);
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(3, parsed!.Logged);
        Assert.Equal(0, parsed.Failed);
        Assert.Equal(0, parsed.Published); // not implemented yet
    }

    [Fact]
    public async Task EmptyBody_ReturnsZeroLogged()
    {
        var function = new Function();
        var context = new TestLambdaContext();
        var request = new Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest { Body = string.Empty };
        var response = await function.FunctionHandler(request, context);
        Assert.Equal(200, response.StatusCode);
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(0, parsed!.Logged);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public async Task Base64EncodedBody_ParsesAndLogs()
    {
        var function = new Function();
        var context = new TestLambdaContext();
        var req = new ProcessSkusRequest { Skus = new List<string> { "B1", "B2" } };
        var json = JsonSerializer.Serialize(req, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        var request = new Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest { Body = base64, IsBase64Encoded = true };
        var response = await function.FunctionHandler(request, context);
        Assert.Equal(200, response.StatusCode);
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(2, parsed!.Logged);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public async Task InvalidJson_ReturnsZeroLogged()
    {
        var function = new Function();
        var context = new TestLambdaContext();
        var request = new Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest { Body = "{not-json" };
        var response = await function.FunctionHandler(request, context);
        Assert.Equal(200, response.StatusCode); // still 200 but empty processed list
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(0, parsed!.Logged);
        Assert.Equal(0, parsed.Failed);
    }
}
