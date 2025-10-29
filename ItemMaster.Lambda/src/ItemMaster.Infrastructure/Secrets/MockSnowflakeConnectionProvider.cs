using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ItemMaster.Infrastructure.Secrets;

[ExcludeFromCodeCoverage]
public class MockSnowflakeConnectionProvider : ISnowflakeConnectionProvider
{
  private readonly ILogger<SnowflakeConnectionProvider> _logger;
  private readonly IConfiguration _configuration;

  public MockSnowflakeConnectionProvider(
      ILogger<SnowflakeConnectionProvider> logger,
      IConfiguration configuration)
  {
    _logger = logger;
    _configuration = configuration;
  }

  public Task<string> GetConnectionStringAsync()
  {
    // Return a mock Snowflake connection string for integration tests
    // This won't actually connect, but it satisfies the dependency

    var account = _configuration["snowflake:account"] ?? "TEST_ACCOUNT";
    var user = _configuration["snowflake:user"] ?? "TEST_USER";
    var role = _configuration["snowflake:role"] ?? "TEST_ROLE";

    var connectionString = $"account={account};user={user};authenticator=SNOWFLAKE_JWT;role={role};";

    _logger.LogInformation(
        "Mock Snowflake connection string generated: account={Account}, user={User}, role={Role}",
        account, user, role);

    return Task.FromResult(connectionString);
  }
}

