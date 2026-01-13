using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace EnterpriseChat.Application.Features.Messaging.Handlers
{
    public sealed class LeaveGroupHandler
    {
        private readonly IChatRoomRepository _repo;
        private readonly IUnitOfWork _uow;

        public LeaveGroupHandler(
            IChatRoomRepository repo,
            IUnitOfWork uow)
        {
            _repo = repo;
            _uow = uow;
        }

        public async Task Handle(
            LeaveGroupCommand command,
            CancellationToken ct = default)
        {
            var room = await _repo.GetByIdAsync(command.RoomId, ct)
                ?? throw new InvalidOperationException("Room not found.");

            if (room.Type != RoomType.Group)
                throw new InvalidOperationException("Cannot leave private room.");

            room.RemoveMember(command.UserId);

            await _uow.CommitAsync(ct);
        }
    }
}
