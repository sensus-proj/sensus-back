using FluentValidation.TestHelper;
using Sensus.Application.Media.Delivery;
using Xunit;

namespace Sensus.Application.UnitTests.Media.Delivery;

/// <summary>
/// Pruebas del validador de entrega multimedia (Spec 023).
/// Ref: FR-023-01 / FR-023-02 - identificador obligatorio.
/// </summary>
public sealed class GetMediaDeliveryQueryValidatorTests
{
    private readonly GetMediaDeliveryQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenMediaIdIsEmpty_ShouldHaveValidationError()
    {
        var query = new GetMediaDeliveryQuery(Guid.Empty, 123456, "signature");
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.MediaId)
            .WithErrorMessage("El identificador del activo multimedia es obligatorio.");
    }

    [Fact]
    public void Validate_WhenMediaIdIsValid_ShouldPassValidation()
    {
        var query = new GetMediaDeliveryQuery(Guid.NewGuid(), 123456, "signature");
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
