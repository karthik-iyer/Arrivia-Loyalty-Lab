using System.Diagnostics.CodeAnalysis;

namespace LoyaltyLab.Domain.Common;

/// <summary>
/// An expected outcome: either a value or a business <see cref="Error"/>.
/// Exceptions are reserved for defects; this type is for outcomes the domain defines.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Success/Failure factories belong on Result<T>; a separate non-generic helper would hide the type the design specifies.")]
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new DomainException($"No value on a failed result ({Error.Code}).");

    public Error Error => IsFailure
        ? _error!
        : throw new DomainException("No error on a successful result.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }

    public Result<TNext> Map<TNext>(Func<T, TNext> map) =>
        IsSuccess ? Result<TNext>.Success(map(_value!)) : Result<TNext>.Failure(_error!);

    public Result<TNext> Bind<TNext>(Func<T, Result<TNext>> bind) =>
        IsSuccess ? bind(_value!) : Result<TNext>.Failure(_error!);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!);
}
