using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure.Secrets;

public class SnowflakeConnectionProvider : ISnowflakeConnectionProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SnowflakeConnectionProvider> _logger;
    private readonly IAmazonSecretsManager _secretsManager;

    public SnowflakeConnectionProvider(
        IAmazonSecretsManager secretsManager,
        IConfiguration configuration,
        ILogger<SnowflakeConnectionProvider> logger)
    {
        _secretsManager = secretsManager;
        _configuration = configuration;
        _logger = logger;
    }

    public virtual async Task<string> GetConnectionStringAsync()
    {
        try
        {
            var rsaKeySecretName = Environment.GetEnvironmentVariable("SSM_RSA_PATH") ??
                                   throw new InvalidOperationException(
                                       "Snowflake RSA key secret configuration is required");
            var rsaKeyResponse = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = rsaKeySecretName
            });

            var raw = rsaKeyResponse.SecretString;
            if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("RSA private key secret is empty");

            string? pem = null;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("private_key", out var pk))
                    pem = pk.GetString();
            }
            catch
            {
                pem = raw;
            }

            if (string.IsNullOrWhiteSpace(pem))
                throw new InvalidOperationException("RSA private key not found in secret");

            var pkcs8Der = ToPkcs8DerBytes(pem);
            if (pkcs8Der == null || pkcs8Der.Length == 0)
                throw new InvalidOperationException("Unable to normalize private key to PKCS#8 DER");

            var pemForDriver = BuildPkcs8Pem(pkcs8Der);

            var tempKeyFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempKeyFile, pemForDriver);

            var privateKeyParam = $"private_key_file={tempKeyFile}";

            var account = _configuration["snowflake:account"];
            var user = _configuration["snowflake:user"];
            var role = _configuration["snowflake:role"];

            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(user))
                throw new InvalidOperationException("Missing required Snowflake configuration (account/user)");

            var sb = new StringBuilder();
            sb.Append($"account={account};user={user};authenticator=SNOWFLAKE_JWT;{privateKeyParam}");
            if (!string.IsNullOrWhiteSpace(role)) sb.Append($";role={role}");

            var connectionString = sb.ToString();
            _logger.LogInformation(
                "Snowflake connection string (without db/schema/warehouse): account={Account}, user={User}, role={Role}",
                account, user, role ?? "not specified");

            return connectionString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build Snowflake connection string");
            throw;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static byte[] ToPkcs8DerBytes(string pem)
    {
        var text = pem.Trim();

        if (text.Contains("BEGIN RSA PRIVATE KEY", StringComparison.OrdinalIgnoreCase))
        {
            var base64 = ExtractBase64Between(text, "RSA PRIVATE KEY");
            var pkcs1Der = Convert.FromBase64String(base64);
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(pkcs1Der, out _);
            return rsa.ExportPkcs8PrivateKey();
        }

        if (text.Contains("BEGIN PRIVATE KEY", StringComparison.OrdinalIgnoreCase))
        {
            var base64 = ExtractBase64Between(text, "PRIVATE KEY");
            var der = Convert.FromBase64String(base64);
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(der, out _);
            return der;
        }

        try
        {
            var der = Convert.FromBase64String(text.Replace("\r", string.Empty).Replace("\n", string.Empty)
                .Replace(" ", string.Empty));
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(der, out _);
            return der;
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static string ExtractBase64Between(string pem, string label)
    {
        var pattern = $"-----BEGIN {Regex.Escape(label)}-----|-----END {Regex.Escape(label)}-----";
        var cleaned = Regex.Replace(pem, pattern, string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        cleaned = cleaned.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace(" ", string.Empty).Trim();
        return cleaned;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static string BuildPkcs8Pem(byte[] der)
    {
        var base64 = Convert.ToBase64String(der);
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PRIVATE KEY-----");
        for (var i = 0; i < base64.Length; i += 64)
        {
            var len = Math.Min(64, base64.Length - i);
            sb.AppendLine(base64.Substring(i, len));
        }

        sb.Append("-----END PRIVATE KEY-----");
        return sb.ToString();
    }
}