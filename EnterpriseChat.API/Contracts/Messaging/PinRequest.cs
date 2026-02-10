namespace EnterpriseChat.API.Contracts.Messaging
{
    public record PinRequest(Guid? MessageId, string? Duration);
}
