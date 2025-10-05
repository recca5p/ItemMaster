using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using ItemMaster.Shared;

namespace ItemMaster.Infrastructure.Secrets;

/// <summary>
/// Resolves MySQL connection string by first checking environment variable MYSQL_CONNECTION_STRING, then (optionally)
/// AWS Secrets Manager secret defined in MYSQL_SECRET_NAME. If the secret value is JSON and contains a key specified by
/// MYSQL_SECRET_KEY (default: connectionString) that value is used; otherwise the raw secret string is assumed to be the
/// connection string. Result is cached for the lifetime of the Lambda execution environment.
/// </summary>
public sealed class SecretsAwareMySqlConnectionStringProvider : IConnectionStringProvider
{
    private readonly IAmazonSecretsManager _secrets;
    private readonly string? _secretName;
    private readonly string _secretKey;
    private readonly string? _envConn;
    private string? _cached;
    private bool _attempted;

    public SecretsAwareMySqlConnectionStringProvider(IAmazonSecretsManager secrets)
    {
        _secrets = secrets;
        _envConn = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");
        _secretName = Environment.GetEnvironmentVariable("MYSQL_SECRET_NAME");
        _secretKey = Environment.GetEnvironmentVariable("MYSQL_SECRET_KEY") ?? "connectionString";
    }

    public string? GetMySqlConnectionString()
    {
        if (_cached is not null) return _cached;
        if (_attempted) return null; // attempted but failed
        _attempted = true;

        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            _cached = _envConn;
            return _cached;
        }

        // Local dev file fallback
        var filePath = Environment.GetEnvironmentVariable("MYSQL_SECRET_FILE");
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            try
            {
                var fileContent = File.ReadAllText(filePath);
                var fromFile = TryExtract(fileContent);
                if (!string.IsNullOrWhiteSpace(fromFile))
                {
                    _cached = fromFile;
                    return _cached;
                }
            }
            catch { /* ignore */ }
        }

        if (string.IsNullOrWhiteSpace(_secretName)) return null;
        try
        {
            var resp = _secrets.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = _secretName
            }).GetAwaiter().GetResult();

            var secretString = resp.SecretString;
            if (string.IsNullOrWhiteSpace(secretString)) return null;

            var extracted = TryExtract(secretString);
            _cached = extracted;
            return _cached;
        }
        catch
        {
            return null; // swallow to allow fallback to in-memory repo
        }
    }

    private string? TryExtract(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(_secretKey, out var val))
            {
                return val.GetString();
            }
        }
        catch
        {
            // not json
        }
        return raw; // treat as plain
    }
}
