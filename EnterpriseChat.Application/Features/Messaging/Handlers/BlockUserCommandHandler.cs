using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class BlockUserCommandHandler
{
    private readonly IUserBlockRepository _repo;
    private readonly IUnitOfWork _uow;

    public BlockUserCommandHandler(
        IUserBlockRepository repo,
        IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task Handle(
    BlockUserCommand command,
    CancellationToken ct = default)
    {
        var alreadyBlocked =
            await _repo.IsBlockedAsync(
                command.BlockerId,
                command.BlockedId,
                ct);

        if (alreadyBlocked)
            return;

        var block = BlockedUser.Create(
            command.BlockerId,
            command.BlockedId);

        await _repo.AddAsync(block, ct);
        await _uow.CommitAsync(ct);
    }

}
