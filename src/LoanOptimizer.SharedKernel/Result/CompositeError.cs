namespace LoanOptimizer.SharedKernel.Result;

public sealed record CompositeError : IError
{
    public IReadOnlyList<IError> Errors { get; }

    public CompositeError(IEnumerable<IError> errors)
    {
        Errors = Flatten(errors).ToList();
    }

    private static IEnumerable<IError> Flatten(IEnumerable<IError> errors)
    {
        foreach (var error in errors)
        {
            if (error is CompositeError composite)
            {
                // Recursively flatten nested composites
                foreach (var nested in composite.Errors)
                {
                    yield return nested;
                }
            }
            else
            {
                yield return error;
            }
        }
    }

    public string Code => "Error.Composite";
    public string Message => "One or more errors occurred.";

    public override string ToString()
    {
        var errorMessages = string.Join("; ", Errors.Select(e => e.ToString()));
        return $"{Code}: {Message} [{errorMessages}]";
    }
}
