using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SliceR.Authorization;
using SliceR.Validation;
using Xunit;

namespace SliceR.Tests;

public class ServiceCollectionExtensionsTests
{
    public record TestRequest(string Name) : IRequest<string>;

    // ReSharper disable once UnusedMember.Global
    public class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    internal class InternalTestRequestValidator : AbstractValidator<TestRequest>
    {
        public InternalTestRequestValidator()
        {
            RuleFor(x => x.Name).MinimumLength(3);
        }
    }

    public class TestRequestHandler : IRequestHandler<TestRequest, string>
    {
        public Task<string> Handle(TestRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.FromResult($"Hello, {request.Name}");
        }
    }

    [Fact]
    public void AddSliceR_RegistersValidatorAndMediatR()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly);

        // Assert
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetService<IMediator>();
        Assert.NotNull(mediator);

        var validators = provider.GetServices<IValidator<TestRequest>>();
        Assert.NotEmpty(validators);

        var pipelines = provider.GetServices<IPipelineBehavior<TestRequest, string>>();
        Assert.NotEmpty(pipelines);

        var authProvider = provider.GetService<IAuthorizationProvider>();
        Assert.NotNull(authProvider);
    }

    [Fact]
    public async Task AddSliceR_RegistersWorkingMediatRPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly);

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new TestRequest("World"));

        // Assert
        Assert.Equal("Hello, World", result);
    }

    [Fact]
    public void AddSliceR_WithNullAssembly_UsesCallingAssembly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(); // Uses null default, which triggers GetCallingAssembly

        // Assert
        var provider = services.BuildServiceProvider();
        var validators = provider.GetServices<IValidator<TestRequest>>();
        Assert.NotEmpty(validators);
    }

    [Fact]
    public void AddSliceR_WithTransientLifetime_RegistersTransientServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly, ServiceLifetime.Transient);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(TestRequestValidator));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddSliceR_WithSingletonLifetime_RegistersSingletonServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly, ServiceLifetime.Singleton);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(TestRequestValidator));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddSliceR_WithIncludeInternalTypesTrue_RegistersInternalValidators()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly, includeInternalTypes: true);

        // Assert
        var provider = services.BuildServiceProvider();
        var validators = provider.GetServices<IValidator<TestRequest>>();
        Assert.Equal(2, validators.Count()); // Public and internal validators
    }

    [Fact]
    public void AddSliceR_WithIncludeInternalTypesFalse_ExcludesInternalValidators()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly, includeInternalTypes: false);

        // Assert
        var provider = services.BuildServiceProvider();
        var validators = provider.GetServices<IValidator<TestRequest>>();
        Assert.Single(validators); // Only public validator
    }

    [Fact]
    public void AddSliceR_RegistersAuthorizationServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly);

        // Assert
        var provider = services.BuildServiceProvider();
        var authMiddlewareResultHandler = provider.GetService<IAuthorizationMiddlewareResultHandler>();
        Assert.NotNull(authMiddlewareResultHandler);
        Assert.IsType<SliceR.Authorization.AuthorizationMiddlewareResultHandler>(authMiddlewareResultHandler);

        var authProvider = provider.GetService<IAuthorizationProvider>();
        Assert.NotNull(authProvider);
    }

    [Fact]
    public void AddSliceR_RegistersHttpContextAccessor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly);

        // Assert
        var provider = services.BuildServiceProvider();
        var httpContextAccessor = provider.GetService<IHttpContextAccessor>();
        Assert.NotNull(httpContextAccessor);
    }

    [Fact]
    public void AddSliceR_RegistersControllerFilters()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly);

        // Assert
        var provider = services.BuildServiceProvider();
        var mvcOptions = provider.GetRequiredService<IOptions<MvcOptions>>();
        var filters = mvcOptions.Value.Filters;

        Assert.Contains(filters, f => f is TypeFilterAttribute tfa && tfa.ImplementationType == typeof(ValidationExceptionFilter));
        Assert.Contains(filters, f => f is TypeFilterAttribute tfa && tfa.ImplementationType == typeof(AuthorizationExceptionFilter));
    }

    [Fact]
    public void AddSliceR_RegistersPipelineBehaviors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly);

        // Assert
        var provider = services.BuildServiceProvider();
        var pipelines = provider.GetServices<IPipelineBehavior<TestRequest, string>>();

        var validationBehaviorType = typeof(ValidationBehavior<,>).MakeGenericType(typeof(TestRequest), typeof(string));

        Assert.Contains(pipelines, p => p.GetType() == validationBehaviorType);

        // AuthorizationBehavior is registered but won't be in the pipelines for TestRequest 
        // since TestRequest doesn't implement IAuthorizedRequest. Let's verify it's registered though.
        var authBehaviorDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IPipelineBehavior<,>) &&
            s.ImplementationType != null &&
            s.ImplementationType.GetGenericTypeDefinition() == typeof(AuthorizationBehavior<,>));
        Assert.NotNull(authBehaviorDescriptor);
    }

    [Fact]
    public void AddSliceR_MultipleCalls_DoesNotDuplicateRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly);
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly);

        // Assert
        var provider = services.BuildServiceProvider();
        var validators = provider.GetServices<IValidator<TestRequest>>();

        // Should have exactly 2 validators (public and internal), not 4
        Assert.Equal(2, validators.Count());
    }

    [Fact]
    public void AddSliceR_ServiceLifetimesAreRespected()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthorization();

        // Act
        services.AddSliceR(typeof(ServiceCollectionExtensionsTests).Assembly);

        // Assert
        // AuthorizationMiddlewareResultHandler should be singleton - look for our specific registration
        var authMiddlewareDescriptor = services.LastOrDefault(s =>
            s.ServiceType == typeof(IAuthorizationMiddlewareResultHandler) &&
            s.ImplementationType == typeof(SliceR.Authorization.AuthorizationMiddlewareResultHandler));
        Assert.NotNull(authMiddlewareDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, authMiddlewareDescriptor.Lifetime);

        // AuthorizationProvider should be scoped
        var authProviderDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IAuthorizationProvider));
        Assert.NotNull(authProviderDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, authProviderDescriptor.Lifetime);

        // Pipeline behaviors should be transient
        var pipelineDescriptors = services.Where(s => s.ServiceType == typeof(IPipelineBehavior<,>));
        Assert.All(pipelineDescriptors, d => Assert.Equal(ServiceLifetime.Transient, d.Lifetime));
    }
}
