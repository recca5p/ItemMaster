using Dapper;
using ItemMaster.Domain;
using ItemMaster.Infrastructure.Secrets;
using ItemMaster.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Snowflake.Data.Client;

namespace ItemMaster.Infrastructure;

public class SnowflakeRepository : ISnowflakeRepository
{
    private readonly IConfiguration _configuration;
    private readonly SnowflakeConnectionProvider _connProvider;
    private readonly string _database;
    private readonly ILogger<SnowflakeRepository> _logger;
    private readonly ISnowflakeItemQueryBuilder _queryBuilder;
    private readonly string _schema;
    private readonly string _warehouse;

    public SnowflakeRepository(
        SnowflakeConnectionProvider connProvider,
        ISnowflakeItemQueryBuilder queryBuilder,
        IConfiguration configuration,
        ILogger<SnowflakeRepository> logger)
    {
        _connProvider = connProvider;
        _logger = logger;
        _configuration = configuration;
        _queryBuilder = queryBuilder;

        _database = _configuration["snowflake:database"] ??
                    throw new InvalidOperationException("Snowflake database configuration is required");
        _schema = _configuration["snowflake:schema"] ??
                  throw new InvalidOperationException("Snowflake schema configuration is required");
        _warehouse = _configuration["snowflake:warehouse"] ??
                     throw new InvalidOperationException("Snowflake warehouse configuration is required");
    }

    public async Task<Result<IEnumerable<Item>>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(cancellationToken);
            var sql = _queryBuilder.BuildSelectAll();

            var items = (await conn.QueryAsync<Item>(new CommandDefinition(sql, cancellationToken: cancellationToken)))
                .ToList();
            _logger.LogInformation("Successfully fetched {Count} items from Snowflake", items.Count);
            return Result<IEnumerable<Item>>.Success(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Snowflake query failed in GetItemsAsync");
            return Result<IEnumerable<Item>>.Failure($"Snowflake query failed: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<Item>>> GetItemsBySkusAsync(IEnumerable<string> skus,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var skuList = skus.Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (skuList.Count == 0) return Result<IEnumerable<Item>>.Success(Enumerable.Empty<Item>());

            var results = new List<Item>(skuList.Count);
            const int batchSize = 1000;

            await using var conn = await OpenConnectionAsync(cancellationToken);

            for (var offset = 0; offset < skuList.Count; offset += batchSize)
            {
                var batch = skuList.Skip(offset).Take(batchSize).ToList();
                var (sql, parameters) = _queryBuilder.BuildSelectBySkus(batch);

                var items = await conn.QueryAsync<Item>(sql, parameters);
                var itemsList = items.ToList();
                results.AddRange(itemsList);
            }

            _logger.LogInformation("Successfully fetched {Count} items for {SkuCount} SKUs from Snowflake",
                results.Count, skuList.Count);
            return Result<IEnumerable<Item>>.Success(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Snowflake query failed in GetItemsBySkusAsync");
            return Result<IEnumerable<Item>>.Failure($"Snowflake query by SKUs failed: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<Item>>> GetLatestItemsAsync(int count = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(cancellationToken);
            var sql = _queryBuilder.BuildSelectLatest(count);

            var items = (await conn.QueryAsync<Item>(new CommandDefinition(sql, cancellationToken: cancellationToken)))
                .ToList();
            _logger.LogInformation("Successfully fetched {Count} latest items from Snowflake", items.Count);
            return Result<IEnumerable<Item>>.Success(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Snowflake query failed in GetLatestItemsAsync");
            return Result<IEnumerable<Item>>.Failure($"Snowflake query for latest items failed: {ex.Message}");
        }
    }

    private async Task<SnowflakeDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var cs = await _connProvider.GetConnectionStringAsync();

        var conn = new SnowflakeDbConnection { ConnectionString = cs };
        await conn.OpenAsync(cancellationToken);

        try
        {
            await conn.ExecuteAsync($"USE WAREHOUSE {_warehouse}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set warehouse to: {Warehouse}", _warehouse);
            throw;
        }

        try
        {
            await conn.ExecuteAsync($"USE DATABASE {_database}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set database to: {Database}", _database);
            throw;
        }

        try
        {
            await conn.ExecuteAsync($"USE SCHEMA {_schema}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set schema to: {Schema}", _schema);
            throw;
        }

        return conn;
    }
}