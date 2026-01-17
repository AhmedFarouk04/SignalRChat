namespace EnterpriseChat.Client.Models
{
    public sealed class GroupMembersModel
    {
        public Guid OwnerId { get; set; }
        public List<UserModel> Members { get; set; } = new();
    }

}
