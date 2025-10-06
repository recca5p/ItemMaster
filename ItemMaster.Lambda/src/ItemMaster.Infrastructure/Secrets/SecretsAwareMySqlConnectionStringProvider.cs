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
            !string.IsNullOrWhiteSpace(_fullConnEnv), Mask(_host), Mask(_dbName), _port ?? "(null)", _sslMode ?? "(null)", Mask(_credsSecret));

        if (!string.IsNullOrWhiteSpace(_fullConnEnv))
        {
            _cached = _fullConnEnv;
            _logger.LogInformation("ConnStringSource=FullEnv length={Length} preview={Preview}", _fullConnEnv.Length, Mask(_fullConnEnv, 10, 0));
            return _cached;
        }

        var assembled = TryAssembleFromHostAndSecret();
        if (assembled is not null)
        {
            _cached = assembled;
            _logger.LogInformation("ConnResolveResult=Assembled length={Length} preview={Preview}", _cached.Length, Mask(_cached, 12, 0));
            return _cached;
        }

        var filePath = Environment.GetEnvironmentVariable("MYSQL_SECRET_FILE");
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            _logger.LogInformation("SecretFileAttempt path={Path}", filePath);
            try
            {
                var fileContent = File.ReadAllText(filePath);
                _logger.LogInformation("SecretFileRead length={Length} preview={Preview}", fileContent.Length, Mask(fileContent, 8, 0));
                var extracted = ExtractCredentialsAndMaybeConnString(fileContent, out var user, out var pwd);
                _logger.LogInformation("SecretFileParsed userFound={UserFound} pwdFound={PwdFound} rawReturned={RawReturned}", user != null, pwd != null, extracted != null);
                if (!string.IsNullOrWhiteSpace(extracted) && user is not null && pwd is not null && _host != null && _dbName != null)
                {
                    _cached = BuildConnectionString(_host, _dbName, user, pwd, _port, _sslMode);
                    _logger.LogInformation("ConnStringSource=SecretFileAssembled userMask={User} host={Host} db={Db}", Mask(user), Mask(_host), Mask(_dbName));
                    return _cached;
                }
                if (!string.IsNullOrWhiteSpace(extracted) && extracted.Contains("Server="))
                {
                    _cached = extracted;
                    _logger.LogInformation("ConnStringSource=SecretFileRaw length={Length} preview={Preview}", extracted.Length, Mask(extracted, 12, 0));
                    return _cached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "SecretFileReadFailure path={Path}", filePath);
            }
        }

        if (!string.IsNullOrWhiteSpace(_credsSecret))
        {
            _logger.LogInformation("LegacyFullSecretAttempt secretId={Secret}", Mask(_credsSecret));
            try
            {
                var resp = _secrets.GetSecretValueAsync(new GetSecretValueRequest { SecretId = _credsSecret }).GetAwaiter().GetResult();
                var secretString = resp.SecretString;
                _logger.LogInformation("LegacySecretFetched secretId={Secret} length={Length} preview={Preview}", Mask(_credsSecret), secretString?.Length ?? 0, Mask(secretString, 10, 0));
                if (!string.IsNullOrWhiteSpace(secretString))
                {
                    if (secretString.Contains("Server=") && secretString.Contains("Uid=") && secretString.Contains("Pwd="))
                    {
                        _cached = secretString;
                        _logger.LogInformation("ConnStringSource=LegacyFullSecret secretId={Secret} length={Length} preview={Preview}", Mask(_credsSecret), secretString.Length, Mask(secretString, 12, 0));
                        return _cached;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "LegacySecretLookupFailure secretId={Secret}", Mask(_credsSecret));
            }
        }

        _logger.LogInformation("ConnectionStringResolutionFailed host={Host} db={Db} secretId={Secret}", Mask(_host), Mask(_dbName), Mask(_credsSecret));
        return null;
    }

    private string? TryAssembleFromHostAndSecret()
    {
        if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_dbName) || string.IsNullOrWhiteSpace(_credsSecret))
        {
            _logger.LogInformation("AssembleSkip missingHost={MissingHost} missingDb={MissingDb} missingSecret={MissingSecret}", string.IsNullOrWhiteSpace(_host), string.IsNullOrWhiteSpace(_dbName), string.IsNullOrWhiteSpace(_credsSecret));
            return null;
        }
        _logger.LogInformation("AssembleAttempt host={Host} db={Db} secretId={Secret}", Mask(_host), Mask(_dbName), Mask(_credsSecret));
        try
        {
            var resp = _secrets.GetSecretValueAsync(new GetSecretValueRequest { SecretId = _credsSecret }).GetAwaiter().GetResult();
            var secretString = resp.SecretString;
            _logger.LogInformation("SecretFetched secretId={Secret} length={Length} preview={Preview} stageCount={StageCount}", Mask(_credsSecret), secretString?.Length ?? 0, Mask(secretString, 10, 0), resp.VersionStages?.Count ?? 0);
            if (string.IsNullOrWhiteSpace(secretString)) return null;

            var raw = ExtractCredentialsAndMaybeConnString(secretString, out var user, out var pwd);
            if (raw is not null && raw.Contains("Server=") && raw.Contains("Uid=") && raw.Contains("Pwd="))
            {
                _logger.LogInformation("ConnStringSource=SecretRawFull secretId={Secret} preview={Preview}", Mask(_credsSecret), Mask(raw, 12, 0));
                return raw;
            }
            if (user is not null && pwd is not null)
            {
                var conn = BuildConnectionString(_host!, _dbName!, user, pwd, _port, _sslMode);
                _logger.LogInformation("ConnStringSource=Assembled host={Host} db={Db} userLen={UserLen} ssl={SslMode} port={Port}", Mask(_host), Mask(_dbName), user.Length, _sslMode ?? "None", _port ?? "3306");
                return conn;
            }
            _logger.LogInformation("SecretMissingCredentials secretId={Secret}", Mask(_credsSecret));
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "SecretLookupFailure secretId={Secret}", Mask(_credsSecret));
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
            using var doc = JsonDocument.Parse(secret);
            wasJson = true;
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                user = GetFirstString(doc.RootElement, "username", "user", "Uid", "login");
                pwd = GetFirstString(doc.RootElement, "password", "pwd", "pass", "secret");
                var rawConn = GetFirstString(doc.RootElement, "connectionString", "conn", "full");
                _logger.LogInformation("SecretJsonParsed userFound={UserFound} pwdFound={PwdFound} rawConnFound={RawConnFound} userVal={UserVal}", user != null, pwd != null, rawConn != null, Mask(user));
                if (rawConn is not null) return rawConn;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "SecretJsonParseFailed wasJson={WasJson} preview={Preview}", wasJson, Mask(secret, 10, 0));
        }

        if (secret.Contains(":") && !secret.Contains("Server="))
        {
            var parts = secret.Split(':', 2);
            if (parts.Length == 2)
            {
                user = parts[0];
                pwd = parts[1];
                _logger.LogInformation("SecretColonForm userLen={UserLen} userVal={UserVal}", user.Length, Mask(user));
                return null;
            }
        }

        if (secret.Contains("Server=") && secret.Contains("Uid=") && secret.Contains("Pwd="))
        {
            _logger.LogInformation("SecretLooksLikeFullConnectionString length={Length} preview={Preview}", secret.Length, Mask(secret, 14, 0));
        }
        return secret;
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

    private static string? Mask(string? value, int keepStart = 4, int keepEnd = 2)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length <= keepStart + keepEnd) return new string('*', value.Length);
        return value.Substring(0, keepStart) + "***" + value.Substring(value.Length - keepEnd);
    }
}
