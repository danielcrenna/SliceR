# SliceR

SliceR is a lightweight library that integrates MediatR with FluentValidation and ASP.NET Core Authorization to enable clean, vertical "slicing" of application features. This approach helps maintain separation of concerns while ensuring proper validation and authorization across your application.

## Features

- **MediatR Integration**: Seamlessly works with MediatR for command and query handling
- **Automated Validation**: Integrates FluentValidation to validate commands and queries before they're handled
- **Policy-Based Authorization**: Enforces authorization policies on requests
- **Resource-Based Authorization**: Supports authorization against specific resources
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

services.AddSliceR(typeof(YourStartupClass).Assembly);
```

This will register all MediatR handlers, validators, and authorization components.

### 2. Create Authorized Requests

For requests requiring authentication only:

```csharp
public record GetUserDataQuery : IAuthorizedRequest<UserDataResponse>
{
    public string? UserId { get; init; }
    
    // No specific policy, just authentication
    public string? PolicyName => null;
}
```

For requests requiring specific authorization policies:

```csharp
public record DeleteUserCommand : IAuthorizedRequest<bool>
{
    public string UserId { get; init; }
    
    // Require the "users.delete" policy
    public string PolicyName => "users.delete";
}
```

### 3. Resource-Based Authorization

For operations on specific resources:

```csharp
public record UpdateDocumentCommand : IAuthorizedResourceRequest<Document, Unit>
{
    public string DocumentId { get; init; }
    public string NewContent { get; init; }
    
    // The resource being accessed
    public Document Resource { get; init; }
    
    // The policy to check
    public string PolicyName => "documents.update";
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