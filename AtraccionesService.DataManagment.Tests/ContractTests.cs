using System.Reflection;
using AtraccionesService.DataAcess.UnitOfWork;
using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.UnitOfWork;

namespace AtraccionesService.DataManagment.Tests;

/// <summary>Pruebas de contratos de DataManagement (PLAN_IMPLEMENTACION_DATAMANAGEMENT.md §20).</summary>
public sealed class ContractTests
{
    private static readonly Assembly Contracts = typeof(IUnitOfWork).Assembly;
    private static readonly Assembly DataAccess = typeof(EfUnitOfWork).Assembly;

    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Data.SqlClient",
        "Microsoft.Data.Sqlite",
        "AtraccionesService.Application",
        "AtraccionesService.API",
        "AtraccionesService.DataAcess",
        "AtraccionesService.Contracts",
    ];

    public static IEnumerable<object[]> ContractInterfaces() =>
        Contracts.GetExportedTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .OrderBy(t => t.FullName)
            .Select(t => new object[] { t.FullName! });

    private static Type ByName(string fullName) => Contracts.GetType(fullName, throwOnError: true)!;

    [Fact]
    public void DataManagement_no_depende_de_ASP_NET_EF_ni_de_otras_capas()
    {
        var references = Contracts.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.Contains("AtraccionesService.Domain", references);
        Assert.DoesNotContain(references, name => ForbiddenAssemblyPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)));
    }

    [Theory, MemberData(nameof(ContractInterfaces))]
    public void Cada_metodo_asincrono_recibe_CancellationToken(string interfaceName)
    {
        var missing = ByName(interfaceName).GetMethods()
            .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType))
            .Where(m => m.GetParameters().LastOrDefault()?.ParameterType != typeof(CancellationToken))
            .Select(m => m.Name)
            .ToList();

        Assert.True(missing.Count == 0, $"{interfaceName}: métodos sin CancellationToken: {string.Join(", ", missing)}");
    }

    [Theory, MemberData(nameof(ContractInterfaces))]
    public void Ningun_metodo_expone_tipos_HTTP_ni_de_EF(string interfaceName)
    {
        var offending = ByName(interfaceName).GetMethods()
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
            .SelectMany(Flatten)
            .Where(t => t.Namespace is { } ns && (ns.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                                                  || ns.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                                                  || ns.StartsWith("System.Net.Http", StringComparison.Ordinal)))
            .Select(t => t.FullName)
            .Distinct()
            .ToList();

        Assert.Empty(offending);
    }

    [Theory, MemberData(nameof(ContractInterfaces))]
    public void DataAccess_implementa_cada_repositorio(string interfaceName)
    {
        var contract = ByName(interfaceName);

        var implementations = DataAccess.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && contract.IsAssignableFrom(t)).ToList();

        Assert.NotEmpty(implementations);
    }

    [Theory, MemberData(nameof(ContractInterfaces))]
    public void La_unidad_de_trabajo_expone_cada_repositorio(string interfaceName)
    {
        var contract = ByName(interfaceName);

        Assert.Contains(typeof(IUnitOfWork).GetProperties(), p => p.PropertyType == contract);
    }

    [Fact]
    public void DataAccess_implementa_unidad_de_trabajo_y_fabrica()
    {
        Assert.True(typeof(IUnitOfWork).IsAssignableFrom(typeof(EfUnitOfWork)));
        Assert.True(typeof(IUnitOfWorkFactory).IsAssignableFrom(typeof(EfUnitOfWorkFactory)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IUnitOfWork)));
    }

    [Fact]
    public void Las_interfaces_se_agrupan_por_responsabilidad()
    {
        // Sin repositorio genérico único en los contratos: cada interfaz pertenece a un agregado o consulta concreta.
        Assert.DoesNotContain(Contracts.GetExportedTypes(), t => t.IsInterface && t.IsGenericTypeDefinition && t.Name.Contains("Repository", StringComparison.Ordinal));
        Assert.All(ContractInterfaces(), row => Assert.InRange(ByName((string)row[0]).GetMethods().Length, 1, 12));
    }

    [Theory]
    [InlineData(0, -5, 20, 0)]
    [InlineData(500, 10, 100, 10)]
    [InlineData(15, 30, 15, 30)]
    public void PaginationRequest_aplica_limites_configurables(int limit, int offset, int expectedLimit, int expectedOffset)
    {
        var normalized = new PaginationRequest(limit, offset).Normalize(PaginationLimits.Default);

        Assert.Equal((expectedLimit, expectedOffset), (normalized.Limit, normalized.Offset));
        Assert.Equal(5, new PaginationRequest(500, 0).Normalize(new PaginationLimits(DefaultLimit: 2, MaxLimit: 5)).Limit);
    }

    [Fact]
    public void PaginationRequest_interpreta_orden_ascendente_y_descendente()
    {
        Assert.Equal(("price", true), (new PaginationRequest(10, 0, "-price").SortField, new PaginationRequest(10, 0, "-price").SortDescending));
        Assert.Equal(("name", false), (new PaginationRequest(10, 0, "name").SortField, new PaginationRequest(10, 0, "name").SortDescending));
    }

    [Fact]
    public void PagedResult_calcula_metadatos_de_pagina()
    {
        IPaginatedResult<int> page = PagedResult<int>.Create([5, 6], totalItems: 7, new PaginationRequest(2, 4));

        Assert.Equal((3, 2, 4, 7), (page.PageNumber, page.PageSize, page.TotalPages, page.TotalItems));
        Assert.Equal(["5", "6"], PagedResult<int>.Create([5, 6], 7, new PaginationRequest(2, 4)).Map(i => i.ToString()).Items);
    }

    private static IEnumerable<Type> Flatten(Type type) =>
        type.IsGenericType ? type.GetGenericArguments().SelectMany(Flatten).Prepend(type) : [type];
}
