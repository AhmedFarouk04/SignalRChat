namespace EnterpriseChat.Domain.ValueObjects;

public sealed class UserId : IEquatable<UserId>
{
    public Guid Value { get; }

    public UserId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.");

        Value = value;
    }

    public static UserId New() => new(Guid.NewGuid());

    public override bool Equals(object? obj)
        => Equals(obj as UserId);

    public bool Equals(UserId? other)
        => other is not null && Value.Equals(other.Value);

    public override int GetHashCode()
        => Value.GetHashCode();

    public static bool operator ==(UserId? left, UserId? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Value == right.Value;
    }

    public static bool operator !=(UserId? left, UserId? right)
        => !(left == right);

    public override string ToString()
        => Value.ToString();

    public static implicit operator Guid(UserId id)
        => id.Value;

    public static explicit operator UserId(Guid value)
        => new(value);
}
