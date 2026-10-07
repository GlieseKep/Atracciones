using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace AtraccionesService.API.Routing;

/// <summary>
/// Antepone <c>api/v1</c> a todas las rutas de controladores. Los controladores declaran sus paths sin el prefijo,
/// igual que el contrato OpenAPI con <c>servers: /api/v1</c>.
/// </summary>
public sealed class ApiRoutePrefixConvention : IApplicationModelConvention
{
    public const string Prefix = "api/v1";

    private readonly AttributeRouteModel _prefix = new(new Microsoft.AspNetCore.Mvc.RouteAttribute(Prefix));

    public void Apply(ApplicationModel application)
    {
        foreach (var selector in application.Controllers.SelectMany(c => c.Selectors))
        {
            selector.AttributeRouteModel = selector.AttributeRouteModel is null
                ? _prefix
                : AttributeRouteModel.CombineAttributeRouteModel(_prefix, selector.AttributeRouteModel);
        }
    }
}
