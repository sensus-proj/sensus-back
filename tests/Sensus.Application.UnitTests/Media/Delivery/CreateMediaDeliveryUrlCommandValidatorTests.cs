using FluentValidation.TestHelper;
using Sensus.Application.Media.Delivery;
using Xunit;

namespace Sensus.Application.UnitTests.Media.Delivery;

/// <summary>
/// Pruebas del validador de emisión de URLs de entrega multimedia (Spec 023).
/// Ref: FR-023-02 - identificador obligatorio.
/// </summary>
public sealed class CreateMediaDeliveryUrlCommandValidatorTests
{
    private readonly CreateMediaDeliveryUrlCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenMediaIdIsEmpty_ShouldHaveValidationError()
    {
        var command = new CreateMediaDeliveryUrlCommand(Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.MediaId)
            .WithErrorMessage("El identificador del activo multimedia es obligatorio.");
    }

    [Fact]
    public void Validate_WhenMediaIdIsValid_ShouldPassValidation()
    {
        var command = new CreateMediaDeliveryUrlCommand(Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
