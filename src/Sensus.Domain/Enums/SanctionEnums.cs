namespace Sensus.Domain.Enums;

/// <summary>
/// Origen de una sanción (Spec 020).
/// </summary>
public enum SanctionType
{
    Automatic = 0,
    Manual = 1
}

/// <summary>
/// Estado efectivo de una sanción. <c>Expired</c> se deriva de la fecha de fin y nunca se persiste.
/// </summary>
public enum SanctionState
{
    Active = 0,
    Expired = 1,
    Reverted = 2
}
