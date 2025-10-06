using Xunit;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;
using ItemMaster.Contracts;

namespace ItemMaster.Lambda.Tests;

public class FunctionTest
{
    private static APIGatewayProxyResponse Invoke(Function function, object? body, bool base64 = false)
    {
        var request = new APIGatewayProxyRequest();
        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (base64)
            {
                request.Body = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
                request.IsBase64Encoded = true;
            }
            else
            {
                request.Body = json;
            }
        }
        var ctx = new TestLambdaContext();
        return function.FunctionHandler(request, ctx).GetAwaiter().GetResult();
    }

    [Fact]
    public void ProcessSkus_ReturnsLoggedCount()
    {
        Environment.SetEnvironmentVariable("ITEMMASTER_TEST_MODE", "true");
        var function = new Function();
        var resp = Invoke(function, new ProcessSkusRequest { Skus = new List<string> { "SKU1", "SKU2", "SKU3" } });
        Assert.Equal(200, resp.StatusCode);
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(resp.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(3, parsed!.Logged);
        Assert.Equal(0, parsed.Failed);
        Assert.Equal(0, parsed.Published);
    }

    [Fact]
    public void EmptyBody_ReturnsZeroLogged()
    {
        Environment.SetEnvironmentVariable("ITEMMASTER_TEST_MODE", "true");
        var function = new Function();
        var ctx = new TestLambdaContext();
        var request = new APIGatewayProxyRequest { Body = string.Empty };
        var response = function.FunctionHandler(request, ctx).GetAwaiter().GetResult();
        Assert.Equal(200, response.StatusCode);
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(0, parsed!.Logged);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public void Base64EncodedBody_ParsesAndLogs()
    {
        Environment.SetEnvironmentVariable("ITEMMASTER_TEST_MODE", "true");
        var function = new Function();
        var resp = Invoke(function, new ProcessSkusRequest { Skus = new List<string> { "B1", "B2" } }, base64: true);
        Assert.Equal(200, resp.StatusCode);
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(resp.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(2, parsed!.Logged);
        Assert.Equal(0, parsed.Failed);
    }

    [Fact]
    public void InvalidJson_ReturnsZeroLogged()
    {
        Environment.SetEnvironmentVariable("ITEMMASTER_TEST_MODE", "true");
        var function = new Function();
        var ctx = new TestLambdaContext();
        var request = new APIGatewayProxyRequest { Body = "{not-json" };
        var response = function.FunctionHandler(request, ctx).GetAwaiter().GetResult();
        Assert.Equal(200, response.StatusCode);
        var parsed = JsonSerializer.Deserialize<ProcessSkusResponse>(response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(parsed);
        Assert.Equal(0, parsed!.Logged);
        Assert.Equal(0, parsed.Failed);
    }
}
