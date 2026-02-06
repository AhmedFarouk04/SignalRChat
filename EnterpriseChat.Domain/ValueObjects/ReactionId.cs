// EnterpriseChat.Domain/ValueObjects/ReactionId.cs
namespace EnterpriseChat.Domain.ValueObjects;

public sealed class ReactionId
{
    public Guid Value { get; }

    private ReactionId(Guid value)
    {
        Value = value;
    }

    public static ReactionId New() => new(Guid.NewGuid());

    public static ReactionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}