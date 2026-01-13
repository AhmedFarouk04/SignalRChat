using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;

namespace EnterpriseChat.Domain.Interfaces;

public interface IMessageRepository
{
	Task AddAsync(
		Message message,
		CancellationToken cancellationToken = default);

	Task<Message?> GetByIdAsync(
		Guid messageId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<Message>> GetByRoomAsync(
		RoomId roomId,
		int skip,
		int take,
		CancellationToken cancellationToken = default);
}
