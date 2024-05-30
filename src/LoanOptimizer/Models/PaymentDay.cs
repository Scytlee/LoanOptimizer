namespace LoanOptimizer.Models;

public class PaymentDay
{
    public required DateTime Date { get; init; }
    public required PaymentDayType Type { get; init; }
    public decimal? TotalBudget { get; set; }
    public decimal? InstalmentsToPay { get; set; }
    public decimal? OverpaymentBudget { get; set; }
}

public enum PaymentDayType
{
    OnlyInstalments,
    InstalmentsAndOverpayments
}
