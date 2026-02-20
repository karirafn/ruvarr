namespace Ruvarr.Abstractions;

public class RuvarrResult
{
    protected RuvarrResult(bool isSuccess, RuvarrError error)
    {
        if ((isSuccess && error != RuvarrError.None) ||
            (!isSuccess && error == RuvarrError.None))
        {
            throw new ArgumentException("Invalid error", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public RuvarrError Error { get; }

    public static RuvarrResult Success => new(true, RuvarrError.None);

    public static RuvarrResult Failure(RuvarrError error) => new(false, error);

    public TResult Match<TResult>(Func<TResult> success, Func<RuvarrError, TResult> failure)
    {
        ArgumentNullException.ThrowIfNull(success);
        ArgumentNullException.ThrowIfNull(failure);

        return IsSuccess ? success() : failure(Error);
    }
}

internal sealed class Result<TValue> : RuvarrResult
{
    private readonly TValue? _value;

    public Result(TValue value)
        : base(true, RuvarrError.None)
    {
        _value = value;
    }

    public Result(RuvarrError error)
        : base(false, error)
    {
    }

    public static implicit operator Result<TValue>(RuvarrError error) => new(error);

    public static implicit operator Result<TValue>(TValue value) => new(value);

    public static implicit operator TValue(Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.FromResult();
    }

    public TValue FromResult()
    {
        ArgumentNullException.ThrowIfNull(_value);

        return _value;
    }

    public TResult Match<TResult>(Func<TValue, TResult> success, Func<RuvarrError, TResult> failure)
    {
        ArgumentNullException.ThrowIfNull(success);
        ArgumentNullException.ThrowIfNull(failure);

        if (IsFailure)
        {
            return failure(Error);
        }

        ArgumentNullException.ThrowIfNull(_value);
        return success(_value);
    }
}