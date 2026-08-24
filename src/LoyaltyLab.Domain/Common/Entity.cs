namespace LoyaltyLab.Domain.Common;

/// <summary>
/// Identity-equality: two entities of the same type with the same id are the same entity,
/// regardless of other state. Transient instances (default id) are never equal.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : struct
{
    protected Entity()
    {
    }

    protected Entity(TId id) => Id = id;

    public TId Id { get; protected set; }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null || other.GetType() != GetType())
        {
            return false;
        }

        if (IsTransient() || other.IsTransient())
        {
            return ReferenceEquals(this, other);
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);

    private bool IsTransient() => EqualityComparer<TId>.Default.Equals(Id, default);
}
