using FluentValidation;

namespace LoanOptimizer.Input;

public record PaymentDayInput
{
    public DateTime Date { get; init; }
    public bool OnlyInstalments { get; init; }
    public decimal TotalBudget { get; init; }
}

public class PaymentDayInputValidator : AbstractValidator<PaymentDayInput>
{
    public PaymentDayInputValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage("Date is required.");

        RuleFor(x => x)
            .Must(HaveExactlyOneBudgetPropertyProvided)
            .WithMessage("Exactly one of OnlyInstalments or TotalBudget must be provided.");
        
        RuleFor(x => x.TotalBudget)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TotalBudget < 0)
            .WithMessage("TotalBudget cannot be a negative value.");
    }

    private static bool HaveExactlyOneBudgetPropertyProvided(PaymentDayInput input)
    {
        var propertiesSet = 0;
        
        if (input.OnlyInstalments)
        {
            propertiesSet++;
        }
        if (input.TotalBudget != default)
        {
            propertiesSet++;
        }

        return propertiesSet == 1;
    }
}
