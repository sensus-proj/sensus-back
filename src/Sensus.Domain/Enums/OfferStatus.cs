namespace Sensus.Domain.Enums;

/// <summary>
/// Estados del ciclo de vida de una oferta comercial (SE-022 / SE-026 y Spec 036).
/// </summary>
public enum OfferStatus
{
    Draft = 0,
    Published = 1,
    Paused = 2,
    Expired = 3,
    Archived = 4
}
