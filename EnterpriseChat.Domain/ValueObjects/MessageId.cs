namespace EnterpriseChat.Domain.ValueObjects;

public sealed class MessageId
{
    public Guid Value { get; }

    private MessageId(Guid value)
    {
        Value = value;
    }

    public static MessageId New() => new(Guid.NewGuid());

    public static MessageId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
