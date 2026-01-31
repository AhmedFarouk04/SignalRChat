using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Common; // أو namespace مناسب في Domain

public record MessageReadInfo(MessageId MessageId, UserId SenderId);