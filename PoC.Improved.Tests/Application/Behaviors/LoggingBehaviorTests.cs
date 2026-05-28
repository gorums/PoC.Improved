using MediatR;
using Microsoft.Extensions.Logging;
using PoC.Improved.Application.Behaviors;

namespace PoC.Improved.Tests.Application.Behaviors;

public sealed record LoggingDummyRequest : IRequest<string>;

public class LoggingBehaviorTests
{
    [Fact]
    public async Task Returns_handler_result_on_success()
    {
        var logger = Substitute.For<ILogger<LoggingBehavior<LoggingDummyRequest, string>>>();
        var sut = new LoggingBehavior<LoggingDummyRequest, string>(logger);
        RequestHandlerDelegate<string> next = () => Task.FromResult("done");

        var result = await sut.Handle(new LoggingDummyRequest(), next, CancellationToken.None);

        Assert.Equal("done", result);
    }

    [Fact]
    public async Task Rethrows_when_handler_throws()
    {
        var logger = Substitute.For<ILogger<LoggingBehavior<LoggingDummyRequest, string>>>();
        var sut = new LoggingBehavior<LoggingDummyRequest, string>(logger);
        RequestHandlerDelegate<string> next = () => throw new InvalidOperationException("boom");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Handle(new LoggingDummyRequest(), next, CancellationToken.None));
    }
}
