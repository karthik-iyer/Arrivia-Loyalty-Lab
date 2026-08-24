namespace LoyaltyLab.Domain.Common;

/// <summary>
/// A programming defect in how the domain is used, not a business outcome.
/// Catching this to recover is itself a defect — fix the call instead.
/// </summary>
public sealed class DomainException : InvalidOperationException
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
