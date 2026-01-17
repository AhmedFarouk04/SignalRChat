
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Commands
{

    public sealed record RemoveMemberFromGroupCommand(
        RoomId RoomId,
        UserId MemberId,
        UserId RequesterId
    );

}
