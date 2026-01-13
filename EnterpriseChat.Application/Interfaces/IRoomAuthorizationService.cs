using EnterpriseChat.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.Interfaces
{
    public interface IRoomAuthorizationService
    {
        Task<bool> CanAccessAsync(
            RoomId roomId,
            UserId userId,
            CancellationToken cancellationToken = default);
    }
}
