using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;

namespace ItemMaster.Integration.Tests;

public class LocalStackHelper
{
    private readonly IConfiguration _configuration;
    private readonly string _endpointUrl;
    private readonly string _region;

    public LocalStackHelper()
    {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true)
            .AddEnvironmentVariables()
            .Build();

        _endpointUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") ?? "http://localhost:4566";
        _region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";
    }

    public IAmazonSQS CreateSqsClient()
    {
        var config = new AmazonSQSConfig
        {
            ServiceURL = _endpointUrl,
            RegionEndpoint = RegionEndpoint.GetBySystemName(_region),
            UseHttp = true
        };

        var credentials = new BasicAWSCredentials("test", "test");
        return new AmazonSQSClient(credentials, config);
    }

    public async Task<string> CreateTestQueueAsync(IAmazonSQS sqsClient, string queueName)
    {
        try
        {
            var request = new CreateQueueRequest
            {
                QueueName = queueName
            };

            var response = await sqsClient.CreateQueueAsync(request);
            var queueUrl = response.QueueUrl;

            Console.WriteLine($"Created test queue: {queueUrl}");
            return queueUrl;
        }
        catch (QueueNameExistsException)
        {
            var getUrlRequest = new GetQueueUrlRequest { QueueName = queueName };
            var urlResponse = await sqsClient.GetQueueUrlAsync(getUrlRequest);
            return urlResponse.QueueUrl;
        }
    }

    public async Task<List<Message>> ReceiveMessagesAsync(IAmazonSQS sqsClient, string queueUrl, int maxMessages = 10)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = maxMessages,
            WaitTimeSeconds = 5
        };

        var response = await sqsClient.ReceiveMessageAsync(request);
        return response.Messages;
    }

    public async Task PurgeQueueAsync(IAmazonSQS sqsClient, string queueUrl)
    {
        try
        {
            await sqsClient.PurgeQueueAsync(new PurgeQueueRequest { QueueUrl = queueUrl });
            Console.WriteLine("Queue purged successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to purge queue: {ex.Message}");
        }
    }
}