using PoC.Improved.Application.Folders;

namespace PoC.Improved.Tests.Application.Folders;

public class GetFoldersValidatorTests
{
    private readonly GetFoldersValidator _sut = new();

    [Fact]
    public void Empty_feature_fails_validation()
    {
        var result = _sut.Validate(new GetFoldersQuery(""));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void Feature_longer_than_64_chars_fails_validation()
    {
        var result = _sut.Validate(new GetFoldersQuery(new string('x', 65)));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_feature_passes()
        => Assert.True(_sut.Validate(new GetFoldersQuery("photos")).IsValid);
}
