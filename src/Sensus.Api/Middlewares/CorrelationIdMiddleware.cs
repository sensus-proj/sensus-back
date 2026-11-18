namespace Sensus.Api.Middlewares;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderKey = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderKey].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("D");
        }

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderKey] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [ItemKey] = correlationId
        }))
        {
            await _next(context);
        }
    }
}
