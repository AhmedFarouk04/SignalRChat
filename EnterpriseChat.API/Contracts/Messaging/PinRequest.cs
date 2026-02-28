namespace EnterpriseChat.API.Contracts.Messaging
{
    public sealed record PinRequest(
        Guid? MessageId,
        string? Duration,
        Guid? UnpinMessageId = null);
}
