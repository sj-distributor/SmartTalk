using FluentValidation;
using SmartTalk.Core.Middlewares.FluentMessageValidator;
using SmartTalk.Messages.Commands.Security;

namespace SmartTalk.Core.Validators.Commands;

public class CreateUserAccountCommandValidator : FluentMessageValidator<CreateUserAccountCommand>
{
    public CreateUserAccountCommandValidator()
    {
        RuleFor(x => x.UserName)
            .NotNull().WithMessage("UserName cannot be null.")
            .NotEmpty().WithMessage("UserName cannot be empty.")
            .Matches("^[a-zA-Z]+$").WithMessage("UserName must contain only English letters.");
    }
}