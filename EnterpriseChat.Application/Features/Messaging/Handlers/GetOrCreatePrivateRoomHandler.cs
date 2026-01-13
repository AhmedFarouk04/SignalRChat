using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using EnterpriseChat.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseChat.Application.Features.Messaging.Handlers
{
    public sealed class GetOrCreatePrivateRoomHandler
    {
        private readonly IChatRoomRepository _repo;
        private readonly IUnitOfWork _uow;

        public GetOrCreatePrivateRoomHandler(
            IChatRoomRepository repo,
            IUnitOfWork uow)
        {
            _repo = repo;
            _uow = uow;
        }

        public async Task<ChatRoom> Handle(
            UserId a,
            UserId b,
            CancellationToken ct = default)
        {
            var existing =
                await _repo.FindPrivateRoomAsync(a, b, ct);

            if (existing != null)
                return existing;

            var room = ChatRoom.CreatePrivate(a, b);
            await _repo.AddAsync(room, ct);
            await _uow.CommitAsync(ct);

            return room;
        }
    }

}
