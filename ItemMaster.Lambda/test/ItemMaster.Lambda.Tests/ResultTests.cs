using FluentAssertions;
using ItemMaster.Shared;
using Xunit;

namespace ItemMaster.Lambda.Tests;

public class ResultTests
{
  [Fact]
  public void Success_ShouldCreateSuccessfulResult()
  {
    // Arrange & Act
    var result = Result.Success();

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.ErrorMessage.Should().BeNull();
  }

  [Fact]
  public void Failure_WithMessage_ShouldCreateFailedResult()
  {
    // Arrange
    var errorMessage = "Test error";

    // Act
    var result = Result.Failure(errorMessage);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.ErrorMessage.Should().Be(errorMessage);
  }
}

public class ResultGenericTests
{
  [Fact]
  public void Success_WithValue_ShouldCreateSuccessfulResult()
  {
    // Arrange
    var value = 42;

    // Act
    var result = Result<int>.Success(value);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().Be(value);
    result.ErrorMessage.Should().BeNull();
  }

  [Fact]
  public void Failure_WithMessage_ShouldCreateFailedResult()
  {
    // Arrange
    var errorMessage = "Test error";

    // Act
    var result = Result<int>.Failure(errorMessage);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Value.Should().Be(0);
    result.ErrorMessage.Should().Be(errorMessage);
  }

  [Fact]
  public void Success_WithObject_ShouldReturnCorrectValue()
  {
    // Arrange
    var value = new { Name = "Test", Id = 1 };

    // Act
    var result = Result<object>.Success(value);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().Be(value);
  }
}

