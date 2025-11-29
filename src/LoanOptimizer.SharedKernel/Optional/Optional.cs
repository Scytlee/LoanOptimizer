namespace LoanOptimizer.SharedKernel.Optional;

public readonly struct Optional<T>
{
    public bool IsSet { get; }
    public bool IsUnset => !IsSet;
    public T? Value => IsSet ? field : throw new InvalidOperationException("Optional value is not set.");

    private Optional(T? value)
    {
        Value = value;
        IsSet = true;
    }

    public static Optional<T> Unset { get; } = new();
    public static Optional<T> FromValue(T? value) => new(value);

    public override string ToString() => IsSet ? Value?.ToString() ?? "null" : "[Unset]";

    public static implicit operator Optional<T>(T? value) => FromValue(value);
}
