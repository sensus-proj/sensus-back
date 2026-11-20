using Sensus.Application.Users.DTOs;
using Sensus.Domain.Entities;

namespace Sensus.Application.Users.Interests;

public static class UserInterestsMappingExtensions
{
    public static UserInterestsDto ToDto(this UserInterests? interests) =>
        interests is null
            ? new UserInterestsDto([])
            : new UserInterestsDto(interests.Categories.Select(c => c.ToString()).ToList());
}
