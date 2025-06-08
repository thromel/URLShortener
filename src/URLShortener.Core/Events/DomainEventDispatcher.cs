using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using URLShortener.Core.Domain.Enhanced;

namespace URLShortener.Core.Events;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchEventsAsync(IEnumerable<DomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
        {
            await DispatchEventAsync(domainEvent, cancellationToken);
        }
    }

    public async Task DispatchEventAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Dispatching domain event {EventType} with ID {EventId}", 
            domainEvent.GetType().Name, domainEvent.EventId);

        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        var handlers = _serviceProvider.GetServices(handlerType);

        var tasks = new List<Task>();
        foreach (var handler in handlers)
        {
            if (handler != null)
            {
                var handleMethod = handlerType.GetMethod("HandleAsync");
                if (handleMethod != null)
                {
                    var task = (Task)handleMethod.Invoke(handler, new object[] { domainEvent, cancellationToken })!;
                    tasks.Add(task);
                }
            }
        }

        try
        {
            await Task.WhenAll(tasks);
            _logger.LogDebug("Successfully dispatched domain event {EventType} with ID {EventId}",
                domainEvent.GetType().Name, domainEvent.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch domain event {EventType} with ID {EventId}",
                domainEvent.GetType().Name, domainEvent.EventId);
            throw;
        }
    }
}