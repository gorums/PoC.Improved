using FluentValidation;

namespace PoC.Improved.Application.Folders;

public sealed class GetFolderValidator : AbstractValidator<GetFolderQuery>
{
    public GetFolderValidator()
    {
        RuleFor(q => q.Feature).NotEmpty().MaximumLength(64);
        RuleFor(q => q.Year).InclusiveBetween(2000, 2100);
    }
}
