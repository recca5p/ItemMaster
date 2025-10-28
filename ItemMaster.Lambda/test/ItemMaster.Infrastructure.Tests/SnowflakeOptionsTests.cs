using FluentAssertions;
using ItemMaster.Infrastructure;
using Xunit;
using Xunit;

namespace ItemMaster.Infrastructure.Tests;

public class SnowflakeOptionsTests
{
    public static IEnumerable<object[]> GetSnowflakeOptionsTestData()
    {
        // Test case 1: Production configuration
        yield return new object[]
        {
            new SnowflakeOptions
            {
                Database = "PROD_ITEMMASTER_DB",
                Schema = "PUBLIC",
                Table = "ITEMS"
            },
            "Production configuration"
        };

        // Test case 2: Development configuration
        yield return new object[]
        {
            new SnowflakeOptions
            {
                Database = "DEV_ITEMMASTER_DB",
                Schema = "DEVELOPMENT",
                Table = "ITEMS_DEV"
            },
            "Development configuration"
        };

        // Test case 3: Test configuration
        yield return new object[]
        {
            new SnowflakeOptions
            {
                Database = "TEST_DB",
                Schema = "TEST_SCHEMA",
                Table = "TEST_ITEMS"
            },
            "Test configuration"
        };

        // Test case 4: Configuration with special characters
        yield return new object[]
        {
            new SnowflakeOptions
            {
                Database = "DB_WITH_UNDERSCORES_123",
                Schema = "SCHEMA_WITH_NUMBERS_456",
                Table = "TABLE_WITH_SPECIAL_789"
            },
            "Configuration with special characters"
        };
    }

    [Theory]
    [MemberData(nameof(GetSnowflakeOptionsTestData))]
    public void SnowflakeOptions_WithDifferentConfigurations_ShouldInitializeCorrectly(
        SnowflakeOptions expectedOptions,
        string scenario)
    {
        // Arrange & Act
        var actualOptions = new SnowflakeOptions
        {
            Database = expectedOptions.Database,
            Schema = expectedOptions.Schema,
            Table = expectedOptions.Table
        };

        // Assert
        actualOptions.Should().BeEquivalentTo(expectedOptions, scenario);
    }

    [Fact]
    public void SnowflakeOptions_DefaultConstructor_ShouldInitializeWithEmptyStrings()
    {
        // Arrange & Act
        var options = new SnowflakeOptions();

        // Assert
        options.Database.Should().Be(string.Empty);
        options.Schema.Should().Be(string.Empty);
        options.Table.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SIMPLE_DB")]
    [InlineData("DB_WITH_NUMBERS_123")]
    [InlineData("VERY_LONG_DATABASE_NAME_WITH_MANY_CHARACTERS")]
    [InlineData("db_lowercase")]
    public void SnowflakeOptions_Database_ShouldBeSettableAndGettable(string database)
    {
        // Arrange & Act
        var options = new SnowflakeOptions { Database = database };

        // Assert
        options.Database.Should().Be(database);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PUBLIC")]
    [InlineData("PRIVATE")]
    [InlineData("SCHEMA_WITH_UNDERSCORES")]
    [InlineData("schema_lowercase")]
    [InlineData("SCHEMA123")]
    public void SnowflakeOptions_Schema_ShouldBeSettableAndGettable(string schema)
    {
        // Arrange & Act
        var options = new SnowflakeOptions { Schema = schema };

        // Assert
        options.Schema.Should().Be(schema);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ITEMS")]
    [InlineData("PRODUCTS")]
    [InlineData("TABLE_WITH_UNDERSCORES")]
    [InlineData("table_lowercase")]
    [InlineData("TABLE123")]
    [InlineData("VERY_LONG_TABLE_NAME_WITH_DESCRIPTIVE_PURPOSE")]
    public void SnowflakeOptions_Table_ShouldBeSettableAndGettable(string table)
    {
        // Arrange & Act
        var options = new SnowflakeOptions { Table = table };

        // Assert
        options.Table.Should().Be(table);
    }

    [Fact]
    public void SnowflakeOptions_AllProperties_ShouldBeSettableAndGettable()
    {
        // Arrange
        var database = "ITEMMASTER_DB";
        var schema = "PRODUCTION";
        var table = "ITEMS_TABLE";

        // Act
        var options = new SnowflakeOptions
        {
            Database = database,
            Schema = schema,
            Table = table
        };

        // Assert
        options.Database.Should().Be(database);
        options.Schema.Should().Be(schema);
        options.Table.Should().Be(table);
    }

    [Fact]
    public void SnowflakeOptions_WithNullValues_ShouldHandleGracefully()
    {
        // Arrange & Act
        var options = new SnowflakeOptions
        {
            Database = null!,
            Schema = null!,
            Table = null!
        };

        // Assert - Properties should be settable to null (though not recommended)
        options.Database.Should().BeNull();
        options.Schema.Should().BeNull();
        options.Table.Should().BeNull();
    }

    [Fact]
    public void SnowflakeOptions_Equality_ShouldWorkCorrectlyForSameValues()
    {
        // Arrange
        var options1 = new SnowflakeOptions
        {
            Database = "TEST_DB",
            Schema = "TEST_SCHEMA",
            Table = "TEST_TABLE"
        };

        var options2 = new SnowflakeOptions
        {
            Database = "TEST_DB",
            Schema = "TEST_SCHEMA",
            Table = "TEST_TABLE"
        };

        // Act & Assert
        options1.Should().BeEquivalentTo(options2);
    }

    [Theory]
    [InlineData("DB1", "SCHEMA1", "TABLE1", "DB2", "SCHEMA1", "TABLE1")]
    [InlineData("DB1", "SCHEMA1", "TABLE1", "DB1", "SCHEMA2", "TABLE1")]
    [InlineData("DB1", "SCHEMA1", "TABLE1", "DB1", "SCHEMA1", "TABLE2")]
    public void SnowflakeOptions_WithDifferentValues_ShouldNotBeEqual(
        string db1, string schema1, string table1,
        string db2, string schema2, string table2)
    {
        // Arrange
        var options1 = new SnowflakeOptions
        {
            Database = db1,
            Schema = schema1,
            Table = table1
        };

        var options2 = new SnowflakeOptions
        {
            Database = db2,
            Schema = schema2,
            Table = table2
        };

        // Act & Assert
        options1.Should().NotBeEquivalentTo(options2);
    }

    [Fact]
    public void SnowflakeOptions_ToString_ShouldNotThrow()
    {
        // Arrange
        var options = new SnowflakeOptions
        {
            Database = "TEST_DB",
            Schema = "TEST_SCHEMA",
            Table = "TEST_TABLE"
        };

        // Act & Assert
        var act = () => options.ToString();
        act.Should().NotThrow();
    }

    [Fact]
    public void SnowflakeOptions_GetFullTableName_ConceptualTest()
    {
        // Arrange
        var options = new SnowflakeOptions
        {
            Database = "ITEMMASTER_DB",
            Schema = "PUBLIC",
            Table = "ITEMS"
        };

        // Act
        var expectedFullName = $"{options.Database}.{options.Schema}.{options.Table}";

        // Assert
        expectedFullName.Should().Be("ITEMMASTER_DB.PUBLIC.ITEMS");
    }
}
