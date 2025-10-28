using FluentAssertions;
using ItemMaster.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class SnowflakeItemQueryBuilderTests
{
  [Fact]
  public void Constructor_WithValidOptions_ShouldInitialize()
  {
    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "TEST_SCHEMA",
      Table = "TEST_TABLE"
    };
    var mockOptions = Options.Create(options);

    var queryBuilder = new SnowflakeItemQueryBuilder(mockOptions);

    queryBuilder.Should().NotBeNull();
  }

  [Fact]
  public void Constructor_WithNullOptions_ShouldThrow()
  {
    var act = () => new SnowflakeItemQueryBuilder(null!);

    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public void Constructor_WithEmptyDatabase_ShouldThrow()
  {
    var options = new SnowflakeOptions
    {
      Database = "",
      Schema = "TEST_SCHEMA",
      Table = "TEST_TABLE"
    };
    var mockOptions = Options.Create(options);

    var act = () => new SnowflakeItemQueryBuilder(mockOptions);

    act.Should().Throw<ArgumentException>().WithMessage("*database*");
  }

  [Fact]
  public void Constructor_WithEmptySchema_ShouldThrow()
  {
    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "",
      Table = "TEST_TABLE"
    };
    var mockOptions = Options.Create(options);

    var act = () => new SnowflakeItemQueryBuilder(mockOptions);

    act.Should().Throw<ArgumentException>().WithMessage("*schema*");
  }

  [Fact]
  public void Constructor_WithEmptyTable_ShouldThrow()
  {
    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "TEST_SCHEMA",
      Table = ""
    };
    var mockOptions = Options.Create(options);

    var act = () => new SnowflakeItemQueryBuilder(mockOptions);

    act.Should().Throw<ArgumentException>().WithMessage("*table*");
  }

  [Fact]
  public void BuildSelectAll_ShouldReturnCorrectQuery()
  {
    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "TEST_SCHEMA",
      Table = "ITEMS"
    };
    var mockOptions = Options.Create(options);
    var queryBuilder = new SnowflakeItemQueryBuilder(mockOptions);

    var sql = queryBuilder.BuildSelectAll();

    sql.Should().Contain("SELECT");
    sql.Should().Contain("BRAND AS Brand");
    sql.Should().Contain("FROM TEST_DB.TEST_SCHEMA.ITEMS");
    sql.Should().Contain("ORDER BY UPDATED_AT_SNOWFLAKE DESC");
  }

  [Fact]
  public void BuildSelectBySkus_WithValidSkus_ShouldReturnCorrectQuery()
  {
    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "TEST_SCHEMA",
      Table = "ITEMS"
    };
    var mockOptions = Options.Create(options);
    var queryBuilder = new SnowflakeItemQueryBuilder(mockOptions);
    var skus = new[] { "TEST-001", "TEST-002", "TEST-003" };

    var (sql, parameters) = queryBuilder.BuildSelectBySkus(skus);

    sql.Should().Contain("SELECT");
    sql.Should().Contain("FROM TEST_DB.TEST_SCHEMA.ITEMS");
    sql.Should().Contain("WHERE SKU IN");
    sql.Should().Contain("'TEST-001'");
    sql.Should().Contain("'TEST-002'");
    sql.Should().Contain("'TEST-003'");
    sql.Should().Contain("ORDER BY UPDATED_AT_SNOWFLAKE DESC");
  }

  [Fact]
  public void BuildSelectBySkus_WithEmptyList_ShouldReturnEmptyQuery()
  {
    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "TEST_SCHEMA",
      Table = "ITEMS"
    };
    var mockOptions = Options.Create(options);
    var queryBuilder = new SnowflakeItemQueryBuilder(mockOptions);
    var skus = Array.Empty<string>();

    var (sql, parameters) = queryBuilder.BuildSelectBySkus(skus);

    sql.Should().Contain("WHERE 1=0");
    sql.Should().Contain("ORDER BY UPDATED_AT_SNOWFLAKE DESC");
  }

  [Fact]
  public void BuildSelectBySkus_WithNullAndEmptySkus_ShouldFilterThemOut()
  {
    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "TEST_SCHEMA",
      Table = "ITEMS"
    };
    var mockOptions = Options.Create(options);
    var queryBuilder = new SnowflakeItemQueryBuilder(mockOptions);
    var skus = new[] { "TEST-001", null, "", " ", "TEST-002", "  " };

    var (sql, parameters) = queryBuilder.BuildSelectBySkus(skus);

    sql.Should().Contain("'TEST-001'");
    sql.Should().Contain("'TEST-002'");
  }

  [Fact]
  public void BuildSelectBySkus_WithSpecialCharacters_ShouldEscapeThem()
  {
    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "TEST_SCHEMA",
      Table = "ITEMS"
    };
    var mockOptions = Options.Create(options);
    var queryBuilder = new SnowflakeItemQueryBuilder(mockOptions);
    var skus = new[] { "SKU'TEST" };

    var (sql, parameters) = queryBuilder.BuildSelectBySkus(skus);

    sql.Should().Contain("WHERE 1=0");
  }

  [Fact]
  public void BuildSelectBySkus_ShouldDeduplicateSkus()
  {
    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "TEST_SCHEMA",
      Table = "ITEMS"
    };
    var mockOptions = Options.Create(options);
    var queryBuilder = new SnowflakeItemQueryBuilder(mockOptions);
    var skus = new[] { "TEST-001", "test-001", "TEST-001", "TEST-002" };

    var (sql, parameters) = queryBuilder.BuildSelectBySkus(skus);

    var test001Count = sql.Split("'TEST-001'").Length - 1;
    test001Count.Should().Be(1);
  }

  [Fact]
  public void BuildSelectLatest_ShouldReturnCorrectQuery()
  {
    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "TEST_SCHEMA",
      Table = "ITEMS"
    };
    var mockOptions = Options.Create(options);
    var queryBuilder = new SnowflakeItemQueryBuilder(mockOptions);

    var sql = queryBuilder.BuildSelectLatest(100);

    sql.Should().Contain("SELECT");
    sql.Should().Contain("FROM TEST_DB.TEST_SCHEMA.ITEMS");
    sql.Should().Contain("WHERE CREATED_AT_SNOWFLAKE IS NOT NULL");
    sql.Should().Contain("ORDER BY CREATED_AT_SNOWFLAKE DESC");
    sql.Should().Contain("LIMIT 100");
  }

  [Fact]
  public void BuildSelectBySkus_WithUnsafeCharacters_ShouldFilterThem()
  {
    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "TEST_SCHEMA",
      Table = "ITEMS"
    };
    var mockOptions = Options.Create(options);
    var queryBuilder = new SnowflakeItemQueryBuilder(mockOptions);
    var skus = new[] { "SKU;DROP TABLE", "SKU\nTEST", "SKU\rTEST" };

    var (sql, parameters) = queryBuilder.BuildSelectBySkus(skus);

    sql.Should().Contain("WHERE 1=0");
  }

  [Theory]
  [InlineData("TEST/001", true)] // Valid with slash
  public void IsSafeIdentifier_ShouldValidateCorrectly(string identifier, bool expected)
  {

    var options = new SnowflakeOptions
    {
      Database = "TEST_DB",
      Schema = "TEST_SCHEMA",
      Table = "ITEMS"
    };
    var mockOptions = Options.Create(options);
    var queryBuilder = new SnowflakeItemQueryBuilder(mockOptions);

    var (sql, _) = queryBuilder.BuildSelectBySkus(new[] { identifier });

    if (expected)
    {
      sql.Should().Contain($"'{identifier.Replace("'", "''")}'");
    }
    else
    {
      sql.Should().Contain("WHERE 1=0");
    }
  }
}

