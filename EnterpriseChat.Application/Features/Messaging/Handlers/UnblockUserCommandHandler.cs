using EnterpriseChat.Application.Features.Messaging.Commands;
using EnterpriseChat.Application.Interfaces;
using EnterpriseChat.Domain.Interfaces;
using MediatR;

public sealed class UnblockUserCommandHandler : IRequestHandler<UnblockUserCommand, Unit>
{
    private readonly IUserBlockRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IUserPresenceNotifier _presenceNotifier;

    public UnblockUserCommandHandler(IUserBlockRepository repo, IUnitOfWork uow, IUserPresenceNotifier presenceNotifier)
    {
        _repo = repo;
        _uow = uow;
        _presenceNotifier = presenceNotifier;
    }

    public async Task<Unit> Handle(UnblockUserCommand request, CancellationToken ct)
    {
        await _repo.RemoveAsync(request.BlockerId, request.BlockedId, ct);
        await _uow.CommitAsync(ct);

        // ✅ نبعت CheckUserOnline للاتنين عشان يشوفوا الحالة الحقيقية
        await _presenceNotifier.BlockChangedAsync(request.BlockerId.Value, request.BlockedId.Value, false, ct);

        return Unit.Value;
    }
}
