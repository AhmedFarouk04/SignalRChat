namespace EnterpriseChat.Domain.ValueObjects;

public sealed class MessageId
{
    public Guid Value { get; }

    private MessageId(Guid value)
    {
        Value = value;
    }
    public static MessageId Empty { get; } = new(Guid.Empty);

    // ✅ أضف دالة ToGuid
    public Guid ToGuid() => Value;

    // ✅ دالة للتحقق من فارغ
    public bool IsEmpty => Value == Guid.Empty;

    // ✅ يمكنك أيضاً إضافة implicit conversion
    public static implicit operator Guid(MessageId id) => id.Value;

    public static implicit operator MessageId(Guid id) => new(id);
    public static MessageId New() => new(Guid.NewGuid());

    public static MessageId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
