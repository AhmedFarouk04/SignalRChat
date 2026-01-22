using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

namespace EnterpriseChat.Application.Features.Messaging.Handlers;

public sealed class UnblockUserCommandHandler
    : IRequestHandler<UnblockUserCommand, Unit>
{
    private readonly IUserBlockRepository _repo;
    private readonly IUnitOfWork _uow;

    public UnblockUserCommandHandler(IUserBlockRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Unit> Handle(UnblockUserCommand request, CancellationToken ct)
    {
        await _repo.RemoveAsync(request.BlockerId, request.BlockedId, ct);
        await _uow.CommitAsync(ct);
        return Unit.Value;
    }
}
