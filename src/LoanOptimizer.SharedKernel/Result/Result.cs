using System.Diagnostics.CodeAnalysis;

namespace LoanOptimizer.SharedKernel.Result;

public sealed class Result : IResult
{
    public IError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    private Result(bool isSuccess, IError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(IError error) => new(false, error);

    // Match method for explicit endpoint error handling
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<IError, TResult> onFailure)
    {
        return IsSuccess ? onSuccess() : onFailure(Error!);
    }
}
