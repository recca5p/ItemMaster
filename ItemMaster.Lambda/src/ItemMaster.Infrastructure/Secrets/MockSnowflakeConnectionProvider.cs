using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure.Secrets;

[ExcludeFromCodeCoverage]
public class MockSnowflakeConnectionProvider : SnowflakeConnectionProvider
{
  private readonly ILogger<SnowflakeConnectionProvider> _mockLogger;
  private readonly IConfiguration _mockConfiguration;

  public MockSnowflakeConnectionProvider(
      ILogger<SnowflakeConnectionProvider> logger,
      IConfiguration configuration)
      : base(new Amazon.SecretsManager.AmazonSecretsManagerClient(), configuration, logger)
  {
    _mockLogger = logger;
    _mockConfiguration = configuration;
  }

  public override Task<string> GetConnectionStringAsync()
  {
    // Return a mock Snowflake connection string for integration tests
    // This won't actually connect, but it satisfies the dependency

    var account = _mockConfiguration["snowflake:account"] ?? "TEST_ACCOUNT";
    var user = _mockConfiguration["snowflake:user"] ?? "TEST_USER";
    var role = _mockConfiguration["snowflake:role"] ?? "TEST_ROLE";

    var connectionString = $"account={account};user={user};authenticator=SNOWFLAKE_JWT;role={role};";

    _mockLogger.LogInformation(
        "Mock Snowflake connection string generated: account={Account}, user={User}, role={Role}",
        account, user, role);

    return Task.FromResult(connectionString);
  }
}

