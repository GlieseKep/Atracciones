using AtraccionesService.Application.Abstractions.Services;
using AtraccionesService.Application.Exceptions;
using AtraccionesService.Application.Queries.Attractions;
using AtraccionesService.Application.ResultModels;
using AtraccionesService.Application.Services.Shared;
using AtraccionesService.DataManagment.UnitOfWork;

namespace AtraccionesService.Application.Services.Catalog;

/// <summary>Disponibilidad por fecha local: cupos por franja (<c>capacity - reservedQuantity</c>) y total del día.</summary>
public sealed class AvailabilityService(IUnitOfWork unitOfWork, BusinessClock clock) : IAvailabilityService
{
    public async Task<AvailabilityResult> GetAsync(GetAttractionAvailabilityQuery query, CancellationToken cancellationToken)
    {
        if (!await unitOfWork.Attractions.ExistsAsync(query.AttractionId, cancellationToken))
        {
            throw new NotFoundException("La atracción no existe.");
        }

        var slots = await unitOfWork.Availability.GetSlotsAsync(query.AttractionId, query.Date, cancellationToken);

        // Las franjas ya iniciadas no se ofrecen como disponibles.
        var results = slots
            .OrderBy(s => s.Time)
            .Select(s => new AvailabilitySlotResult(TimeFormats.Format(s.Time), clock.IsFuture(s.Date, s.Time) ? s.AvailableSpots : 0))
            .ToList();

        return new AvailabilityResult(query.Date, clock.TimeZoneId, results.Sum(s => s.AvailableSpots), results);
    }
}
