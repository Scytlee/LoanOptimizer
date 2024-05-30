using FluentValidation;

namespace LoanOptimizer.Input;

public record ProgramInput
{
    public IEnumerable<LoanInput> Loans { get; init; } = Enumerable.Empty<LoanInput>();
    public IEnumerable<PaymentDayInput> PaymentPlan { get; init; } = Enumerable.Empty<PaymentDayInput>();
}

public class ProgramInputValidator : AbstractValidator<ProgramInput>
{
    public ProgramInputValidator()
    {
        RuleFor(x => x.Loans)
            .NotEmpty()
            .WithMessage("At least one loan is required.");

        RuleForEach(x => x.Loans)
            .SetValidator(new LoanInputValidator());
        
        RuleFor(x => x.PaymentPlan)
            .NotEmpty()
            .WithMessage("At least one payment day is required.");
        
        RuleForEach(x => x.PaymentPlan)
            .SetValidator(new PaymentDayInputValidator());
    }
}
