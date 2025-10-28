using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using ItemMaster.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure.Secrets;

public class SecretsAwareMySqlConnectionStringProvider : IConnectionStringProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecretsAwareMySqlConnectionStringProvider> _logger;
    private readonly IAmazonSecretsManager _secretsManager;

    public SecretsAwareMySqlConnectionStringProvider(
        IAmazonSecretsManager secretsManager,
        IConfiguration configuration,
        ILogger<SecretsAwareMySqlConnectionStringProvider> logger)
    {
        _secretsManager = secretsManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetMySqlConnectionStringAsync()
    {
        try
        {
            var host = _configuration["mysql:host"];
            var database = _configuration["mysql:db"];
            var secretArn = _configuration["mysql:secret_arn"];

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(database) || string.IsNullOrEmpty(secretArn))
                throw new InvalidOperationException("Missing required MySQL configuration parameters");

            var secret = await GetMySqlSecretAsync(secretArn);

            var connectionString = $"Server={host};Database={database};Uid={secret.Username};Pwd={secret.Password};";

            return connectionString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build MySQL connection string");
            throw;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private async Task<MySqlSecret> GetMySqlSecretAsync(string secretArn)
    {
        var resp = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretArn });

        if (string.IsNullOrWhiteSpace(resp.SecretString))
            throw new InvalidOperationException("Empty secret returned from Secrets Manager");

        var raw = resp.SecretString;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            if (raw.Length > 1 && raw[0] == '"' && raw[^1] == '"')
            {
                var innerJson = JsonSerializer.Deserialize<string>(raw, options);
                if (string.IsNullOrWhiteSpace(innerJson))
                    throw new InvalidOperationException("Secret appears double-encoded but inner JSON is empty");

                var secret = JsonSerializer.Deserialize<MySqlSecret>(innerJson, options);
                if (secret == null) throw new InvalidOperationException("Failed to parse inner MySQL secret JSON");
                return secret;
            }

            var maybe = JsonSerializer.Deserialize<MySqlSecret>(raw, options);
            if (maybe == null) throw new InvalidOperationException("Failed to parse MySQL secret JSON");
            return maybe;
        }
        catch (JsonException jex)
        {
            _logger.LogError(jex, "Failed to deserialize MySQL secret (JsonException)");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MySQL secret");
            throw;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private class MySqlSecret
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}