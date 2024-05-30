namespace LoanOptimizer.Models;

public class SimulationResult
{
    public bool Finished { get; set; }
    public InvalidStateReason? InvalidReason { get; set; }
}
