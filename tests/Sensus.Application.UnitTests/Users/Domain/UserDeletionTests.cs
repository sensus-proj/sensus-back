using FluentAssertions;
using Sensus.Domain.Entities;

namespace Sensus.Application.UnitTests.Users.Domain;

/// <summary>Reglas de baja lógica de cuenta (Spec 060, FR-060-01 y FR-060-02).</summary>
public class UserDeletionTests
{
    [Fact]
    public void MarkAsDeleted_ShouldKeepPiiAndRotateStamp_OnlyOnce()
    {
        var now = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
        var user = User.Create("Usuario Prueba", "usuario@sensus.com", "hash", true, now.AddDays(-1)).Value;
        string originalStamp = user.SecurityStamp;

        bool first = user.MarkAsDeleted(now);
        string deletedStamp = user.SecurityStamp;

        first.Should().BeTrue();
        user.IsDeleted.Should().BeTrue();
        user.DeletedAtUtc.Should().Be(now);
        user.DeletedAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        user.Estatus.Should().BeFalse();
        deletedStamp.Should().NotBe(originalStamp);
        user.Correo.Should().Be("usuario@sensus.com");
        user.NombreCompleto.Should().Be("Usuario Prueba");
        user.PasswordHash.Should().Be("hash");

        bool second = user.MarkAsDeleted(now.AddDays(1));

        second.Should().BeFalse();
        user.DeletedAtUtc.Should().Be(now);
        user.SecurityStamp.Should().Be(deletedStamp);
    }

    [Fact]
    public void RestoreDeletion_ShouldNotRotateStamp()
    {
        var now = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
        var user = User.Create("Usuario Prueba", "usuario@sensus.com", "hash", true, now.AddDays(-1)).Value;
        string originalStamp = user.SecurityStamp;

        user.RestoreDeletion(now);

        user.IsDeleted.Should().BeTrue();
        user.DeletedAtUtc.Should().Be(now);
        user.Estatus.Should().BeFalse();
        user.SecurityStamp.Should().Be(originalStamp);
    }
}
