using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Common; 
public record MessageReadInfo(MessageId MessageId, UserId SenderId);