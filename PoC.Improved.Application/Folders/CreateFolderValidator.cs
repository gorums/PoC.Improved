using FluentValidation;

namespace PoC.Improved.Application.Folders;

public sealed class CreateFolderValidator : AbstractValidator<CreateFolderCommand>
{
    public CreateFolderValidator()
    {
        RuleFor(c => c.Feature).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Year).InclusiveBetween(2000, 2100);
    }
}
