using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Infrastructure.Repositories
{
    public sealed class RoomAuthorizationService : IRoomAuthorizationService
    {
        private readonly ChatDbContext _context;

        public RoomAuthorizationService(ChatDbContext context)
        {
            _context = context;
        }

        public async Task<bool> CanAccessAsync(
            RoomId roomId,
            UserId userId,
            CancellationToken cancellationToken = default)
        {
            var room = await _context.ChatRooms
                .FirstOrDefaultAsync(x => x.Id.Value == roomId.Value, cancellationToken);

            return room != null && room.IsMember(userId);
        }
    }

}
