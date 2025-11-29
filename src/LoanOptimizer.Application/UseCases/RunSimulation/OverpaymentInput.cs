namespace LoanOptimizer.Application.UseCases.RunSimulation;

public record OverpaymentInput
{
    public DateTime Date { get; init; }
    public decimal Amount { get; init; }
}
