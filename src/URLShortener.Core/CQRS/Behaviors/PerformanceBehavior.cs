using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace URLShortener.Core.CQRS.Behaviors;

public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly Stopwatch _timer;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
        _timer = new Stopwatch();
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _timer.Start();

        var response = await next();

        _timer.Stop();

        var elapsedMilliseconds = _timer.ElapsedMilliseconds;

        if (elapsedMilliseconds > 500) // Log if request takes longer than 500ms
        {
            var requestName = typeof(TRequest).Name;
            
            _logger.LogWarning("Long running request: {RequestName} ({ElapsedMs}ms) {@Request}",
                requestName, elapsedMilliseconds, request);
        }

        return response;
    }
}