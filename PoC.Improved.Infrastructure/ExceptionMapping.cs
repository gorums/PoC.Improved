using PoC.Improved.Domain;
using Polly.Timeout;

namespace PoC.Improved.Infrastructure.ExceptionMapping;

/// <summary>
/// Maps a single family of infrastructure exceptions to a DomainException.
/// Adding support for a new SDK = adding a new IExceptionMapper, no central switch to edit.
/// </summary>
public interface IExceptionMapper
{
    bool CanMap(Exception ex);
    DomainException Map(Exception ex, string operationDescription);
}

public interface IExceptionMapperRegistry
{
    DomainException Map(Exception ex, string operationDescription);
}

public sealed class ExceptionMapperRegistry : IExceptionMapperRegistry
{
    private readonly IReadOnlyList<IExceptionMapper> _mappers;

    public ExceptionMapperRegistry(IEnumerable<IExceptionMapper> mappers)
    {
        _mappers = mappers.ToList();
    }

    public DomainException Map(Exception ex, string operationDescription)
    {
        foreach (var mapper in _mappers)
        {
            if (mapper.CanMap(ex))
                return mapper.Map(ex, operationDescription);
        }
        return new UnhandledExternalException(
            $"Unexpected failure while {operationDescription}: {ex.Message}", ex);
    }
}

// ----- Concrete mappers -----
// Each one is tiny, testable in isolation, and easy to swap.

public sealed class TimeoutMapper : IExceptionMapper
{
    public bool CanMap(Exception ex) =>
        ex is TaskCanceledException
           or OperationCanceledException
           or TimeoutException
           or TimeoutRejectedException;

    public DomainException Map(Exception ex, string op) =>
        new OperationTimeoutException($"Timeout while {op}.", ex);
}

public sealed class HttpMapper : IExceptionMapper
{
    public bool CanMap(Exception ex) => ex is HttpRequestException;

    public DomainException Map(Exception ex, string op) =>
        new ExternalServiceException("HTTP", $"Upstream HTTP call failed while {op}.", ex);
}

public sealed class ArgumentMapper : IExceptionMapper
{
    public bool CanMap(Exception ex) => ex is ArgumentException;

    public DomainException Map(Exception ex, string op) =>
        new BadInputException(ex.Message, ex);
}

// Example of a provider-specific mapper - lives next to the provider, not in a central file.
// Could match by exception type name to avoid pulling AWSSDK references into Domain.
public sealed class S3LikeMapper : IExceptionMapper
{
    public bool CanMap(Exception ex) =>
        ex.GetType().Name == "AmazonS3Exception";

    public DomainException Map(Exception ex, string op) =>
        new ExternalServiceException("S3", $"S3 operation failed while {op}.", ex);
}
