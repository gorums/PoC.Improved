using FluentValidation;
using FluentValidation.Results;
using PoC.Improved.Application.Cqrs;
using PoC.Improved.Application.Behaviors;
using PoC.Improved.Domain;

namespace PoC.Improved.Tests.Application.Behaviors;

public sealed record ValidationDummyRequest(string Value) : IRequest<string>;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Passes_through_when_no_validators_registered()
    {
        var sut = new ValidationBehavior<ValidationDummyRequest, string>(
            Array.Empty<IValidator<ValidationDummyRequest>>());
        RequestHandlerDelegate<string> next = () => Task.FromResult("ok");

        var result = await sut.Handle(new ValidationDummyRequest("x"), next, CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Passes_through_when_all_validators_succeed()
    {
        var validator = Substitute.For<IValidator<ValidationDummyRequest>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<ValidationDummyRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        var sut = new ValidationBehavior<ValidationDummyRequest, string>(new[] { validator });
        RequestHandlerDelegate<string> next = () => Task.FromResult("ok");

        var result = await sut.Handle(new ValidationDummyRequest("x"), next, CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Throws_BadInputException_when_any_validator_fails()
    {
        var validator = Substitute.For<IValidator<ValidationDummyRequest>>();
        var failures = new[]
        {
            new ValidationFailure("Value", "is required"),
            new ValidationFailure("Value", "must be short"),
        };
        validator.ValidateAsync(Arg.Any<ValidationContext<ValidationDummyRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));
        var sut = new ValidationBehavior<ValidationDummyRequest, string>(new[] { validator });
        var handlerCalled = false;
        RequestHandlerDelegate<string> next = () => { handlerCalled = true; return Task.FromResult("ok"); };

        var ex = await Assert.ThrowsAsync<BadInputException>(
            () => sut.Handle(new ValidationDummyRequest(""), next, CancellationToken.None));

        Assert.Contains("is required", ex.UserMessage);
        Assert.Contains("must be short", ex.UserMessage);
        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task Aggregates_failures_across_multiple_validators()
    {
        var v1 = Substitute.For<IValidator<ValidationDummyRequest>>();
        v1.ValidateAsync(Arg.Any<ValidationContext<ValidationDummyRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("a", "err-1") }));
        var v2 = Substitute.For<IValidator<ValidationDummyRequest>>();
        v2.ValidateAsync(Arg.Any<ValidationContext<ValidationDummyRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("b", "err-2") }));
        var sut = new ValidationBehavior<ValidationDummyRequest, string>(new[] { v1, v2 });
        RequestHandlerDelegate<string> next = () => Task.FromResult("ok");

        var ex = await Assert.ThrowsAsync<BadInputException>(
            () => sut.Handle(new ValidationDummyRequest(""), next, CancellationToken.None));

        Assert.Contains("err-1", ex.UserMessage);
        Assert.Contains("err-2", ex.UserMessage);
    }
}
