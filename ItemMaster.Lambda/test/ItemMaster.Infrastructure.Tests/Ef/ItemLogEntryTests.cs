using FluentAssertions;
using ItemMaster.Infrastructure.Ef;
using Xunit;

namespace ItemMaster.Infrastructure.Tests.Ef;

public class ItemLogEntryTests
{
    public static IEnumerable<object[]> GetItemLogEntryTestData()
    {
        // Test case 1: Complete log entry
        yield return new object[]
        {
            new ItemLogEntry
            {
                Id = 1,
                Sku = "TEST-001",
                Source = "ApiGateway",
                RequestId = "req-123-456",
                TimestampUtc = DateTime.Parse("2023-10-15T10:30:00Z").ToUniversalTime()
            },
            "Complete log entry"
        };

        // Test case 2: Log entry with long SKU
        yield return new object[]
        {
            new ItemLogEntry
            {
                Id = 2,
                Sku = "VERY-LONG-SKU-NAME-WITH-MANY-CHARACTERS-12345",
                Source = "SQS",
                RequestId = "req-789-012",
                TimestampUtc = DateTime.Parse("2023-10-15T15:45:30Z").ToUniversalTime()
            },
            "Log entry with long SKU"
        };

        // Test case 3: Log entry with special characters
        yield return new object[]
        {
            new ItemLogEntry
            {
                Id = 3,
                Sku = "SKU-WITH-SPECIAL@CHARS#123",
                Source = "DirectApi",
                RequestId = "req-345-678-special",
                TimestampUtc = DateTime.Parse("2023-10-15T20:15:45Z").ToUniversalTime()
            },
            "Log entry with special characters"
        };
    }

    [Theory]
    [MemberData(nameof(GetItemLogEntryTestData))]
    public void ItemLogEntry_WithDifferentData_ShouldInitializeCorrectly(
        ItemLogEntry expectedEntry,
        string scenario)
    {
        // Arrange & Act
        var actualEntry = new ItemLogEntry
        {
            Id = expectedEntry.Id,
            Sku = expectedEntry.Sku,
            Source = expectedEntry.Source,
            RequestId = expectedEntry.RequestId,
            TimestampUtc = expectedEntry.TimestampUtc
        };

        // Assert
        actualEntry.Should().BeEquivalentTo(expectedEntry, scenario);
    }

    [Fact]
    public void ItemLogEntry_DefaultConstructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var entry = new ItemLogEntry();

        // Assert
        entry.Id.Should().Be(0);
        entry.Sku.Should().Be(string.Empty);
        entry.Source.Should().Be(string.Empty);
        entry.RequestId.Should().Be(string.Empty);
        entry.TimestampUtc.Should().Be(default);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(999999)]
    [InlineData(long.MaxValue)]
    public void ItemLogEntry_Id_ShouldBeSettableAndGettable(long id)
    {
        // Arrange & Act
        var entry = new ItemLogEntry { Id = id };

        // Assert
        entry.Id.Should().Be(id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SIMPLE-SKU")]
    [InlineData("SKU-WITH-NUMBERS-123")]
    [InlineData("SKU@WITH#SPECIAL$CHARS%")]
    [InlineData("A")]
    public void ItemLogEntry_Sku_ShouldBeSettableAndGettable(string sku)
    {
        // Arrange & Act
        var entry = new ItemLogEntry { Sku = sku };

        // Assert
        entry.Sku.Should().Be(sku);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ApiGateway")]
    [InlineData("SQS")]
    [InlineData("DirectApi")]
    [InlineData("BatchProcessor")]
    public void ItemLogEntry_Source_ShouldBeSettableAndGettable(string source)
    {
        // Arrange & Act
        var entry = new ItemLogEntry { Source = source };

        // Assert
        entry.Source.Should().Be(source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("req-123")]
    [InlineData("request-id-with-uuid-12345678-1234-1234-1234-123456789012")]
    [InlineData("simple-req")]
    [InlineData("req@with#special$chars")]
    public void ItemLogEntry_RequestId_ShouldBeSettableAndGettable(string requestId)
    {
        // Arrange & Act
        var entry = new ItemLogEntry { RequestId = requestId };

        // Assert
        entry.RequestId.Should().Be(requestId);
    }

    [Theory]
    [InlineData("2023-01-01T00:00:00Z")]
    [InlineData("2023-06-15T12:30:45Z")]
    [InlineData("2023-12-31T23:59:59Z")]
    public void ItemLogEntry_TimestampUtc_ShouldBeSettableAndGettable(string timestampString)
    {
        // Arrange
        var timestamp = DateTime.Parse(timestampString).ToUniversalTime();

        // Act
        var entry = new ItemLogEntry { TimestampUtc = timestamp };

        // Assert
        entry.TimestampUtc.Should().Be(timestamp);
        entry.TimestampUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ItemLogEntry_AllProperties_ShouldBeSettableAndGettable()
    {
        // Arrange
        var id = 42L;
        var sku = "TEST-SKU-123";
        var source = "ApiGateway";
        var requestId = "req-uuid-12345";
        var timestamp = DateTime.UtcNow;

        // Act
        var entry = new ItemLogEntry
        {
            Id = id,
            Sku = sku,
            Source = source,
            RequestId = requestId,
            TimestampUtc = timestamp
        };

        // Assert
        entry.Id.Should().Be(id);
        entry.Sku.Should().Be(sku);
        entry.Source.Should().Be(source);
        entry.RequestId.Should().Be(requestId);
        entry.TimestampUtc.Should().Be(timestamp);
    }

    [Fact]
    public void ItemLogEntry_WithNullValues_ShouldHandleGracefully()
    {
        // Arrange & Act
        var entry = new ItemLogEntry
        {
            Sku = null!,
            Source = null!,
            RequestId = null!
        };

        // Assert - Properties should be settable to null (though not recommended)
        entry.Sku.Should().BeNull();
        entry.Source.Should().BeNull();
        entry.RequestId.Should().BeNull();
    }

    [Fact]
    public void ItemLogEntry_Equality_ShouldWorkCorrectlyForSameValues()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var entry1 = new ItemLogEntry
        {
            Id = 1,
            Sku = "TEST-SKU",
            Source = "ApiGateway",
            RequestId = "req-123",
            TimestampUtc = timestamp
        };

        var entry2 = new ItemLogEntry
        {
            Id = 1,
            Sku = "TEST-SKU",
            Source = "ApiGateway",
            RequestId = "req-123",
            TimestampUtc = timestamp
        };

        // Act & Assert
        entry1.Should().BeEquivalentTo(entry2);
    }

    [Fact]
    public void ItemLogEntry_ToString_ShouldNotThrow()
    {
        // Arrange
        var entry = new ItemLogEntry
        {
            Id = 1,
            Sku = "TEST-SKU",
            Source = "ApiGateway",
            RequestId = "req-123",
            TimestampUtc = DateTime.UtcNow
        };

        // Act & Assert
        var act = () => entry.ToString();
        act.Should().NotThrow();
    }
}