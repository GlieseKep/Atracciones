using AtraccionesService.DataAcess.Context;
using AtraccionesService.DataAcess.Entities.Catalog;
using AtraccionesService.DataAcess.Mapping;
using AtraccionesService.DataAcess.Repositories.Generic;
using AtraccionesService.DataManagment.Contracts.Catalog;
using AtraccionesService.DataManagment.Contracts.Common;
using AtraccionesService.DataManagment.Exceptions;
using AtraccionesService.DataManagment.QueryModels;
using AtraccionesService.DataManagment.Specifications;
using AtraccionesService.Domain.Catalog;
using AtraccionesService.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AtraccionesService.DataAcess.Repositories.Catalog;

public sealed class AttractionRepository(AtraccionesDbContext context) : GenericRepository<AttractionEntity>(context), IAttractionRepository
{
    private IQueryable<AttractionEntity> WithRelations(IQueryable<AttractionEntity> query) => query
        .Include(a => a.Operator)
        .Include(a => a.Categories)
        .Include(a => a.Badges)
        .Include(a => a.Inclusions)
        .Include(a => a.Languages)
        .Include(a => a.Locations)
        .Include(a => a.Photos)
        .AsSplitQuery();

    public async Task<Attraction?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await WithRelations(Set.AsNoTracking()).FirstOrDefaultAsync(a => a.Id == id, cancellationToken))?.ToDomain();

    public async Task<IReadOnlyList<Attraction>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        (await WithRelations(Set.AsNoTracking()).Where(a => ids.Contains(a.Id)).ToListAsync(cancellationToken)).Select(a => a.ToDomain()).ToList();

    public Task<PagedResult<Attraction>> ListAsync(PaginationRequest page, CancellationToken cancellationToken) =>
        PageAsync(Set.AsNoTracking().OrderBy(a => a.Name).ThenBy(a => a.Id), page, cancellationToken);

    public async Task<PagedResult<AttractionQueryResult>> ListSummariesAsync(PaginationRequest page, CancellationToken cancellationToken)
    {
        var query = Set.AsNoTracking();
        var ordered = (page.SortField, page.SortDescending) switch
        {
            ("price", false) => query.OrderBy(a => a.Price.Amount),
            ("price", true) => query.OrderByDescending(a => a.Price.Amount),
            ("rating", false) => query.OrderBy(a => a.RatingScore),
            ("rating", true) => query.OrderByDescending(a => a.RatingScore),
            (_, true) => query.OrderByDescending(a => a.Name),
            _ => query.OrderBy(a => a.Name),
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await ordered.ThenBy(a => a.Id).Skip(page.Offset).Take(page.Limit)
            .Select(a => new AttractionQueryResult(a.Id, a.Name, a.ProductType, a.Price.Currency, a.Price.Amount, a.RatingScore, a.RatingReviewCount))
            .ToListAsync(cancellationToken);
        return PagedResult<AttractionQueryResult>.Create(items, total, page);
    }

    public Task<PagedResult<Attraction>> SearchAsync(AttractionSearchCriteria criteria, PaginationRequest page, CancellationToken cancellationToken)
    {
        var query = Set.AsNoTracking();

        if (criteria.Currency is { } currency)
        {
            query = query.Where(a => a.Price.Currency == currency);
        }

        if (criteria.Cities.Count > 0)
        {
            var cities = criteria.Cities.Select(c => c.ToLower()).ToList();
            query = query.Where(a => a.Locations.Any(l => cities.Contains(l.City.ToLower())));
        }

        if (criteria.Countries.Count > 0)
        {
            var countries = criteria.Countries.ToList();
            query = query.Where(a => a.Locations.Any(l => countries.Contains(l.Country)));
        }

        if (criteria.MinimumReviewScore is { } score)
        {
            query = query.Where(a => a.RatingScore >= score);
        }

        if (criteria.MinimumReviewCount is { } count)
        {
            query = query.Where(a => a.RatingReviewCount >= count);
        }

        if (criteria.StartDate is not null || criteria.EndDate is not null)
        {
            var start = criteria.StartDate ?? DateOnly.MinValue;
            var end = criteria.EndDate ?? DateOnly.MaxValue;
            query = query.Where(a => Context.Availability.Any(s =>
                s.AttractionId == a.Id && s.Capacity > s.ReservedQuantity && s.Date >= start && s.Date <= end));
        }

        var ordered = criteria.Sort switch
        {
            AttractionSort.PriceAscending => query.OrderBy(a => a.Price.Amount),
            AttractionSort.PriceDescending => query.OrderByDescending(a => a.Price.Amount),
            AttractionSort.RatingDescending => query.OrderByDescending(a => a.RatingScore),
            _ => query.OrderByDescending(a => a.RatingReviewCount),
        };

        return PageAsync(ordered.ThenBy(a => a.Name).ThenBy(a => a.Id), page, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => Set.AnyAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Attraction attraction, CancellationToken cancellationToken)
    {
        var entity = new AttractionEntity { Id = attraction.Id, RatingReviewCount = attraction.Rating?.NumberOfReviews, RatingScore = attraction.Rating?.Score };
        await ApplyAsync(entity, attraction, cancellationToken);
        await AddAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(Attraction attraction, CancellationToken cancellationToken)
    {
        var entity = await WithRelations(Set).FirstAsync(a => a.Id == attraction.Id, cancellationToken);
        Context.Locations.RemoveRange(entity.Locations);
        Context.Photos.RemoveRange(entity.Photos);
        await ApplyAsync(entity, attraction, cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }

    /// <summary>Borrado físico; ubicaciones, fotos, asociaciones y franjas se eliminan en cascada. Las reservas históricas se conservan.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await Set.FindAsync([id], cancellationToken) is { } entity)
        {
            await DeleteAsync(entity, cancellationToken);
        }
    }

    private async Task<PagedResult<Attraction>> PageAsync(IQueryable<AttractionEntity> ordered, PaginationRequest page, CancellationToken cancellationToken)
    {
        var total = await ordered.CountAsync(cancellationToken);
        var items = await WithRelations(ordered.Skip(page.Offset).Take(page.Limit)).ToListAsync(cancellationToken);
        return PagedResult<Attraction>.Create(items.Select(a => a.ToDomain()).ToList(), total, page);
    }

    private async Task ApplyAsync(AttractionEntity entity, Attraction attraction, CancellationToken cancellationToken)
    {
        var d = attraction.Details;
        entity.Name = d.Name;
        entity.LongDescription = d.LongDescription;
        entity.Duration = d.Duration;
        entity.Price = d.Price.ToPrice();
        entity.ProductType = d.ProductType.ToString();
        entity.FreeCancellation = d.FreeCancellation;
        entity.UrlWeb = attraction.Urls?.Web;
        entity.UrlApp = attraction.Urls?.App;
        entity.Operator = d.Operator is null ? null : await ResolveOperatorAsync(d.Operator, cancellationToken);
        entity.OperatorId = d.Operator?.Id;
        entity.Categories = await ResolveAsync(Context.Categories, d.Categories, cancellationToken);
        entity.Badges = await ResolveAsync(Context.Badges, d.Badges, cancellationToken);
        entity.Inclusions = await ResolveAsync(Context.Inclusions, d.Includes, cancellationToken);
        entity.Languages = await ResolveAsync(Context.Languages, d.SupportedLanguages, cancellationToken);
        entity.Locations = d.Locations.Select((l, i) => new LocationEntity
        {
            Id = Guid.NewGuid(), AttractionId = attraction.Id, Position = i, Address = l.Address, City = l.City, Country = l.Country,
            Latitude = l.Latitude, Longitude = l.Longitude, Type = l.Type,
        }).ToList();
        entity.Photos = d.PhotoUrls.Select((url, i) => new PhotoEntity { Id = Guid.NewGuid(), AttractionId = attraction.Id, Position = i, Url = url }).ToList();

        // En una actualización la atracción ya está rastreada: las filas hijas nuevas traen Id asignado y deben
        // marcarse como insertadas explícitamente (si no, EF las trataría como existentes).
        if (Context.Entry(entity).State != EntityState.Detached)
        {
            Context.Locations.AddRange(entity.Locations);
            Context.Photos.AddRange(entity.Photos);
        }
    }

    private async Task<OperatorEntity> ResolveOperatorAsync(OperatorInfo info, CancellationToken cancellationToken)
    {
        var existing = await Context.Operators.FindAsync([info.Id], cancellationToken);
        if (existing is null)
        {
            existing = new OperatorEntity { Id = info.Id, Name = info.Name };
            Context.Operators.Add(existing);
        }
        else
        {
            existing.Name = info.Name;
        }

        return existing;
    }

    /// <summary>Reutiliza los valores de catálogo existentes (nombre único) y crea los que falten.</summary>
    private static async Task<List<T>> ResolveAsync<T>(DbSet<T> set, IReadOnlyList<string> names, CancellationToken cancellationToken)
        where T : class, ICatalogValue, new()
    {
        var distinct = names.Distinct(StringComparer.Ordinal).ToList();
        var existing = await set.Where(v => distinct.Contains(v.Name)).ToListAsync(cancellationToken);
        var tracked = set.Local.Where(v => distinct.Contains(v.Name)).ToList();
        var known = existing.Concat(tracked).DistinctBy(v => v.Name).ToDictionary(v => v.Name, StringComparer.Ordinal);
        return distinct.Select(name => known.TryGetValue(name, out var value) ? value : new T { Name = name }).ToList();
    }
}

public sealed class AvailabilityRepository(AtraccionesDbContext context) : GenericRepository<AttractionAvailabilityEntity>(context), IAvailabilityRepository
{
    public async Task<AvailabilitySlot?> GetSlotByIdAsync(Guid slotId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().FirstOrDefaultAsync(s => s.Id == slotId, cancellationToken))?.ToDomain();

    public async Task<IReadOnlyList<AvailabilitySlot>> GetSlotsAsync(Guid attractionId, DateOnly date, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().Where(s => s.AttractionId == attractionId && s.Date == date).OrderBy(s => s.Time).ToListAsync(cancellationToken))
        .Select(s => s.ToDomain()).ToList();

    public async Task<IReadOnlyList<AvailabilitySlot>> GetSlotsInRangeAsync(Guid attractionId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking()
            .Where(s => s.AttractionId == attractionId && s.Date >= from && s.Date <= to)
            .OrderBy(s => s.Date).ThenBy(s => s.Time)
            .ToListAsync(cancellationToken))
        .Select(s => s.ToDomain()).ToList();

    public async Task<AvailabilitySlot?> GetSlotAsync(Guid attractionId, DateOnly date, TimeOnly time, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().FirstOrDefaultAsync(s => s.AttractionId == attractionId && s.Date == date && s.Time == time, cancellationToken))?.ToDomain();

    public Task AddAsync(AvailabilitySlot slot, CancellationToken cancellationToken) => AddAsync(slot.ToEntity(), cancellationToken);

    public async Task<bool> AddWithConcurrencyCheckAsync(AvailabilitySlot slot, CancellationToken cancellationToken)
    {
        var entity = slot.ToEntity();
        Set.Add(entity);
        try
        {
            await SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (ConcurrencyException)
        {
            Context.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> UpdateIfVersionMatchesAsync(AvailabilitySlot slot, long expectedVersion, CancellationToken cancellationToken)
    {
        var updated = await Set
            .Where(s => s.Id == slot.Id && s.Version == expectedVersion)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Capacity, slot.Capacity)
                .SetProperty(x => x.ReservedQuantity, slot.ReservedQuantity)
                .SetProperty(x => x.Version, expectedVersion + 1), cancellationToken);
        return updated == 1;
    }

    /// <summary>
    /// Actualización condicional en una sola sentencia: solo incrementa si quedan cupos, por lo que dos solicitudes
    /// concurrentes no pueden sobrevender aunque el motor no ofrezca bloqueo de fila.
    /// </summary>
    public async Task<bool> TryReserveQuantityAsync(Guid slotId, int quantity, CancellationToken cancellationToken)
    {
        var updated = await Set
            .Where(s => s.Id == slotId && s.Capacity - s.ReservedQuantity >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity + quantity)
                .SetProperty(x => x.Version, x => x.Version + 1), cancellationToken);
        return updated == 1;
    }

    public Task ReleaseQuantityAsync(Guid slotId, int quantity, CancellationToken cancellationToken) =>
        Set.Where(s => s.Id == slotId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity >= quantity ? x.ReservedQuantity - quantity : 0)
                .SetProperty(x => x.Version, x => x.Version + 1), cancellationToken);
}

public sealed class ReservationRepository(AtraccionesDbContext context) : GenericRepository<ReservationEntity>(context), IReservationRepository
{
    public async Task<Reservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken))?.ToDomain();

    public async Task<Reservation?> GetByIdForCustomerAsync(Guid reservationId, Guid customerId, CancellationToken cancellationToken) =>
        (await Set.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reservationId && r.CustomerId == customerId, cancellationToken))?.ToDomain();

    public Task<bool> ExistsForCustomerAsync(Guid reservationId, Guid customerId, CancellationToken cancellationToken) =>
        Set.AnyAsync(r => r.Id == reservationId && r.CustomerId == customerId, cancellationToken);

    public async Task<PagedResult<Reservation>> GetByCustomerAsync(Guid customerId, ReservationFilter filter, PaginationRequest page, CancellationToken cancellationToken)
    {
        var query = Filter(Set.AsNoTracking().Where(r => r.CustomerId == customerId), filter);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Offset).Take(page.Limit).ToListAsync(cancellationToken);
        return PagedResult<Reservation>.Create(items.Select(r => r.ToDomain()).ToList(), total, page);
    }

    public async Task<PagedResult<ReservationQueryResult>> GetByAttractionAsync(Guid attractionId, ReservationFilter filter, PaginationRequest page, CancellationToken cancellationToken)
    {
        var query = Filter(Set.AsNoTracking().Where(r => r.AttractionId == attractionId), filter);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Offset).Take(page.Limit)
            .Select(r => new ReservationQueryResult(r.Id, r.AttractionId, r.Date, r.Time, r.TicketCount, r.Status, r.TotalPrice.Currency, r.TotalPrice.Amount))
            .ToListAsync(cancellationToken);
        return PagedResult<ReservationQueryResult>.Create(items, total, page);
    }

    public Task<bool> HasActiveByAttractionAsync(Guid attractionId, DateOnly fromDate, CancellationToken cancellationToken)
    {
        var cancelled = ReservationStatus.CANCELLED.ToString();
        return Set.AnyAsync(r => r.AttractionId == attractionId && r.Date >= fromDate && r.Status != cancelled, cancellationToken);
    }

    public Task AddAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var entity = new ReservationEntity();
        entity.Apply(reservation);
        return AddAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var entity = await Set.FirstAsync(r => r.Id == reservation.Id, cancellationToken);
        entity.Apply(reservation);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateStatusAsync(Guid reservationId, ReservationStatus expected, ReservationStatus next, string? reason, CancellationToken cancellationToken)
    {
        var expectedValue = expected.ToString();
        var nextValue = next.ToString();
        var updated = await Set
            .Where(r => r.Id == reservationId && r.Status == expectedValue)
            .ExecuteUpdateAsync(s =>
            {
                s.SetProperty(r => r.Status, nextValue);
                if (reason is not null)
                {
                    s.SetProperty(r => r.CancellationReason, reason);
                }
            }, cancellationToken);
        return updated == 1;
    }

    private static IQueryable<ReservationEntity> Filter(IQueryable<ReservationEntity> query, ReservationFilter filter)
    {
        if (filter.Status is { } status)
        {
            var value = status.ToString();
            query = query.Where(r => r.Status == value);
        }

        if (filter.FromDate is { } from)
        {
            query = query.Where(r => r.Date >= from);
        }

        if (filter.ToDate is { } to)
        {
            query = query.Where(r => r.Date <= to);
        }

        return filter.SortDescending
            ? query.OrderByDescending(r => r.Date).ThenByDescending(r => r.Time).ThenBy(r => r.Id)
            : query.OrderBy(r => r.Date).ThenBy(r => r.Time).ThenBy(r => r.Id);
    }
}
