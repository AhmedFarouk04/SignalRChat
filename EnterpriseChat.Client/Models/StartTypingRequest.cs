// EnterpriseChat.Client/Models/StartTypingRequest.cs
namespace EnterpriseChat.Client.Models;

public sealed class StartTypingRequest
{
    public int TtlSeconds { get; set; } = 5;
}