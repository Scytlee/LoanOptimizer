using System.Diagnostics.CodeAnalysis;

namespace LoanOptimizer.SharedKernel.Result;

public sealed class Result<T> : IResult
{
    public T? Value { get; }
    public IError? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    [MemberNotNullWhen(true, nameof(Error))]
    [MemberNotNullWhen(false, nameof(Value))]
    public bool IsFailure => !IsSuccess;

    private Result(bool isSuccess, T? value, IError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(IError error) => new(false, default, error);

    /// <summary>
    /// Creates a Result from a validation error list. Returns Success if no errors, otherwise wraps errors in CompositeError.
    /// </summary>
    public static Result<T> FromValidation(IEnumerable<IError> errors, Func<T> valueFactory)
    {
        var errorList = errors.ToList();
        if (errorList.Count > 0)
        {
            return Failure(new CompositeError(errorList));
        }
        return Success(valueFactory());
    }

    // Match method for explicit endpoint error handling
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<IError, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }
}
