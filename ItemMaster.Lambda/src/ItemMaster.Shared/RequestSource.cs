namespace ItemMaster.Shared;

public enum RequestSource
{
    Unknown = 0,
    ApiGateway = 1,
    EventBridge = 2,
    CicdHealthCheck = 3,
    Lambda = 4,
    Sqs = 5
}