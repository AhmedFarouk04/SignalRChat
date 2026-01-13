using EnterpriseChat.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseChat.Infrastructure.Messaging;

public sealed class DomainEventDispatcher
{
	private readonly IServiceProvider _serviceProvider;

	public DomainEventDispatcher(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public async Task DispatchAsync(
		IEnumerable<object> domainEvents,
		CancellationToken cancellationToken = default)
	{
		foreach (var domainEvent in domainEvents)
		{
			var handlerType =
				typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());

			var handlers =
				_serviceProvider.GetServices(handlerType);

			foreach (var handler in handlers)
			{
				var method = handlerType.GetMethod("Handle")!;
				await (Task)method.Invoke(
					handler,
					new[] { domainEvent, cancellationToken })!;
			}
		}
	}
}
