using FluentValidation;

namespace LoanOptimizer.Input;

public record OverpaymentInput
{
    public DateTime Date { get; init; }
    public decimal Amount { get; init; }
}

public class OverpaymentInputValidator : AbstractValidator<OverpaymentInput>
{
    public OverpaymentInputValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage("Date is required.");
        
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be a positive non-zero value.");
    }
}
