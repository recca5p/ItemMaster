using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using ItemMaster.Shared;

namespace ItemMaster.Infrastructure.Secrets;

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
        if (_attempted) return null;
        _attempted = true;

        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            _cached = _envConn;
            return _cached;
        }

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
            catch { }
        }

        if (string.IsNullOrWhiteSpace(_secretName)) return null;
        try
        {
            var resp = _secrets.GetSecretValueAsync(new GetSecretValueRequest { SecretId = _secretName }).GetAwaiter().GetResult();
            var secretString = resp.SecretString;
            if (string.IsNullOrWhiteSpace(secretString)) return null;
            var extracted = TryExtract(secretString);
            _cached = extracted;
            return _cached;
        }
        catch { return null; }
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
        catch { }
        return raw;
    }
}
