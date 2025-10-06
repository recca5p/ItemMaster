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
            _logger.LogInformation("DbConnResolved source=env");
            return _cached;
        }

        var assembled = TryAssemble();
        if (assembled is not null)
        {
            _cached = assembled;
            _logger.LogInformation("DbConnResolved source=assembled host={Host} db={Db}", _host, _dbName);
            return _cached;
        }

        _logger.LogError("DbConnResolveFailed host={Host} db={Db} secretId={SecretId}", _host, _dbName, _credsSecret);
        return null;
    }

    private string? TryAssemble()
    {
        if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_dbName) || string.IsNullOrWhiteSpace(_credsSecret)) return null;
        try
        {
            var resp = _secrets.GetSecretValueAsync(new GetSecretValueRequest { SecretId = _credsSecret }).GetAwaiter().GetResult();
            var secret = resp.SecretString;
            if (string.IsNullOrWhiteSpace(secret)) return null;
            var raw = ExtractCredentialsAndMaybeConnString(secret, out var user, out var pwd);
            if (raw is not null && raw.Contains("Server=") && raw.Contains("Uid=") && raw.Contains("Pwd=")) return raw;
            if (user is not null && pwd is not null)
                return BuildConnectionString(_host!, _dbName!, user, pwd, _port, _sslMode);
            return null;
        }
        catch
        {
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
        try
        {
            var trimmed = secret.TrimStart();
            if (trimmed.StartsWith("{"))
            {
                using var doc = JsonDocument.Parse(secret);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    user = GetFirstString(doc.RootElement, "username", "user", "Uid", "login");
                    pwd = GetFirstString(doc.RootElement, "password", "pwd", "pass", "secret");
                    var rawConn = GetFirstString(doc.RootElement, "connectionString", "conn", "full");
                    if (rawConn is not null) return rawConn;
                    if (user is not null && pwd is not null) return null;
                }
            }
        }
        catch { }
        if ((user is null || pwd is null) && secret.Contains(":") && !secret.Contains("Server="))
        {
            var parts = secret.Split(':', 2);
            if (parts.Length == 2 && !parts[0].Contains("\"username\""))
            {
                user = parts[0];
                pwd = parts[1];
                return null;
            }
        }
        if (secret.Contains("Server=") && secret.Contains("Uid=") && secret.Contains("Pwd=")) return secret;
        return (user is null || pwd is null) ? secret : null;
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

    private static (string? user, string? pwd) ParseUserPwdFromConn(string conn)
    {
        string? u = null; string? p = null;
        try
        {
            var parts = conn.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                var key = kv[0].Trim();
                var val = kv[1];
                if (key.Equals("Uid", StringComparison.OrdinalIgnoreCase) || key.Equals("User Id", StringComparison.OrdinalIgnoreCase) || key.Equals("Username", StringComparison.OrdinalIgnoreCase)) u = val;
                else if (key.Equals("Pwd", StringComparison.OrdinalIgnoreCase) || key.Equals("Password", StringComparison.OrdinalIgnoreCase)) p = val;
            }
        }
        catch { }
        return (u, p);
    }
}
