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

        _logger.LogInformation("ConnResolveStart fullEnvPresent={FullEnv} host={Host} db={Db} port={Port} ssl={Ssl} secretId={SecretId}",
            !string.IsNullOrWhiteSpace(_fullConnEnv), _host, _dbName, _port ?? "(null)", _sslMode ?? "(null)", _credsSecret ?? "(null)");

        if (!string.IsNullOrWhiteSpace(_fullConnEnv))
        {
            _cached = _fullConnEnv;
            var (fu, fp) = ParseUserPwdFromConn(_fullConnEnv);
            _logger.LogInformation("ConnStringSource=FullEnv length={Length} conn={Conn} user={User} pwd={Pwd}", _fullConnEnv.Length, _fullConnEnv, fu, fp);
            return _cached;
        }

        var assembled = TryAssembleFromHostAndSecret();
        if (assembled is not null)
        {
            _cached = assembled;
            _logger.LogInformation("ConnResolveResult=Assembled length={Length} conn={Conn}", _cached.Length, _cached);
            return _cached;
        }

        _logger.LogInformation("ConnectionStringResolutionFailed host={Host} db={Db} secretId={Secret}", _host, _dbName, _credsSecret);
        return null;
    }

    private string? TryAssembleFromHostAndSecret()
    {
        if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_dbName) || string.IsNullOrWhiteSpace(_credsSecret))
        {
            _logger.LogInformation("AssembleSkip missingHost={MissingHost} missingDb={MissingDb} missingSecret={MissingSecret}", string.IsNullOrWhiteSpace(_host), string.IsNullOrWhiteSpace(_dbName), string.IsNullOrWhiteSpace(_credsSecret));
            return null;
        }
        _logger.LogInformation("AssembleAttempt host={Host} db={Db} secretId={Secret}", _host, _dbName, _credsSecret);
        try
        {
            var resp = _secrets.GetSecretValueAsync(new GetSecretValueRequest { SecretId = _credsSecret }).GetAwaiter().GetResult();
            var secretString = resp.SecretString;
            _logger.LogInformation("SecretFetched secretId={Secret} length={Length} stageCount={StageCount}", _credsSecret, secretString?.Length ?? 0, resp.VersionStages?.Count ?? 0);
            if (string.IsNullOrWhiteSpace(secretString)) return null;

            var raw = ExtractCredentialsAndMaybeConnString(secretString, out var user, out var pwd);
            if (raw is not null && raw.Contains("Server=") && raw.Contains("Uid=") && raw.Contains("Pwd="))
            {
                var (ru, rp) = ParseUserPwdFromConn(raw);
                _logger.LogInformation("ConnStringSource=SecretRawFull secretId={Secret} conn={Conn} user={User} pwd={Pwd}", _credsSecret, raw, ru, rp);
                return raw;
            }
            if (user is not null && pwd is not null)
            {
                var conn = BuildConnectionString(_host!, _dbName!, user, pwd, _port, _sslMode);
                _logger.LogInformation("ConnStringSource=Assembled host={Host} db={Db} user={User} pwd={Pwd} ssl={SslMode} port={Port} conn={ConnectionString}", _host, _dbName, user, pwd, _sslMode ?? "None", _port ?? "3306", conn);
                return conn;
            }
            _logger.LogInformation("SecretMissingCredentials secretId={Secret}", _credsSecret);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "SecretLookupFailure secretId={Secret}", _credsSecret);
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

        var wasJson = false;
        try
        {
            var trimmed = secret.TrimStart();
            if (trimmed.StartsWith("{"))
            {
                using var doc = JsonDocument.Parse(secret);
                wasJson = true;
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    user = GetFirstString(doc.RootElement, "username", "user", "Uid", "login");
                    pwd = GetFirstString(doc.RootElement, "password", "pwd", "pass", "secret");
                    var rawConn = GetFirstString(doc.RootElement, "connectionString", "conn", "full");
                    _logger.LogInformation("SecretJsonParsed userFound={UserFound} pwdFound={PwdFound} rawConnFound={RawConnFound} userVal={UserVal}", user != null, pwd != null, rawConn != null, user);
                    if (rawConn is not null) return rawConn; // full connection string inside JSON
                    if (user is not null && pwd is not null)
                        return null; // signal credentials extracted; caller will assemble
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "SecretJsonParseFailed wasJson={WasJson} rawPreview={Preview}", wasJson, secret.Length > 40 ? secret.Substring(0,40) : secret);
            // fall through to other heuristics
        }

        // Only attempt colon form if we still don't have both user & pwd
        if ((user is null || pwd is null) && secret.Contains(":") && !secret.Contains("Server="))
        {
            var parts = secret.Split(':', 2);
            if (parts.Length == 2 && !parts[0].Contains("\"username\"")) // avoid splitting raw JSON again
            {
                user = parts[0];
                pwd = parts[1];
                _logger.LogInformation("SecretColonForm userLen={UserLen} userVal={UserVal}", user.Length, user);
                return null;
            }
        }

        if (secret.Contains("Server=") && secret.Contains("Uid=") && secret.Contains("Pwd="))
        {
            _logger.LogInformation("SecretLooksLikeFullConnectionString length={Length}", secret.Length);
            return secret;
        }

        // If we reach here and user/pwd still null, return original so caller can decide (likely failure)
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
