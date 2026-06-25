using FluentValidation;

namespace AutoInsurance.QuoteBuy.Application.Commands.CreateQuote;

public class CreateQuoteCommandValidator : AbstractValidator<CreateQuoteCommand>
{
    public CreateQuoteCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DateOfBirth).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ZipCode).NotEmpty().Length(5).Matches(@"^\d{5}$");
        RuleFor(x => x.State).NotEmpty().Length(2);
    }
}
