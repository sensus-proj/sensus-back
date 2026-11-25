namespace Sensus.Domain.Enums;

/// <summary>
/// Estados de ciclo de vida de una publicación en el feed social (Spec 024).
/// </summary>
public enum FeedPostStatus
{
    PendingModeration = 0,
    Published = 1,
    Quarantined = 2,
    Deleted = 3
}
