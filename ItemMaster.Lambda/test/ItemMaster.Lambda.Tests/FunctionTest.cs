using Xunit;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;
using ItemMaster.Contracts;
using System.Text;

namespace ItemMaster.Lambda.Tests;

public class FunctionTest
{
    static FunctionTest()
    {
        Environment.SetEnvironmentVariable("ITEMMASTER_TEST_MODE", "true");
    }

    [Fact]
    public async Task ProcessSkus_ReturnsLoggedCount()
    {
        var function = new Function();
        var resp = await InvokeAsync(function, new ProcessSkusRequest { Skus = new List<string> { "SKU1", "SKU2", "SKU3" } });
        Assert.Equal(200, resp.StatusCode);
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(resp.Body as string, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(3, parsed!.Logged);
        Assert.Equal(3, parsed.Published);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public async Task EmptyBody_ReturnsZeroLogged()
    {
        var function = new Function();
        var context = new TestLambdaContext();
        var request = new APIGatewayProxyRequest { Body = string.Empty };
        var response = await function.FunctionHandler(request, context);
        Assert.Equal(200, response.StatusCode);
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(response.Body as string, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(0, parsed!.Logged);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public async Task Base64EncodedBody_ParsesAndLogs()
    {
        var function = new Function();
        var resp = await InvokeAsync(function, new ProcessSkusRequest { Skus = new List<string> { "B1", "B2" } }, base64: true);
        Assert.Equal(200, resp.StatusCode);
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(resp.Body as string, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(2, parsed!.Logged);
        Assert.Equal(2, parsed.Published);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public async Task InvalidJson_ReturnsZeroLogged()
    {
        var function = new Function();
        var ctx = new TestLambdaContext();
        var request = new APIGatewayProxyRequest { Body = "{not-json" };
        var response = await function.FunctionHandler(request, ctx);
        Assert.Equal(200, response.StatusCode);
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(response.Body as string, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(0, parsed!.Logged);
        Assert.Equal(0, parsed.Published);
        Assert.Equal(0, parsed.Failed);
    }

    private async Task<APIGatewayProxyResponse> InvokeAsync(Function function, ProcessSkusRequest request, bool base64 = false)
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
