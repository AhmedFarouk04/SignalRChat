namespace EnterpriseChat.Domain.ValueObjects;

public sealed class MessageId : IEquatable<MessageId>
{
    public Guid Value { get; }

    // ✅ غير private إلى public
    public MessageId(Guid value)
    {
        Value = value;
    }

    public static MessageId Empty => new(Guid.Empty);
    public bool IsEmpty => Value == Guid.Empty;
    public Guid ToGuid() => Value;

    // ✅ إضافة operators للتساوي
    public static bool operator ==(MessageId? left, MessageId? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Value == right.Value;
    }

    public static bool operator !=(MessageId? left, MessageId? right) => !(left == right);

    public bool Equals(MessageId? other)
    {
        if (other is null) return false;
        return Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as MessageId);

    public override int GetHashCode() => Value.GetHashCode();

    public static implicit operator Guid(MessageId id) => id.Value;
    public static implicit operator MessageId(Guid id) => new(id);

    public static MessageId New() => new(Guid.NewGuid());
    public static MessageId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}