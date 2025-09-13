# SliceR

SliceR is a lightweight library that integrates MediatR with FluentValidation and ASP.NET Core Authorization to enable clean, vertical "slicing" of application features. This approach helps maintain separation of concerns while ensuring proper validation and authorization across your application.

## Features

- **MediatR Integration**: Seamlessly works with MediatR for command and query handling
- **Automated Validation**: Integrates FluentValidation to validate commands and queries before they're handled
- **Policy-Based Authorization**: Enforces authorization policies on requests
- **Resource-Based Authorization**: Supports authorization against specific resources
- **Attribute-Based Authorization**: Declarative authorization using attributes
- **ASP.NET Core Integration**: Provides middleware and exception filters for web applications
- **Minimal Configuration**: Set up with a single extension method

## Installation

Add a reference to the SliceR project in your solution:

```xml
<ProjectReference Include="..\path\to\SliceR\src\SliceR\SliceR.csproj" />
```

## Getting Started

### 1. Register Services

In your `Program.cs` or `Startup.cs`:

```csharp
using SliceR;

// ...

services.AddSliceR(typeof(YourStartupClass).Assembly)
    .WithResourceResolvers(); // Enable automatic resource resolution
```

This will register all handlers, validators, and authorization components. The `WithResourceResolvers()` call enables automatic resource resolution for resource-based authorization.

### 2. Create Authorized Requests

#### Using Attributes

The simplest way to add authorization is using attributes:

```csharp
// Authentication only
[Authenticated]
public record GetUserDataQuery(string? UserId) : IRequest<UserDataResponse>;

// Alternative: using [Authorized] without parameters
[Authorized]
public record GetProfileQuery(string? UserId) : IRequest<ProfileResponse>;

// With specific policy
[Authorized("users.delete")]
public record DeleteUserCommand(string UserId) : IRequest<bool>;

// With resource resolution
[Authorized("documents.update")]
[ResolveResource(typeof(DocumentResourceResolver))]
public record UpdateDocumentCommand(Guid DocumentId, string NewContent) : IRequest<Unit>
{
    public Document? Resource { get; set; }
}
```

#### Using Interfaces

Alternatively, use interfaces for more control:

```csharp
// Authentication only
public record GetUserDataQuery : IAuthenticatedRequest<UserDataResponse>
{
    public string? UserId { get; init; }
}

// With specific policy
public record DeleteUserCommand : IAuthorizedRequest<bool>
{
    public string UserId { get; init; }
    public string PolicyName => "users.delete";
}
```

### 3. Resource-Based Authorization

Resources can be resolved automatically before authorization:

**With Attributes (v1.2.0+)**

```csharp
[Authorized("documents.update")]
[ResolveResource(typeof(DocumentResourceResolver))]
public record UpdateDocumentCommand(Guid DocumentId) : IRequest<Unit>
{
    public Document? Resource { get; set; }
}

public class DocumentResourceResolver : IResourceResolver<UpdateDocumentCommand, Document>
{
    private readonly IDocumentRepository _repository;

    public DocumentResourceResolver(IDocumentRepository repository) => _repository = repository;

    public async Task<Document?> ResolveAsync(UpdateDocumentCommand request, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(request.DocumentId);
    }
}
```

**With Interfaces**

```csharp
public record UpdateDocumentCommand : IAuthorizedResourceRequest<Document, Unit>
{
    public Guid DocumentId { get; init; }
    public Document? Resource { get; set; }
    public string PolicyName => "documents.update";
}
```

**Convention-Based Registration**

```csharp
services.AddSliceR(typeof(YourStartupClass).Assembly)
    .WithResourceResolver<UpdateDocumentCommand, Document>(async (request, serviceProvider, cancellationToken) =>
    {
        var repository = serviceProvider.GetRequiredService<IDocumentRepository>();
        return await repository.GetByIdAsync(request.DocumentId);
    })
    .WithResourceResolver<DeleteUserCommand, User>(async (request, serviceProvider, cancellationToken) =>
    {
        var userService = serviceProvider.GetRequiredService<IUserService>();
        return await userService.GetUserByIdAsync(request.UserId);
    });
```

With automatic resource resolution, your controllers become much simpler:

```csharp
[HttpPut("/documents/{id}")]
public async Task<IActionResult> UpdateDocument(Guid id, UpdateDocumentRequest request)
{
    var command = new UpdateDocumentCommand 
    { 
        DocumentId = id,
        NewContent = request.Content
        // Resource will be resolved automatically!
    };
    
    return Ok(await _mediator.Send(command));
}
```

### 4. Adding Validation

Create validators using FluentValidation:

```csharp
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
```

## How It Works

SliceR adds pipeline behaviors to MediatR:

1. **Validation Behavior**: Automatically validates requests using registered FluentValidation validators
2. **Authorization Behavior**: Ensures users are authorized to perform the requested operation

When a request fails validation or authorization, appropriate exceptions are thrown and handled by the registered exception filters.

## Working with MVC Controllers

### Automatic Exception Handling

When you call `AddSliceR()`, it automatically registers two exception filters that convert validation and authorization exceptions into standard ProblemDetails responses:

1. **ValidationExceptionFilter**: Catches `ValidationException` from FluentValidation and returns a 400 Bad Request with `ValidationProblemDetails`
2. **AuthorizationExceptionFilter**: Catches `AuthorizationFailedException` and returns either 401 Unauthorized or 403 Forbidden with `ProblemDetails`

These filters are automatically added to your MVC pipeline, so you don't need any additional configuration. Simply send your MediatR commands/queries from your controllers:

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    
    public UsersController(IMediator mediator) => _mediator = mediator;
    
    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserCommand command)
    {
        // Validation and authorization happen automatically
        // If validation fails, returns 400 with ValidationProblemDetails
        // If authorization fails, returns 401/403 with ProblemDetails
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
```

### What Gets Converted to ProblemDetails

The exception filters handle the following scenarios:

**ValidationExceptionFilter:**
- Converts `ValidationException` to HTTP 400 Bad Request
- Returns `ValidationProblemDetails` with field-level errors
- Groups errors by property name for easy client-side processing

**AuthorizationExceptionFilter:**
- Converts `AuthorizationFailedException` to:
  - HTTP 401 Unauthorized when authentication is missing
  - HTTP 403 Forbidden when authorization policy fails
- Returns `ProblemDetails` with error details in extensions

### Example Response Bodies

Validation failure response:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Validation failed",
  "errors": {
    "Email": ["Email is required", "Email must be a valid email address"],
    "Password": ["Password must be at least 8 characters"]
  }
}
```

Authorization failure response:
```json
{
  "title": "Authorization Failed",
  "status": 403,
  "detail": "Failed requirement: users.delete",
  "errors": ["User does not have permission to delete users"]
}
```

### Important Notes for Controller Usage

- **No Try-Catch Needed**: The exception filters handle exceptions automatically
- **Consistent API Responses**: All validation and authorization failures return standard ProblemDetails
- **Works with API Controllers**: The filters are registered globally and work with all controllers

## Advanced Configuration

### Custom Authorization Provider

You can implement your own authorization provider:

```csharp
public class CustomAuthorizationProvider : IAuthorizationProvider
{
    // Implement authorization logic
    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, string policyName)
    {
        // Custom authorization logic
    }
    
    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, string policyName, object resource)
    {
        // Custom resource-based authorization logic
    }
}

// Register your custom provider
services.AddScoped<IAuthorizationProvider, CustomAuthorizationProvider>();
```

### Service Lifetime

You can customize the service lifetime when registering SliceR:

```csharp
services.AddSliceR(
    assembly: typeof(YourStartupClass).Assembly,
    lifetime: ServiceLifetime.Singleton,
    includeInternalTypes: true
);
```

## Benefits of the Vertical Slice Architecture

Using SliceR helps implement a vertical slice architecture where:

- Each feature is isolated with its own request, handler, validator, and authorization rules
- Cross-cutting concerns like validation and authorization are handled consistently
- Code organization follows feature boundaries rather than technical layers
- Features can be understood, tested, and maintained in isolation
