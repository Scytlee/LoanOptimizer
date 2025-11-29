namespace LoanOptimizer.SharedKernel.Result;

public interface IResult
{
    bool IsSuccess { get; }
    IError? Error { get; }
}
