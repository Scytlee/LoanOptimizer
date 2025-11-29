namespace LoanOptimizer.Application.UseCases.RunSimulation;

public class SimulationResult
{
    public bool Finished { get; set; }
    public InvalidStateReason? InvalidReason { get; set; }
    public LoanState[] FinalState { get; set; } = [];
}

public enum InvalidStateReason
{
    NotEnoughMoneyForInstalments,
    LeftoverPayments
}
