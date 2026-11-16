using System.Globalization;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Domain.ValueObjects;

namespace Sensus.Application.Ai.Services;

/// <summary>
/// Servicio de degradación determinista para Sensi IA (FR-046-13, NFR-046-07).
/// Genera respuestas estructuradas y paquetes de ofertas calculados por backend sin invocar modelos generativos.
/// </summary>
public sealed class DeterministicAiFallbackService : IDeterministicAiFallbackService
{
    private readonly IOfferRepository _offerRepository;

    public DeterministicAiFallbackService(IOfferRepository offerRepository)
    {
        _offerRepository = offerRepository;
    }

    public async Task<FallbackAiTurnResult> BuildFallbackAsync(
        string userPrompt,
        Guid userId,
        decimal? budgetMxn,
        CancellationToken cancellationToken = default)
    {
        // 1. Si se especificó un presupuesto positivo, intentar armar un paquete de ofertas con BudgetCalculator
        if (budgetMxn.HasValue && budgetMxn.Value > 0)
        {
            var offers = await _offerRepository.SearchAsync(
                query: null,
                category: null,
                maxBudgetMxn: budgetMxn.Value,
                limit: 10,
                cancellationToken: cancellationToken);

            var now = DateTime.UtcNow;
            var packageResult = BudgetCalculator.TryCreatePackage(
                offers,
                budgetMxn.Value,
                now,
                "Paquete recomendado de ofertas locales dentro de tu presupuesto.");

            if (packageResult.IsSuccess && packageResult.Value.ItemCount > 0)
            {
                var package = packageResult.Value;
                var citedIds = package.Items.Select(i => i.OfferId.ToString()).ToList();

                var reply = string.Format(
                    CultureInfo.InvariantCulture,
                    "En este momento me encuentro en modo de alta demanda, pero calculé directamente para ti este paquete de {0} oferta(s) con un total de ${1:F2} MXN dentro de tu presupuesto de ${2:F2} MXN:",
                    package.ItemCount,
                    package.TotalPriceMxn,
                    budgetMxn.Value);

                return new FallbackAiTurnResult(
                    reply,
                    package,
                    citedIds,
                    "Respuesta determinista generada por saturación o límite de IA.");
            }
        }

        // 2. Respuesta informativa estándar sin ofertas específicas
        var defaultReply =
            "En este momento el servicio inteligente de Sensi IA se encuentra temporalmente en modo de contingencia. " +
            "Puedes explorar todas las ofertas activas cercanas a tu ubicación en la pestaña de Ofertas y apartarlas con tu código QR único.";

        return new FallbackAiTurnResult(
            defaultReply,
            null,
            Array.Empty<string>(),
            "Modo contingencia por indisponibilidad de API OpenAI o límite de presupuesto.");
    }
}
