using FluentValidation;

namespace PoC.Improved.Application.Folders;

public sealed class GetFoldersValidator : AbstractValidator<GetFoldersQuery>
{
    public GetFoldersValidator()
    {
        RuleFor(q => q.Feature)
            .NotEmpty().WithMessage("feature is required")
            .MaximumLength(64);
    }
}
