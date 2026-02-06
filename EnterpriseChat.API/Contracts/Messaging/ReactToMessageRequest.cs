// EnterpriseChat.API/Contracts/Messaging/ReactToMessageRequest.cs
using EnterpriseChat.Domain.Enums;

namespace EnterpriseChat.API.Contracts.Messaging;

public sealed class ReactToMessageRequest
{
    public ReactionType ReactionType { get; set; }
}