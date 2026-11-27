using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;

namespace Sensus.IntegrationTests.Infrastructure;

public sealed class OfferClaimBsonFilterTests
{
    private static readonly RenderArgs<OfferClaimDocument> ClaimRenderArgs = new(
        BsonSerializer.SerializerRegistry.GetSerializer<OfferClaimDocument>(),
        BsonSerializer.SerializerRegistry);

    [Fact]
    public void BuildOfferClaimFilters_ShouldRenderAllGuidFieldsAsStrings_WithoutUnspecifiedGuidException()
    {
        Guid userId = Guid.NewGuid();
        Guid offerId = Guid.NewGuid();
        Guid secondOfferId = Guid.NewGuid();
        IReadOnlyCollection<Guid> ids = [offerId, secondOfferId];

        BsonDocument countDoc = MongoOfferClaimRepository.BuildActiveCountFilter(offerId).Render(ClaimRenderArgs);
        BsonDocument batchDoc = MongoOfferClaimRepository.BuildOfferBatchFilter(ids).Render(ClaimRenderArgs);
        BsonDocument userOfferDoc = MongoOfferClaimRepository.BuildActiveByUserAndOfferFilter(userId, offerId).Render(ClaimRenderArgs);
        BsonDocument userAnyDoc = MongoOfferClaimRepository.BuildActiveByUserAndAnyOfferFilter(userId, ids).Render(ClaimRenderArgs);

        countDoc.ToJson().Should().Contain(offerId.ToString());
        batchDoc.ToJson().Should().Contain(secondOfferId.ToString());
        userOfferDoc.ToJson().Should().Contain(userId.ToString());
        userAnyDoc.ToJson().Should().Contain(offerId.ToString());
    }

    [Fact]
    public void OfferClaimDocument_ShouldSerializeAndDeserializeCartClaimOfferIdsAsStrings()
    {
        Guid offerA = Guid.NewGuid();
        Guid offerB = Guid.NewGuid();
        OfferClaim claim = OfferClaim.CreateCart(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new OfferClaimItem(offerA, 2, "Pan", 10m), new OfferClaimItem(offerB, 3, "Café", 15m)],
            DateTime.UtcNow).Value;
        claim.AssignCodeHash("hash");

        BsonDocument bson = OfferClaimDocument.FromDomain(claim).ToBsonDocument();
        OfferClaimDocument roundTrip = BsonSerializer.Deserialize<OfferClaimDocument>(bson);
        OfferClaim hydrated = roundTrip.ToDomain();

        bson["offerIds"].AsBsonArray.Should().Contain(new BsonString(offerA.ToString()));
        bson["offerIds"].AsBsonArray.Should().Contain(new BsonString(offerB.ToString()));
        hydrated.TotalUnits.Should().Be(5);
        hydrated.EffectiveItems.Should().HaveCount(2);
    }
}
