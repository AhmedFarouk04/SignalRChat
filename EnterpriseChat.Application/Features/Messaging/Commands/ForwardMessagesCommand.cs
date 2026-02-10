using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.Features.Messaging.Commands
{
    public record ForwardMessagesCommand(
    Guid SenderId,
    List<Guid> MessageIds,
    List<Guid> TargetRoomIds) : IRequest<bool>;
}
