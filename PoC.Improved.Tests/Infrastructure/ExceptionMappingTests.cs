using PoC.Improved.Domain;
using PoC.Improved.Infrastructure.ExceptionMapping;
using Polly.Timeout;

namespace PoC.Improved.Tests.Infrastructure;

public class TimeoutMapperTests
{
    private readonly TimeoutMapper _sut = new();

    [Theory]
    [MemberData(nameof(TimeoutExceptions))]
    public void CanMap_returns_true_for_every_timeout_flavor(Exception ex)
        => Assert.True(_sut.CanMap(ex));

    public static IEnumerable<object[]> TimeoutExceptions() => new[]
    {
        new object[] { new TaskCanceledException() },
        new object[] { new OperationCanceledException() },
        new object[] { new TimeoutException() },
        new object[] { new TimeoutRejectedException() },
    };

    [Fact]
    public void CanMap_returns_false_for_unrelated_exception()
        => Assert.False(_sut.CanMap(new InvalidOperationException()));

    [Fact]
    public void Map_returns_OperationTimeoutException_with_operation_in_message()
    {
        var domain = _sut.Map(new TimeoutException(), "listing folders");

        var timeout = Assert.IsType<OperationTimeoutException>(domain);
        Assert.Contains("listing folders", timeout.UserMessage);
        Assert.Equal(408, timeout.StatusCode);
    }
}

public class HttpMapperTests
{
    private readonly HttpMapper _sut = new();

    [Fact]
    public void CanMap_returns_true_for_HttpRequestException()
        => Assert.True(_sut.CanMap(new HttpRequestException()));

    [Fact]
    public void CanMap_returns_false_for_other_types()
        => Assert.False(_sut.CanMap(new InvalidOperationException()));

    [Fact]
    public void Map_returns_ExternalServiceException_with_HTTP_service_name()
    {
        var domain = _sut.Map(new HttpRequestException(), "calling X");

        var ext = Assert.IsType<ExternalServiceException>(domain);
        Assert.Equal("HTTP", ext.Service);
        Assert.Equal(503, ext.StatusCode);
        Assert.Contains("calling X", ext.UserMessage);
    }
}

public class ArgumentMapperTests
{
    private readonly ArgumentMapper _sut = new();

    [Fact]
    public void CanMap_returns_true_for_ArgumentException()
        => Assert.True(_sut.CanMap(new ArgumentException("bad arg")));

    [Fact]
    public void Map_returns_BadInputException_with_original_message()
    {
        var domain = _sut.Map(new ArgumentException("missing field"), "op");

        var bad = Assert.IsType<BadInputException>(domain);
        Assert.Equal(400, bad.StatusCode);
        Assert.Contains("missing field", bad.UserMessage);
    }
}

public class S3LikeMapperTests
{
    private readonly S3LikeMapper _sut = new();

    private sealed class AmazonS3Exception : Exception
    {
        public AmazonS3Exception(string message) : base(message) { }
    }

    [Fact]
    public void CanMap_matches_by_type_name_only()
        => Assert.True(_sut.CanMap(new AmazonS3Exception("denied")));

    [Fact]
    public void CanMap_returns_false_for_other_types()
        => Assert.False(_sut.CanMap(new HttpRequestException()));

    [Fact]
    public void Map_returns_ExternalServiceException_with_S3_service_name()
    {
        var domain = _sut.Map(new AmazonS3Exception("denied"), "uploading");

        var ext = Assert.IsType<ExternalServiceException>(domain);
        Assert.Equal("S3", ext.Service);
    }
}

public class ExceptionMapperRegistryTests
{
    [Fact]
    public void Map_uses_first_mapper_that_matches()
    {
        var first = Substitute.For<IExceptionMapper>();
        var second = Substitute.For<IExceptionMapper>();
        var ex = new InvalidOperationException();
        first.CanMap(ex).Returns(false);
        second.CanMap(ex).Returns(true);
        second.Map(ex, "op").Returns(new BadInputException("ok"));

        var sut = new ExceptionMapperRegistry(new[] { first, second });

        var result = sut.Map(ex, "op");

        Assert.IsType<BadInputException>(result);
        first.DidNotReceive().Map(Arg.Any<Exception>(), Arg.Any<string>());
        second.Received(1).Map(ex, "op");
    }

    [Fact]
    public void Map_falls_back_to_UnhandledExternalException_when_no_mapper_matches()
    {
        var mapper = Substitute.For<IExceptionMapper>();
        mapper.CanMap(Arg.Any<Exception>()).Returns(false);
        var sut = new ExceptionMapperRegistry(new[] { mapper });

        var result = sut.Map(new Exception("boom"), "op");

        var unhandled = Assert.IsType<UnhandledExternalException>(result);
        Assert.Contains("op", unhandled.UserMessage);
        Assert.Contains("boom", unhandled.UserMessage);
    }

    [Fact]
    public void Map_falls_back_when_registry_is_empty()
    {
        var sut = new ExceptionMapperRegistry(Array.Empty<IExceptionMapper>());

        var result = sut.Map(new Exception("x"), "op");

        Assert.IsType<UnhandledExternalException>(result);
    }
}
