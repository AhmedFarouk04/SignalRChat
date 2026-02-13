using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

public sealed class BlockUserCommandHandler : IRequestHandler<BlockUserCommand, Unit>
{
    private readonly IUserBlockRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IUserPresenceNotifier _presenceNotifier;

    public BlockUserCommandHandler(IUserBlockRepository repo, IUnitOfWork uow, IUserPresenceNotifier presenceNotifier)
    {
        _repo = repo;
        _uow = uow;
        _presenceNotifier = presenceNotifier;
    }

    public async Task<Unit> Handle(BlockUserCommand command, CancellationToken ct)
    {
        if (await _repo.IsBlockedAsync(command.BlockerId, command.BlockedId, ct))
            return Unit.Value;

        await _repo.AddAsync(BlockedUser.Create(command.BlockerId, command.BlockedId), ct);
        await _uow.CommitAsync(ct);

        // ✅ اخفاء presence فورًا
        await _presenceNotifier.HideUsersFromEachOtherAsync(
            command.BlockerId.Value,
            command.BlockedId.Value,
            ct);

        // (اختياري) تحديث flag
        await _presenceNotifier.BlockChangedAsync(command.BlockerId.Value, command.BlockedId.Value, true, ct);

        return Unit.Value;
    }
}
