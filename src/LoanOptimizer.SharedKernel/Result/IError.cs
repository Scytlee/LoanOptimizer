namespace LoanOptimizer.SharedKernel.Result;

public interface IError
{
    string Code { get; }
    string Message { get; }
}
