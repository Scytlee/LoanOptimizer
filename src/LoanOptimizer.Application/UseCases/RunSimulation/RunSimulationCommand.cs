using LoanOptimizer.Application.Abstractions.Requests;

namespace LoanOptimizer.Application.UseCases.RunSimulation;

public sealed record RunSimulationCommand : ICommand<RunSimulationResult>
{
    public LoanData[] Loans { get; private init; } = null!;
    public PaymentDay[] PaymentPlan { get; private init; } = null!;

    private RunSimulationCommand() { }

    public static RunSimulationCommand Create(
        LoanData[] loans,
        PaymentDay[] paymentPlan)
    {
        return new RunSimulationCommand
        {
            Loans = loans,
            PaymentPlan = paymentPlan
        };
    }
}
