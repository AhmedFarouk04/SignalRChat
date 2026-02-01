namespace EnterpriseChat.API.Contracts.Messaging
{

    public sealed class CreateGroupRequest
    {
        public string Name { get; init; } = string.Empty;
        public IReadOnlyCollection<Guid> Members { get; init; } = [];
    
    
    }

}
