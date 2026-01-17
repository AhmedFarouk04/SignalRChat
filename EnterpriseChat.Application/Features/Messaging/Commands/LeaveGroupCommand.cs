
using EnterpriseChat.Domain.ValueObjects;
namespace EnterpriseChat.Application.Features.Messaging.Commands
{
    public sealed record LeaveGroupCommand(
    RoomId RoomId,
    UserId UserId
);
}
