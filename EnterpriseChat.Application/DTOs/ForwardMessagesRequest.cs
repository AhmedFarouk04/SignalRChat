namespace EnterpriseChat.Application.DTOs
{
    public class ForwardMessagesRequest
    {
        public List<Guid> MessageIds { get; set; } = new();
        public List<Guid> TargetRoomIds { get; set; } = new();
    }
}
