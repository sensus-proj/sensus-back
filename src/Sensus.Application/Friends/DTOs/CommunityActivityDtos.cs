namespace Sensus.Application.Friends.DTOs;

/// <summary>
/// Elemento del feed social de Comunidad: únicamente comentarios/reseñas y me gusta
/// de tus amigos conectados y tuyos.
/// </summary>
public sealed record CommunityActivityItemDto(
    string Id,
    string ActivityType,
    Guid ActorUserId,
    string ActorName,
    string? ActorPhotoUrl,
    Guid? OfferId,
    Guid? MerchantId,
    string TargetTitle,
    string? TargetSubtitle,
    string? CommentText,
    int? Rating,
    DateTime CreatedAtUtc);
