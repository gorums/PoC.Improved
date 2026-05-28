using FluentValidation;

namespace PoC.Improved.Application.Files;

public sealed class GetFileUrlValidator : AbstractValidator<GetFileUrlQuery>
{
    public GetFileUrlValidator()
    {
        RuleFor(q => q.Path)
            .NotEmpty().WithMessage("path is required");
    }
}
