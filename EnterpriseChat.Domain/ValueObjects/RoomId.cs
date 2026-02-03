namespace EnterpriseChat.Domain.ValueObjects;

public sealed class RoomId : IEquatable<RoomId>
{
    public Guid Value { get; }

    public RoomId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("RoomId cannot be empty.");

        Value = value;
    }

    public static RoomId New() => new(Guid.NewGuid());

    public override bool Equals(object? obj)
        => Equals(obj as RoomId);

    public bool Equals(RoomId? other)
        => other is not null && Value.Equals(other.Value);

    public override int GetHashCode()
        => Value.GetHashCode();

    public override string ToString()
        => Value.ToString();

    public static implicit operator Guid(RoomId id)
        => id.Value;

    public static explicit operator RoomId(Guid value)
        => new(value);

    // ✅ جديد: operators ضروريين لEF translation
    public static bool operator ==(RoomId? left, RoomId? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Value == right.Value;
    }

    public static bool operator !=(RoomId? left, RoomId? right) => !(left == right);
}