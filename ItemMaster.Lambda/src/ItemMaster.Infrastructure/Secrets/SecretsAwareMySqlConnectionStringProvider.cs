using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure.Secrets;

public sealed class SecretsAwareMySqlConnectionStringProvider : IConnectionStringProvider
{
    private readonly IAmazonSecretsManager _secrets;
    private readonly ILogger<SecretsAwareMySqlConnectionStringProvider> _logger;
    private readonly string? _fullConnEnv;
    private readonly string? _host;
    private readonly string? _dbName;
    private readonly string? _sslMode;
    private readonly string? _port;
    private readonly string? _credsSecret;
    private string? _cached;
    private bool _attempted;

    public SecretsAwareMySqlConnectionStringProvider(IAmazonSecretsManager secrets, ILogger<SecretsAwareMySqlConnectionStringProvider> logger)
    {
        _secrets = secrets;
        _logger = logger;
        _fullConnEnv = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");
        _host = Environment.GetEnvironmentVariable("MYSQL_HOST");
        _dbName = Environment.GetEnvironmentVariable("MYSQL_DB_NAME");
        _sslMode = Environment.GetEnvironmentVariable("MYSQL_SSL_MODE");
        _port = Environment.GetEnvironmentVariable("MYSQL_PORT");
        _credsSecret = Environment.GetEnvironmentVariable("MY_SQL_CREDITIAL");
    }

    public string? GetMySqlConnectionString()
    {
        if (_cached is not null) return _cached;
        if (_attempted) return null;
        _attempted = true;

        if (!string.IsNullOrWhiteSpace(_fullConnEnv))
        {
            _cached = _fullConnEnv;
            _logger.LogInformation("ConnStringSource=FullEnv");
            return _cached;
        }

        var assembled = TryAssembleFromHostAndSecret();
        if (assembled is not null)
        {
            _cached = assembled;
            return _cached;
        }

        var filePath = Environment.GetEnvironmentVariable("MYSQL_SECRET_FILE");
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            try
            {
                var fileContent = File.ReadAllText(filePath);
                var extracted = ExtractCredentialsAndMaybeConnString(fileContent, out var user, out var pwd);
                if (!string.IsNullOrWhiteSpace(extracted) && user is not null && pwd is not null && _host != null && _dbName != null)
                {
                    _cached = BuildConnectionString(_host, _dbName, user, pwd, _port, _sslMode);
                    _logger.LogInformation("ConnStringSource=SecretFileAssembled");
                    return _cached;
                }
                if (!string.IsNullOrWhiteSpace(extracted) && extracted.Contains("Server="))
                {
                    _cached = extracted;
                    _logger.LogInformation("ConnStringSource=SecretFileRaw");
                    return _cached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SecretFileReadFailure path={Path}", filePath);
            }
        }

        if (!string.IsNullOrWhiteSpace(_credsSecret))
        {
            try
            {
                var resp = _secrets.GetSecretValueAsync(new GetSecretValueRequest { SecretId = _credsSecret }).GetAwaiter().GetResult();
                var secretString = resp.SecretString;
                if (!string.IsNullOrWhiteSpace(secretString))
                {
                    // If this is directly a connection string use it
                    if (secretString.Contains("Server=") && secretString.Contains("Uid=") && secretString.Contains("Pwd="))
                    {
                        _cached = secretString;
                        _logger.LogInformation("ConnStringSource=LegacyFullSecret secret={Secret}", _credsSecret);
                        return _cached;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LegacySecretLookupFailure secret={Secret}", _credsSecret);
            }
        }

        _logger.LogError("ConnectionStringResolutionFailed host={Host} db={Db} secret={Secret}", _host, _dbName, _credsSecret);
        return null;
    }

    private string? TryAssembleFromHostAndSecret()
    {
        if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_dbName) || string.IsNullOrWhiteSpace(_credsSecret))
            return null;
        try
        {
            var resp = _secrets.GetSecretValueAsync(new GetSecretValueRequest { SecretId = _credsSecret }).GetAwaiter().GetResult();
            var secretString = resp.SecretString;
            if (string.IsNullOrWhiteSpace(secretString)) return null;

            var raw = ExtractCredentialsAndMaybeConnString(secretString, out var user, out var pwd);
            if (raw is not null && raw.Contains("Server=") && raw.Contains("Uid=") && raw.Contains("Pwd="))
            {
                _logger.LogInformation("ConnStringSource=SecretRawFull secret={Secret}", _credsSecret);
                return raw;
            }
            if (user is not null && pwd is not null)
            {
                var conn = BuildConnectionString(_host!, _dbName!, user, pwd, _port, _sslMode);
                _logger.LogInformation("ConnStringSource=Assembled host={Host} db={Db} secret={Secret}", _host, _dbName, _credsSecret);
                return conn;
            }
            _logger.LogWarning("SecretMissingCredentials secret={Secret}", _credsSecret);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SecretLookupFailure secret={Secret}", _credsSecret);
            return null;
        }
    }

    private static string BuildConnectionString(string host, string db, string user, string pwd, string? port, string? sslMode)
    {
        var p = int.TryParse(port, out var portInt) ? portInt : 3306;
        var ssl = string.IsNullOrWhiteSpace(sslMode) ? "None" : sslMode;
        return $"Server={host};Port={p};Database={db};Uid={user};Pwd={pwd};SslMode={ssl};TreatTinyAsBoolean=false;";
    }

    private string? ExtractCredentialsAndMaybeConnString(string secret, out string? user, out string? pwd)
    {
        user = null; pwd = null;
        if (string.IsNullOrWhiteSpace(secret)) return null;

        // Try JSON
        try
        {
            using var doc = JsonDocument.Parse(secret);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                user = GetFirstString(doc.RootElement, "username", "user", "Uid", "login");
                pwd = GetFirstString(doc.RootElement, "password", "pwd", "pass", "secret");
                var rawConn = GetFirstString(doc.RootElement, "connectionString", "conn", "full");
                if (rawConn is not null) return rawConn;
            }
        }
        catch { }

        // Try basic user:pass format
        if (secret.Contains(":") && !secret.Contains("Server="))
        {
            var parts = secret.Split(':', 2);
            if (parts.Length == 2)
            {
                user = parts[0];
                pwd = parts[1];
                return null;
            }
        }

        return secret; // maybe a full conn string
    }

    private static string? GetFirstString(JsonElement obj, params string[] names)
    {
        foreach (var n in names)
        {
            if (obj.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        return null;
    }
}
