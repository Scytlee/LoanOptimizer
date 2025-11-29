using LoanOptimizer.Application.Abstractions.Requests;

namespace LoanOptimizer.Application.UseCases.RunSimulation;

public sealed record RunSimulationCommand : ICommand<RunSimulationResult>
{
    private RunSimulationCommand() { }

    public static RunSimulationCommand Create()
    {
        return new RunSimulationCommand();
    }
}
