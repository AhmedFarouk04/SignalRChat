using EnterpriseChat.Application.DTOs;
using EnterpriseChat.Domain.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.Features.Messaging.Queries
{

    public sealed record GetMyRoomsQuery(UserId CurrentUserId)
        : IRequest<IReadOnlyList<RoomListItemDto>>;
}
