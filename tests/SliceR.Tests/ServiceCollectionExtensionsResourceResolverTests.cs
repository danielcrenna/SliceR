using MediatR;
using SliceR.Authorization;
using Xunit;

namespace SliceR.Tests;

public class ServiceCollectionExtensionsResourceResolverTests
{
    public record TestDocument(Guid Id, string Content);
    
    public record TestRequest : IAuthorizedResourceRequest<TestDocument, Unit>
    {
        public Guid DocumentId { get; init; }
        public string PolicyName => "test.policy";
        public TestDocument? Resource { get; set; }
    }
    
    public record AnotherTestRequest : IAuthorizedResourceRequest<TestDocument, Unit>
    {
        public Guid DocumentId { get; init; }
        public string PolicyName => "another.policy";
        public TestDocument? Resource { get; set; }
    }
    
    [Fact]
    public void WithResourceResolvers_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        var result = services.WithResourceResolvers();
        
        // Assert
        result.Should().Be(services);
    }
    
    [Fact]
    public void WithResourceResolvers_DoesNotAddAnyServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var initialCount = services.Count;
        
        // Act
        services.WithResourceResolvers();
        
        // Assert
        services.Count.Should().Be(initialCount);
    }
    
    [Fact]
    public void WithResourceResolver_RegistersResourceResolver()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.WithResourceResolver<TestRequest, TestDocument>((request, sp, ct) =>
            Task.FromResult<TestDocument?>(new TestDocument(request.DocumentId, "Test")));
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var resolver = serviceProvider.GetService<IResourceResolver<TestRequest, TestDocument>>();
        resolver.Should().NotBeNull();
    }
    
    [Fact]
    public void WithResourceResolver_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        var result = services.WithResourceResolver<TestRequest, TestDocument>((request, sp, ct) =>
            Task.FromResult<TestDocument?>(new TestDocument(request.DocumentId, "Test")));
        
        // Assert
        result.Should().Be(services);
    }
    
    [Fact]
    public void WithResourceResolver_AllowsMethodChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        var result = services
            .WithResourceResolver<TestRequest, TestDocument>((request, sp, ct) =>
                Task.FromResult<TestDocument?>(new TestDocument(request.DocumentId, "Test1")))
            .WithResourceResolver<AnotherTestRequest, TestDocument>((request, sp, ct) =>
                Task.FromResult<TestDocument?>(new TestDocument(request.DocumentId, "Test2")));
        
        // Assert
        result.Should().Be(services);
        
        var serviceProvider = services.BuildServiceProvider();
        var resolver1 = serviceProvider.GetService<IResourceResolver<TestRequest, TestDocument>>();
        var resolver2 = serviceProvider.GetService<IResourceResolver<AnotherTestRequest, TestDocument>>();
        
        resolver1.Should().NotBeNull();
        resolver2.Should().NotBeNull();
    }
    
    [Fact]
    public async Task WithResourceResolver_RegisteredResolver_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.WithResourceResolver<TestRequest, TestDocument>((request, sp, ct) =>
            Task.FromResult<TestDocument?>(new TestDocument(request.DocumentId, "Resolved Content")));
        
        var serviceProvider = services.BuildServiceProvider();
        var resolver = serviceProvider.GetRequiredService<IResourceResolver<TestRequest, TestDocument>>();
        var request = new TestRequest { DocumentId = Guid.NewGuid() };
        
        // Act
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        
        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(request.DocumentId);
        result.Content.Should().Be("Resolved Content");
    }
    
    [Fact]
    public async Task WithResourceResolver_WithDependencyInjection_CanAccessServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestRepository, TestRepository>();
        services.WithResourceResolver<TestRequest, TestDocument>((request, sp, ct) =>
        {
            var repository = sp.GetRequiredService<ITestRepository>();
            return repository.GetDocumentAsync(request.DocumentId);
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var resolver = serviceProvider.GetRequiredService<IResourceResolver<TestRequest, TestDocument>>();
        var request = new TestRequest { DocumentId = Guid.NewGuid() };
        
        // Act
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        
        // Assert
        result.Should().NotBeNull();
        result!.Content.Should().Be("Repository Content");
    }
    
    public interface ITestRepository
    {
        Task<TestDocument?> GetDocumentAsync(Guid id);
    }
    
    public class TestRepository : ITestRepository
    {
        public Task<TestDocument?> GetDocumentAsync(Guid id)
        {
            return Task.FromResult<TestDocument?>(new TestDocument(id, "Repository Content"));
        }
    }
}