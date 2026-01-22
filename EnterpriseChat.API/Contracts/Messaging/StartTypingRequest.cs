namespace EnterpriseChat.API.Contracts.Messaging;

public sealed class StartTypingRequest
{
    public int TtlSeconds { get; set; } = 5;
}
