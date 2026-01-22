using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class BlockUserCommandHandler
    : IRequestHandler<BlockUserCommand, Unit>
{
    private readonly IUserBlockRepository _repo;
    private readonly IUnitOfWork _uow;

    public BlockUserCommandHandler(IUserBlockRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Unit> Handle(BlockUserCommand command, CancellationToken ct)
    {
        if (await _repo.IsBlockedAsync(command.BlockerId, command.BlockedId, ct))
            return Unit.Value;

        await _repo.AddAsync(BlockedUser.Create(command.BlockerId, command.BlockedId), ct);
        await _uow.CommitAsync(ct);

        return Unit.Value;
    }
}
