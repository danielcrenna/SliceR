using System.Reflection;
using MediatR;
using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests.Authorization;

public class GetResourceTypeTests
{
    public record TestDocument(Guid Id, string Content);

    public record ValidResourceRequest : IAuthorizedResourceRequest<TestDocument, Unit>
    {
        public string PolicyName => "test.policy";
        public TestDocument? Resource { get; set; }
    }

    public record NonResourceRequest : IAuthorizedRequest<Unit>
    {
        public string PolicyName => "test.policy";
    }

    public record BaseResourceRequest : IAuthorizedResourceRequest<Unit>
    {
        public string PolicyName => "test.policy";
    }

    public interface ICustomInterface
    {
        string CustomProperty { get; }
    }

    public record RequestWithMultipleInterfaces : IAuthorizedResourceRequest<TestDocument, Unit>, ICustomInterface
    {
        public string PolicyName => "test.policy";
        public TestDocument? Resource { get; set; }
        public string CustomProperty => "Custom";
    }

    [Fact]
    public void GetResourceType_WithValidResourceRequest_ReturnsResourceType()
    {
        // Arrange
        var requestType = typeof(ValidResourceRequest);

        // Act
        var result = GetResourceTypeViaReflection(requestType);

        // Assert
        result.Should().Be(typeof(TestDocument));
    }

    [Fact]
    public void GetResourceType_WithNonResourceRequest_ReturnsNull()
    {
        // Arrange
        var requestType = typeof(NonResourceRequest);

        // Act
        var result = GetResourceTypeViaReflection(requestType);

        // Assert
        result.Should().Be(null);
    }

    [Fact]
    public void GetResourceType_WithBaseResourceRequest_ReturnsNull()
    {
        // Arrange
        var requestType = typeof(BaseResourceRequest);

        // Act
        var result = GetResourceTypeViaReflection(requestType);

        // Assert
        result.Should().Be(null);
    }

    [Fact]
    public void GetResourceType_WithRequestHavingMultipleInterfaces_ReturnsCorrectResourceType()
    {
        // Arrange
        var requestType = typeof(RequestWithMultipleInterfaces);

        // Act
        var result = GetResourceTypeViaReflection(requestType);

        // Assert
        result.Should().Be(typeof(TestDocument));
    }

    [Fact]
    public void GetResourceType_WithStringType_ReturnsNull()
    {
        // Arrange
        var requestType = typeof(string);

        // Act
        var result = GetResourceTypeViaReflection(requestType);

        // Assert
        result.Should().Be(null);
    }

    [Fact]
    public void GetResourceType_WithObjectType_ReturnsNull()
    {
        // Arrange
        var requestType = typeof(object);

        // Act
        var result = GetResourceTypeViaReflection(requestType);

        // Assert
        result.Should().Be(null);
    }

    private static Type? GetResourceTypeViaReflection(Type requestType)
    {
        // Use reflection to call the private GetResourceType method
        var authorizationBehaviorType = typeof(AuthorizationBehavior<,>).MakeGenericType(typeof(ValidResourceRequest), typeof(Unit));
        var method = authorizationBehaviorType.GetMethod("GetResourceType", BindingFlags.NonPublic | BindingFlags.Static);
        return (Type?)method?.Invoke(null, [requestType]);
    }
}
