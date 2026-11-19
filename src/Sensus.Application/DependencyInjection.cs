using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Sensus.Application.Common.Behaviors;

namespace Sensus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        // Sanciones y baneo (Spec 020)
        services.AddScoped<Sensus.Application.Common.Interfaces.ISanctionNotifier, Sensus.Application.Sanctions.Services.SanctionNotifier>();

        // Sensi IA (Spec 046)
        services.AddSingleton<Sensus.Application.Ai.Services.ITwoBlockPromptBuilder, Sensus.Application.Ai.Services.TwoBlockPromptBuilder>();
        services.AddScoped<Sensus.Application.Common.Interfaces.IDeterministicAiFallbackService, Sensus.Application.Ai.Services.DeterministicAiFallbackService>();
        services.AddScoped<Sensus.Application.Common.Interfaces.ICitedOfferEnricher, Sensus.Application.Ai.Services.CitedOfferEnricher>();

        return services;
    }
}
