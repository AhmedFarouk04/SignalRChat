using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Application.Features.Messaging.Commands
{

    public sealed record RemoveMemberFromGroupCommand(
        RoomId RoomId,
        UserId MemberId,
        UserId RequesterId
    );

}
