using EnterpriseChat.Application.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.Features.Messaging.Queries
{
    // EnterpriseChat.Application/Features/Messaging/Queries/SearchMessagesQuery.cs
    public record SearchMessagesQuery(Guid RoomId, Guid UserId, string SearchTerm, int Take = 50)
        : IRequest<IReadOnlyList<MessageReadDto>>;
}
