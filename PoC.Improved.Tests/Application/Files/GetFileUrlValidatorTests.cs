using PoC.Improved.Application.Files;

namespace PoC.Improved.Tests.Application.Files;

public class GetFileUrlValidatorTests
{
    private readonly GetFileUrlValidator _sut = new();

    [Fact]
    public void Null_path_fails_validation()
        => Assert.False(_sut.Validate(new GetFileUrlQuery(null)).IsValid);

    [Fact]
    public void Empty_path_fails_validation()
        => Assert.False(_sut.Validate(new GetFileUrlQuery("")).IsValid);

    [Fact]
    public void Valid_path_passes()
        => Assert.True(_sut.Validate(new GetFileUrlQuery("a/b.jpg")).IsValid);
}
