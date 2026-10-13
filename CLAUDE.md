# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build everything
dotnet build

# Run the new console app (hardcoded loan data)
dotnet run --project src/LoanOptimizer.ConsoleUI

# Run the legacy console app (reads from JSON)
dotnet run --project src/LoanOptimizer -- input/input.json

# Run tests
dotnet test

# Run a single test by name
dotnet test --filter "CalculateOverpayment_Test"
```

Requires .NET 10 SDK (pinned in `global.json`). Uses the `Microsoft.Testing.Platform` test runner with xUnit v3.

## Architecture

The solution is being migrated from a monolithic console app (`LoanOptimizer`) to a layered architecture. Both versions coexist:

**Legacy project** (`src/LoanOptimizer`) — standalone console app that reads loan/payment data from a JSON file (`input/input.json`), validates with FluentValidation, runs the simulation, and prints results. Contains its own models under `Models/` and input DTOs under `Input/`.

**New layered projects:**
- `LoanOptimizer.ConsoleUI` — new entrypoint. Uses Microsoft.Extensions.Hosting and MediatR to dispatch commands. Loan data is currently hardcoded in `Program.cs`.
- `LoanOptimizer.Application` — use cases, domain models, and CQRS abstractions. Commands/queries flow through MediatR handlers that return `Result<T>`. The simulation logic lives here in `UseCases/RunSimulation/`.
- `LoanOptimizer.SharedKernel` — `Result<T>`/`Result` pattern, `IError`, `Optional<T>`. Referenced by Application.

Note: the `LoanOptimizer` legacy project is **not** referenced by the solution file (`LoanOptimizer.slnx`) but still exists on disk. The test project still references it.

## Domain Concepts

The simulator optimizes **loan overpayment allocation** across multiple loans given a fixed budget per payment day. It uses a greedy algorithm (allocate each unit of budget to the loan where it saves the most interest) followed by a refinement step that explores nearby allocations via backtracking with progressively smaller step sizes. `LoanCache` memoizes interest calculations per loan to avoid redundant recomputation.

Key types in `Application/UseCases/RunSimulation/`:
- `LoanData` — loan parameters (amount, rate, dates, instalments)
- `LoanState` — payment schedule for a loan, mutated by `ApplyOverpayment`/`ApplyOverpayments`
- `PaymentDay` — a date when payments are made, either instalments-only or instalments + overpayments with a total budget
- `Simulator` — static class orchestrating the simulation across all payment days
- `RunSimulationCommand` / `RunSimulationCommandHandler` — MediatR CQRS entry point

## Code Style

Follows `.editorconfig`: 4-space indentation, Allman brace style, `decimal` for all monetary calculations, nullable reference types enabled.
