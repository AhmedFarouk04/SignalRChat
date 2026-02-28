// ✅ غير private لـ public في الـ constructor
public sealed class ReactionId
{
    public Guid Value { get; }

    public ReactionId(Guid value) // ← غير private لـ public
    {
        Value = value;
    }

    public static ReactionId New() => new(Guid.NewGuid());
    public static ReactionId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}