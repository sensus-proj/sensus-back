namespace Sensus.Domain.Enums;

/// <summary>
/// Veredicto final de una decisión de moderación auditada (Spec 021).
/// Ref: FR-021-01 - veredicto registrado junto con puntajes y umbral.
/// </summary>
public enum ModerationVerdict
{
    Approved = 0,
    Quarantined = 1,
    Rejected = 2
}

/// <summary>
/// Tipo de contenido sobre el que se adoptó la decisión de moderación.
/// Ref: FR-021-01 - identifica el objetivo auditado.
/// </summary>
public enum ModerationTargetType
{
    MediaAsset = 0,
    FeedComment = 1,
    Offer = 2,
    StoreReview = 3,
    Comercio = 4,
    User = 5
}

/// <summary>
/// Origen del actor que disparó la decisión de moderación.
/// Ref: FR-021-01 - trazabilidad de quién provocó la evaluación.
/// </summary>
public enum ModerationActorType
{
    User = 0,
    System = 1,
    Administrator = 2
}
