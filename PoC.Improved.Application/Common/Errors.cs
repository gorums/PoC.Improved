using FluentResults;

namespace PoC.Improved.Application.Common;

/// <summary>
/// Categorized errors. The Api layer pattern-matches on subclass to set the HTTP status.
/// Code is a stable string identifier (e.g. "Folder.NotFound") usable by clients.
/// </summary>
public abstract class CategorizedError : Error
{
    protected CategorizedError(string code, string message) : base(message)
    {
        Code = code;
        Metadata["Code"] = code;
    }

    public string Code { get; }
}

public sealed class NotFoundError : CategorizedError
{
    public NotFoundError(string code, string message) : base(code, message) { }
}

public sealed class ValidationError : CategorizedError
{
    public ValidationError(string code, string message) : base(code, message) { }
}

public sealed class ConflictError : CategorizedError
{
    public ConflictError(string code, string message) : base(code, message) { }
}

public sealed class UnauthorizedError : CategorizedError
{
    public UnauthorizedError(string code, string message) : base(code, message) { }
}

public sealed class ForbiddenError : CategorizedError
{
    public ForbiddenError(string code, string message) : base(code, message) { }
}
