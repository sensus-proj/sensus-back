using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;

namespace Sensus.IntegrationTests.Fixtures.OfferFeed;

/// <summary>
/// Fixture determinista de relevancia, diversidad, exploración y exclusiones exigido por
/// <c>specs/042-recomendaciones/plan.md</c> §7.1 (T042-11, NFR-042-03 a NFR-042-06).
/// </summary>
public sealed class OfferFeedDeterministicFixture
{
    public const double ReferenceLat = 19.4326d;
    public const double ReferenceLng = -99.1332d;
    public static readonly DateTime FixedNowUtc = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    public static readonly Guid UserAWithInterestsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid UserBWithAffinityId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid UserCNoSignalsId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid UserEMerchantOwnerId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static readonly OfferCategory[] OtherCategories =
    [
        OfferCategory.Grocery,
        OfferCategory.CleaningHome,
        OfferCategory.Perishables,
        OfferCategory.Dairy,
        OfferCategory.Food,
        OfferCategory.Beverages,
        OfferCategory.Pets,
        OfferCategory.Pharmacy
    ];

    public IReadOnlyList<Comercio> ActiveMerchants { get; }
    public Comercio InactiveMerchant { get; }
    public Comercio UserEOwnMerchant { get; }
    public IReadOnlyList<Offer> VisibleOffers { get; }
    public IReadOnlyList<Offer> ExcludedOffers { get; }
    public IReadOnlyDictionary<Guid, double> DistanceByOfferId { get; }
    public IReadOnlyList<FeedOfferCandidate> VisibleCandidatesIn10Km { get; }

    public OfferFeedDeterministicFixture()
    {
        var activeMerchants = new List<Comercio>();
        for (int i = 0; i < 6; i++)
        {
            double latOffset = i switch
            {
                0 => 0.003d, // ~0.3 km (0-1)
                1 => 0.007d, // ~0.8 km (0-1)
                2 => 0.018d, // ~2.0 km (1-3)
                3 => 0.050d, // ~5.5 km (3-10)
                4 => 0.075d, // ~8.3 km (3-10)
                _ => 0.110d  // ~12.2 km (10-15)
            };

            Guid ownerId = Guid.Parse($"aaaa0000-0000-0000-0000-00000000000{i + 1}");
            Comercio comercio = Comercio.Reconstitute(
                Guid.Parse($"bbbb0000-0000-0000-0000-00000000000{i + 1}"),
                ownerId,
                $"Comercio Anillo {i + 1}",
                $"Av. Prueba {i + 1}",
                "06000",
                ReferenceLat + latOffset,
                ReferenceLng,
                activo: true,
                FixedNowUtc.AddDays(-60),
                MerchantCategory.Restaurantes);
            activeMerchants.Add(comercio);
        }

        ActiveMerchants = activeMerchants;

        InactiveMerchant = Comercio.Reconstitute(
            Guid.Parse("cccc0000-0000-0000-0000-000000000001"),
            Guid.Parse("aaaa0000-0000-0000-0000-000000000099"),
            "Comercio Inactivo",
            "Av. Cerrada 99",
            "06000",
            ReferenceLat + 0.002d,
            ReferenceLng,
            activo: false,
            FixedNowUtc.AddDays(-60),
            MerchantCategory.Otros);

        UserEOwnMerchant = Comercio.Reconstitute(
            Guid.Parse("dddd0000-0000-0000-0000-000000000001"),
            UserEMerchantOwnerId,
            "Comercio del Usuario E",
            "Calle Propia 1",
            "06000",
            ReferenceLat + 0.002d,
            ReferenceLng,
            activo: true,
            FixedNowUtc.AddDays(-60),
            MerchantCategory.Cafeterias);

        var visible = new List<Offer>(300);
        var distanceMap = new Dictionary<Guid, double>(330);

        // 300 ofertas visibles repartidas en 9 categorías (60 % = 180 Bakery para probar diversidad)
        // 40 publicadas hace menos de 48h para exploración.
        for (int i = 0; i < 300; i++)
        {
            OfferCategory category = i < 180
                ? OfferCategory.Bakery
                : OtherCategories[(i - 180) % OtherCategories.Length];

            // Anillos: la mayoría dentro de 0-10 km (índices merchant 0..4), ~30 en 10-15 km (índice 5)
            int merchantIdx = i < 270 ? (i % 5) : 5;
            Comercio merchant = activeMerchants[merchantIdx];

            bool isNew = i >= 180 && i < 220; // 40 ofertas nuevas fuera del sesgo Bakery
            DateTime createdAt = isNew ? FixedNowUtc.AddHours(-(6 + (i % 36))) : FixedNowUtc.AddDays(-(5 + (i % 20)));
            DateTime endsAt = FixedNowUtc.AddHours(6 + ((i * 3) % 160));

            Guid offerId = BuildDeterministicOfferId(1, i + 1);
            Offer offer = Offer.Reconstitute(
                offerId,
                merchant.Id,
                $"Oferta #{i + 1:D3} {category}",
                "Descripción determinista del fixture §7.1",
                100m,
                20 + (i % 30),
                80m - (i % 30),
                stock: 5 + (i % 10),
                FixedNowUtc.AddDays(-30),
                endsAt,
                OfferStatus.Published,
                null,
                merchant.Latitud,
                merchant.Longitud,
                createdAt,
                null,
                viewCount: (i * 7) % 120,
                version: 1,
                category: category.ToString());

            visible.Add(offer);
            distanceMap[offerId] = ComputeDistanceMeters(merchant.Latitud, merchant.Longitud);
        }

        VisibleOffers = visible;

        // 30 ofertas excluidas: 8 vencidas, 7 pausadas, 6 sin stock, 5 de comercio inactivo, 4 de comercio del usuario E
        var excluded = new List<Offer>(30);
        Comercio nearMerchant = activeMerchants[0];
        for (int i = 0; i < 30; i++)
        {
            Guid offerId = BuildDeterministicOfferId(2, i + 1);
            Comercio ownerMerchant = nearMerchant;
            OfferStatus status = OfferStatus.Published;
            int stock = 5;
            DateTime endsAt = FixedNowUtc.AddDays(3);

            if (i < 8)
            {
                endsAt = FixedNowUtc.AddHours(-2); // vencida
            }
            else if (i < 15)
            {
                status = OfferStatus.Paused; // pausada
            }
            else if (i < 21)
            {
                stock = 0; // sin stock
            }
            else if (i < 26)
            {
                ownerMerchant = InactiveMerchant; // comercio inactivo
            }
            else
            {
                ownerMerchant = UserEOwnMerchant; // comercio del propio usuario E
            }

            Offer offer = Offer.Reconstitute(
                offerId,
                ownerMerchant.Id,
                $"Excluida #{i + 1:D2}",
                "No debe mostrarse en el feed",
                100m,
                20,
                80m,
                stock,
                FixedNowUtc.AddDays(-10),
                endsAt,
                status,
                null,
                ownerMerchant.Latitud,
                ownerMerchant.Longitud,
                FixedNowUtc.AddDays(-5),
                null,
                0,
                1,
                "Bakery");

            excluded.Add(offer);
            distanceMap[offerId] = ComputeDistanceMeters(ownerMerchant.Latitud, ownerMerchant.Longitud);
        }

        ExcludedOffers = excluded;
        DistanceByOfferId = distanceMap;

        var merchantById = activeMerchants.ToDictionary(m => m.Id);
        VisibleCandidatesIn10Km = visible
            .Where(o => distanceMap[o.Id] <= 10_000d)
            .Select(o => new FeedOfferCandidate(
                o.Id,
                o.Title,
                o.Category!,
                o.ImageUrl,
                o.OriginalPrice,
                o.DiscountedPrice,
                o.Stock,
                distanceMap[o.Id],
                o.CreatedAtUtc,
                o.EndsAt,
                o.MerchantId,
                merchantById[o.MerchantId].NombreComercial,
                o.ViewCount))
            .ToList();
    }

    public IReadOnlyList<FeedOfferCandidate> QueryVisibleForUser(Guid userId, int radiusMeters)
    {
        return VisibleOffers
            .Concat(ExcludedOffers)
            .Where(o =>
            {
                if (o.Status != OfferStatus.Published || o.Stock <= 0 || o.EndsAt <= FixedNowUtc || o.StartsAt > FixedNowUtc)
                {
                    return false;
                }

                if (o.MerchantId == InactiveMerchant.Id)
                {
                    return false;
                }

                if (userId == UserEMerchantOwnerId && o.MerchantId == UserEOwnMerchant.Id)
                {
                    return false;
                }

                return DistanceByOfferId[o.Id] <= radiusMeters;
            })
            .Select(o =>
            {
                string merchantName = o.MerchantId == UserEOwnMerchant.Id
                    ? UserEOwnMerchant.NombreComercial
                    : ActiveMerchants.First(m => m.Id == o.MerchantId).NombreComercial;

                return new FeedOfferCandidate(
                    o.Id,
                    o.Title,
                    o.Category!,
                    o.ImageUrl,
                    o.OriginalPrice,
                    o.DiscountedPrice,
                    o.Stock,
                    DistanceByOfferId[o.Id],
                    o.CreatedAtUtc,
                    o.EndsAt,
                    o.MerchantId,
                    merchantName,
                    o.ViewCount);
            })
            .ToList();
    }

    private static Guid BuildDeterministicOfferId(int group, int index)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes[0] = (byte)group;
        bytes[14] = (byte)(index >> 8);
        bytes[15] = (byte)(index & 0xFF);
        return new Guid(bytes);
    }

    private static double ComputeDistanceMeters(double lat, double lng)
    {
        const double r = 6_371_000d;
        double dLat = (lat - ReferenceLat) * Math.PI / 180d;
        double dLon = (lng - ReferenceLng) * Math.PI / 180d;
        double a = Math.Pow(Math.Sin(dLat / 2d), 2d) +
            Math.Cos(ReferenceLat * Math.PI / 180d) * Math.Cos(lat * Math.PI / 180d) * Math.Pow(Math.Sin(dLon / 2d), 2d);
        return 2d * r * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
    }
}
