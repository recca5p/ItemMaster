using Amazon.Lambda.APIGatewayEvents;
using ItemMaster.Shared;

namespace ItemMaster.Lambda;

public interface IRequestSourceDetector
{
    RequestSource DetectSource(APIGatewayProxyRequest request);
}