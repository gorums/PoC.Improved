using FluentResults;
using PoC.Improved.Application.Common;

namespace PoC.Improved.Tests.Application.Common;

public class ErrorTests
{
    [Fact]
    public void NotFoundError_carries_code_and_message()
    {
        var error = new NotFoundError("Folder.NotFound", "missing");

        Assert.Equal("Folder.NotFound", error.Code);
        Assert.Equal("missing", error.Message);
        Assert.Equal("Folder.NotFound", error.Metadata["Code"]);
    }

    [Fact]
    public void Result_Fail_with_NotFoundError_is_failed_result()
    {
        var result = Result.Fail<int>(new NotFoundError("X", "no"));

        Assert.True(result.IsFailed);
        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.IsType<NotFoundError>(error);
    }

    [Fact]
    public void Result_Ok_carries_value()
    {
        var result = Result.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Theory]
    [MemberData(nameof(ErrorTypes))]
    public void All_categorized_errors_expose_code_and_message(CategorizedError error)
    {
        Assert.Equal("X.Y", error.Code);
        Assert.Equal("msg", error.Message);
    }

    public static IEnumerable<object[]> ErrorTypes() => new[]
    {
        new object[] { new NotFoundError("X.Y", "msg") },
        new object[] { new ValidationError("X.Y", "msg") },
        new object[] { new ConflictError("X.Y", "msg") },
        new object[] { new UnauthorizedError("X.Y", "msg") },
        new object[] { new ForbiddenError("X.Y", "msg") },
    };
}
