using FluentValidation;
using MediatR;
using SliceR.Validation;
using Xunit;

namespace SliceR.Tests.Validation;

public class ValidationBehaviorTests
{
    public record TestRequest(string Name, int Age);

    public class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Age).GreaterThan(0);
        }
    }

    public class EmptyValidator : AbstractValidator<TestRequest>;

    public class FailingValidator : AbstractValidator<TestRequest>
    {
        public FailingValidator()
        {
            RuleFor(x => x.Name).Must(_ => false).WithMessage("Always fails");
        }
    }

    private readonly RequestHandlerDelegate<string> _nextMock =
        _ => Task.FromResult("Success");

    [Fact]
    public async Task Handle_WithNoValidators_CallsNext()
    {
        // Arrange
        var validators = Array.Empty<IValidator<TestRequest>>();
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("Test", 30);

        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None);

        // Assert
        result.Should().Be("Success");
    }

    [Fact]
    public async Task Handle_WithPassingValidator_CallsNext()
    {
        // Arrange
        var validators = new[] { new TestRequestValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("Test", 30);

        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None);

        // Assert
        result.Should().Be("Success");
    }

    [Fact]
    public async Task Handle_WithEmptyValidator_CallsNext()
    {
        // Arrange
        var validators = new[] { new EmptyValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("Test", 30);

        // Act
        var result = await behavior.Handle(request, _nextMock, CancellationToken.None);

        // Assert
        result.Should().Be("Success");
    }

    [Fact]
    public async Task Handle_WithFailingValidator_ThrowsValidationException()
    {
        // Arrange
        var validators = new[] { new FailingValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("Test", 30);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(request, _nextMock, CancellationToken.None));

        exception.Errors.Should().HaveCount(1);
        exception.Errors.First().ErrorMessage.Should().Be("Always fails");
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var validators = new[] { new TestRequestValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("", -5);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(request, _nextMock, CancellationToken.None));

        exception.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithMultipleValidators_ValidatesWithAll()
    {
        // Arrange
        var validators = new IValidator<TestRequest>[]
        {
            new TestRequestValidator(),
            new FailingValidator()
        };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("", -5);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(request, _nextMock, CancellationToken.None));

        exception.Errors.Should().HaveCountGreaterThan(2);
    }
}
