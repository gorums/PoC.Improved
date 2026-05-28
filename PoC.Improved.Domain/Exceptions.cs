namespace PoC.Improved.Domain;

/// <summary>
/// Base for any exception that carries its own HTTP status + user-facing message.
/// The API middleware translates these without having to know about EF, AWS, HTTP, etc.
/// </summary>
public abstract class DomainException : Exception
{
    public int StatusCode { get; }
    public string UserMessage { get; }

    protected DomainException(int statusCode, string userMessage, Exception? inner = null)
        : base(userMessage, inner)
    {
        StatusCode = statusCode;
        UserMessage = userMessage;
    }
}

public sealed class ExternalServiceException : DomainException
{
    public string Service { get; }

    public ExternalServiceException(string service, string message, Exception? inner = null)
        : base(503, message, inner)
    {
        Service = service;
    }
}

public sealed class OperationTimeoutException : DomainException
{
    public OperationTimeoutException(string message, Exception? inner = null)
        : base(408, message, inner) { }
}

public sealed class ResourceConflictException : DomainException
{
    public ResourceConflictException(string message, Exception? inner = null)
        : base(409, message, inner) { }
}

public sealed class BadInputException : DomainException
{
    public BadInputException(string message, Exception? inner = null)
        : base(400, message, inner) { }
}

public sealed class UnhandledExternalException : DomainException
{
    public UnhandledExternalException(string message, Exception? inner = null)
        : base(500, message, inner) { }
}
