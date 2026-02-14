namespace EnterpriseChat.Domain.Enums;
public enum MessageStatus : byte
{
    Pending = 0,   // Local only, not sent yet
    Sent = 1,      // Sent to server, no receipts yet
    Delivered = 2, // At least one receipt >= Delivered
    Read = 3,      // At least one receipt == Read
    Failed = 4     // Failed to send (add this for completeness)
}