namespace LoanOptimizer.Application.UseCases.RunSimulation;

public record Payment
{
    public required PaymentType Type { get; init; }
    public required DateTime Date { get; init; }
    public required decimal Amount { get; init; }
    public required decimal InterestPart { get; init; }
    public required decimal PrincipalPart { get; init; }
    public required decimal OverallInterest { get; init; }
    public required decimal RemainingPrincipal { get; init; }
    public bool Paid { get; set; }
}

public enum PaymentType
{
    Instalment,
    Overpayment
}
