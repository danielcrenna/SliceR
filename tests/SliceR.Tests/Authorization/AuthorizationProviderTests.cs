using System.Security.Claims;
using FakeItEasy;
using Microsoft.AspNetCore.Authorization;
using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests.Authorization;

public class AuthorizationProviderTests
{
    private readonly IAuthorizationService _authService = A.Fake<IAuthorizationService>();
    private readonly ClaimsPrincipal _user = new(new ClaimsIdentity("TestAuth"));

    [Fact]
    public async Task AuthorizeAsync_WithPolicyName_CallsAuthorizationService()
    {
        // Arrange
        A.CallTo(() => _authService.AuthorizeAsync(_user, null, "test-policy"))
            .Returns(AuthorizationResult.Success());

        var provider = new AuthorizationProvider(_authService);

        // Act
        var result = await provider.AuthorizeAsync(_user, "test-policy");

        // Assert
        result.Succeeded.Should().BeTrue();
        A.CallTo(() => _authService.AuthorizeAsync(_user, null, "test-policy"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AuthorizeAsync_WithResourceAndPolicyName_CallsAuthorizationService()
    {
        // Arrange
        var resource = new object();
        A.CallTo(() => _authService.AuthorizeAsync(_user, resource, "test-policy"))
            .Returns(AuthorizationResult.Success());

        var provider = new AuthorizationProvider(_authService);

        // Act
        var result = await provider.AuthorizeAsync(_user, "test-policy", resource);

        // Assert
        result.Succeeded.Should().BeTrue();
        A.CallTo(() => _authService.AuthorizeAsync(_user, resource, "test-policy"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AuthorizeAsync_WithFailedAuthorization_ReturnsFailedResult()
    {
        // Arrange
        var authFailure = AuthorizationFailure.Failed(
            [new AuthorizationFailureReason(A.Fake<IAuthorizationHandler>(), "Test error message")]);

        A.CallTo(() => _authService.AuthorizeAsync(_user, null, "test-policy"))
            .Returns(AuthorizationResult.Failed(authFailure));

        var provider = new AuthorizationProvider(_authService);

        // Act
        var result = await provider.AuthorizeAsync(_user, "test-policy");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.FailureReasons.Should().HaveCount(1);
        Assert.Equal("Test error message", result.Failure!.FailureReasons.First().Message);
    }
}
