namespace LoanOptimizer.Application.UseCases.RunSimulation;

public sealed record RunSimulationResult(
    SimulationResult SimulationResult,
    LoanData[] Loans,
    LoanState[] FinalState,
    PaymentDay[] PaymentPlan);
