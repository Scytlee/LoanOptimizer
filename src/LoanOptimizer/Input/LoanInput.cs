using FluentValidation;

namespace LoanOptimizer.Input;

public record LoanInput
{
    public decimal Amount { get; init; }
    public decimal YearlyInterestRate { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime FirstInstalmentDate { get; init; }
    public int NumberOfInstalments { get; init; }
    public IEnumerable<OverpaymentInput> Overpayments { get; init; } = Enumerable.Empty<OverpaymentInput>();
}

public class LoanInputValidator : AbstractValidator<LoanInput>
{
    public LoanInputValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be a positive non-zero value.");

        RuleFor(x => x.YearlyInterestRate)
            .GreaterThan(0)
            .LessThanOrEqualTo(300)
            .WithMessage("YearlyInterestRate must be a value between 0 (exclusive) and 300 (inclusive).");

        RuleFor(x => x.StartDate)
            .NotEmpty()
            .WithMessage("StartDate is required.");

        RuleFor(x => x.FirstInstalmentDate)
            .NotEmpty()
            .WithMessage("FirstInstalmentDate is required.");

        RuleFor(x => x.NumberOfInstalments)
            .GreaterThan(0)
            .WithMessage("NumberOfInstalments must be a positive non-zero value.");

        RuleFor(x => x)
            .Must(x => x.StartDate < x.FirstInstalmentDate)
            .WithMessage("FirstInstalmentDate must be after StartDate.");
        
        RuleForEach(x => x.Overpayments)
            .SetValidator(new OverpaymentInputValidator());
    }
}
