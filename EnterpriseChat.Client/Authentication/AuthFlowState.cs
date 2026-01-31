namespace EnterpriseChat.Client.Authentication;

public sealed class AuthFlowState
{
    public string PendingEmail { get; set; } = "";
    public Guid? PendingUserId { get; set; }
}
