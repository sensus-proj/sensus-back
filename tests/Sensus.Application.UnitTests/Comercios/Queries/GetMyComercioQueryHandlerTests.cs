using FluentAssertions;
using NSubstitute;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Comercios.Queries.GetMyComercio;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Comercios.Queries;

public class GetMyComercioQueryHandlerTests
{
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly GetMyComercioQueryHandler _handler;

    private readonly Guid _testUserId = Guid.NewGuid();

    public GetMyComercioQueryHandlerTests()
    {
        _comercioRepository = Substitute.For<IComercioRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns(_testUserId.ToString());

        _handler = new GetMyComercioQueryHandler(_comercioRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_Should_ReturnComercioDto_WhenComercioExists()
    {
        // Arrange
        var comercio = Comercio.Create(
            _testUserId,
            "Abarrotes El Sol",
            "Av. Hidalgo 100",
            "03940",
            19.432608,
            -99.133209).Value;

        _comercioRepository.GetByUsuarioIdAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns(comercio);

        var query = new GetMyComercioQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.NombreComercial.Should().Be("Abarrotes El Sol");
        result.Value.UsuarioId.Should().Be(_testUserId);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserHasNoComercio()
    {
        // Arrange
        _comercioRepository.GetByUsuarioIdAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns((Comercio?)null);

        var query = new GetMyComercioQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Comercio.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenUserIsNotAuthenticated()
    {
        // Arrange
        _currentUserService.IsAuthenticated.Returns(false);

        var query = new GetMyComercioQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Unauthorized");
    }

    [Fact]
    public async Task Handle_Should_ReturnFromCache_WhenPresentInCache()
    {
        var cacheService = Substitute.For<ICacheService>();
        var cached = new ComercioDto(
            Guid.NewGuid(),
            _testUserId,
            "Comercio Cacheado",
            "Calle 1",
            "01234",
            19.4,
            -99.1,
            true,
            DateTime.UtcNow,
            new List<HorarioDiaDto>());

        cacheService.GetAsync<ComercioDto>($"sensus:merchants:user:{_testUserId}", Arg.Any<CancellationToken>())
            .Returns(cached);

        var handlerWithCache = new GetMyComercioQueryHandler(_comercioRepository, _currentUserService, cacheService);
        var result = await handlerWithCache.Handle(new GetMyComercioQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NombreComercial.Should().Be("Comercio Cacheado");
        await _comercioRepository.DidNotReceive().GetByUsuarioIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
