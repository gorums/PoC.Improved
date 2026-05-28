using PoC.Improved.Domain;

namespace PoC.Improved.Tests.Domain;

public class DomainExceptionTests
{
    [Fact]
    public void ExternalServiceException_uses_503_and_carries_service_name()
    {
        var ex = new ExternalServiceException("S3", "boom");

        Assert.Equal(503, ex.StatusCode);
        Assert.Equal("boom", ex.UserMessage);
        Assert.Equal("S3", ex.Service);
    }

    [Fact]
    public void OperationTimeoutException_uses_408()
    {
        var ex = new OperationTimeoutException("slow");
        Assert.Equal(408, ex.StatusCode);
    }

    [Fact]
    public void ResourceConflictException_uses_409()
    {
        var ex = new ResourceConflictException("conflict");
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public void BadInputException_uses_400()
    {
        var ex = new BadInputException("bad");
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void UnhandledExternalException_uses_500()
    {
        var ex = new UnhandledExternalException("oops");
        Assert.Equal(500, ex.StatusCode);
    }

    [Fact]
    public void Inner_exception_is_preserved()
    {
        var inner = new InvalidOperationException("root");
        var ex = new BadInputException("bad", inner);
        Assert.Same(inner, ex.InnerException);
    }
}
