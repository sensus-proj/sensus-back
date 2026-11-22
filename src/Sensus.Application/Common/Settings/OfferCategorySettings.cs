namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración de validación de categorías de oferta (Specs 034 FR-034-08 y 035 FR-035-08).
/// <para>
/// <see cref="RequireStrictValidation"/> permanece desactivada (<c>false</c>) por defecto y solo debe
/// activarse (<c>true</c>) en producción cuando la versión de la aplicación móvil con el cambio T15.15
/// (<c>StoreCategory.apiCode</c>) haya sido publicada y adoptada por los comercios.
/// </para>
/// </summary>
public sealed class OfferCategorySettings
{
    public const string SectionName = "OfferCategory";

    /// <summary>
    /// Cuando es <c>true</c>, publicar (FR-034-08) y editar ofertas publicadas (FR-035-08) exigen
    /// un código válido del catálogo <see cref="Domain.Enums.OfferCategory"/>.
    /// Desactivado por defecto (<c>false</c>) hasta que la versión móvil con T15.15 esté publicada.
    /// </summary>
    public bool RequireStrictValidation { get; set; }
}
