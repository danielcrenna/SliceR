using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests.Authorization;

public class AuthorizationFailedExceptionTests
{
    [Fact]
    public void Constructor_WithRequirementAndErrors_SetsProperties()
    {
        // Arrange
        const string requirement = "test-policy";
        var errors = new[] { "Error 1", "Error 2" };

        // Act
        var exception = new AuthorizationFailedException(requirement, errors);

        // Assert
        exception.FailedRequirement.Should().Be(requirement);
        exception.Errors.Should().BeEquivalentTo(errors);
        exception.Message.Should().Contain(requirement);
    }

    [Fact]
    public void Constructor_WithNullErrors_SetsEmptyErrorsArray()
    {
        // Arrange & Act
        var exception = new AuthorizationFailedException("test-policy", (string[]?)null!);

        // Assert
        exception.Errors.Should().NotBeNull();
        exception.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyErrors_SetsEmptyErrorsArray()
    {
        // Arrange & Act
        var exception = new AuthorizationFailedException("test-policy", []);

        // Assert
        exception.Errors.Should().NotBeNull();
        exception.Errors.Should().BeEmpty();
    }
}
