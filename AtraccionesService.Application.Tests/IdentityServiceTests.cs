using AtraccionesService.Application.Abstractions.Authorization;
using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Commands.Customers;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Application.Tests.Infrastructure;
using AtraccionesService.Domain.Identity;
using static AtraccionesService.Application.Tests.Infrastructure.ApplicationTestHost;

namespace AtraccionesService.Application.Tests;

public sealed class IdentityServiceTests : IDisposable
{
    private readonly ApplicationTestHost _host = new();

    private Task<RegisteredUserResult> Register(AuthenticatedUser user, BillingData? billing = null) =>
        _host.Run<IUserProfileService, RegisteredUserResult>(s =>
            s.RegisterAsync(new RegisterUserCommand(user, user.Email!, billing ?? new BillingData(null, null, null, null, null)), default));

    [Fact]
    public async Task Registro_crea_usuario_y_cliente_y_repetirlo_no_duplica()
    {
        var user = User("nuevo");

        var first = await Register(user, new BillingData("Nuevo", "factura@ejemplo.com", "Quito", "1712345678", null));
        var second = await Register(user);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.CustomerId, second.CustomerId);
        var stored = Assert.Single(_host.Database.Users.Values);
        Assert.Equal((Issuer, "nuevo"), (stored.OAuthIssuer, stored.OAuthSubject));
        Assert.Equal("factura@ejemplo.com", Assert.Single(_host.Database.Customers.Values).BillingEmail);
    }

    [Fact]
    public async Task Mismo_subject_de_otro_issuer_es_otro_usuario()
    {
        await Register(User("compartido"));
        await Register(new AuthenticatedUser("https://otro-issuer.test", "compartido", "x@ejemplo.com", true));

        Assert.Equal(2, _host.Database.Users.Count);
    }

    [Fact]
    public async Task Registro_rechaza_email_no_verificado_y_no_crea_registros_parciales()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            Register(new AuthenticatedUser(Issuer, "sin-verificar", "x@ejemplo.com", EmailVerified: false)));

        Assert.Empty(_host.Database.Users);
        Assert.Empty(_host.Database.Customers);
    }

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("ref con espacios")]
    public async Task Facturacion_rechaza_referencias_de_pago_que_parecen_datos_reales(string reference)
    {
        var user = await _host.RegisterAsync();

        var error = await Assert.ThrowsAsync<ValidationException>(() => _host.Run<ICustomerService, CustomerResult>(s =>
            s.UpdateCurrentAsync(new UpdateCustomerCommand(user, new BillingData("Ana", "ana@ejemplo.com", "Quito", null, reference)), default)));

        Assert.True(error.Errors.ContainsKey("paymentMethodReference"));
    }

    [Fact]
    public async Task Actualizar_facturacion_reemplaza_datos_del_cliente_propio()
    {
        var user = await _host.RegisterAsync();
        _host.Clock.Advance(TimeSpan.FromMinutes(5));

        var updated = await _host.Run<ICustomerService, CustomerResult>(s =>
            s.UpdateCurrentAsync(new UpdateCustomerCommand(user, new BillingData("Ana", "ana@ejemplo.com", "Quito", null, "sim-card-01")), default));

        Assert.Equal("sim-card-01", updated.PaymentMethodReference);
        Assert.Null(updated.TaxId);
        Assert.Equal(_host.Clock.Now, updated.UpdatedAt);
        await Assert.ThrowsAsync<NotFoundException>(() => _host.Run<ICustomerService, CustomerResult>(s => s.GetCurrentAsync(User("sin-perfil"), default)));
    }

    [Fact]
    public async Task Permisos_locales_dependen_del_rol_y_del_estado_del_usuario()
    {
        var admin = await _host.RegisterAsync("admin", LocalPermissions.CatalogWrite);
        var plain = await _host.RegisterAsync("plain");

        Assert.True(await _host.Run<ILocalPermissionService, bool>(s => s.HasPermissionAsync(admin, LocalPermissions.CatalogWrite, default)));
        Assert.False(await _host.Run<ILocalPermissionService, bool>(s => s.HasPermissionAsync(plain, LocalPermissions.CatalogWrite, default)));
        Assert.False(await _host.Run<ILocalPermissionService, bool>(s => s.HasPermissionAsync(User("desconocido"), LocalPermissions.CatalogWrite, default)));

        var stored = _host.Database.Users.Values.Single(u => u.OAuthSubject == "admin");
        _host.Database.Users[stored.Id] = Domain.Identity.User.Restore(stored.Id, stored.OAuthIssuer, stored.OAuthSubject, stored.Email, UserStatus.DISABLED, stored.CreatedAt, stored.UpdatedAt);
        Assert.False(await _host.Run<ILocalPermissionService, bool>(s => s.HasPermissionAsync(admin, LocalPermissions.CatalogWrite, default)));
    }

    public void Dispose() => _host.Dispose();
}
