using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Queries
{
    public sealed class GetGroupAdminsQueryHandler : IRequestHandler<GetGroupAdminsQuery, IReadOnlyList<Guid>>
    {
        private readonly IChatRoomRepository _repo;
        private readonly IRoomAuthorizationService _auth;

        public GetGroupAdminsQueryHandler(IChatRoomRepository repo, IRoomAuthorizationService auth)
        {
            _repo = repo;
            _auth = auth;
        }

        public async Task<IReadOnlyList<Guid>> Handle(GetGroupAdminsQuery request, CancellationToken ct)
        {
            await _auth.EnsureUserIsMemberAsync(request.RoomId, request.RequesterId, ct);

            var room = await _repo.GetByIdWithMembersAsync(request.RoomId, ct)
                ?? throw new InvalidOperationException("Room not found.");

            if (room.Type != RoomType.Group)
                throw new InvalidOperationException("Only group rooms allowed.");

            var admins = room.Members
                .Where(m => m.IsAdmin || room.OwnerId == m.UserId)
                .Select(m => m.UserId.Value)
                .Distinct()
                .ToList();

            return admins;
        }
    }
}