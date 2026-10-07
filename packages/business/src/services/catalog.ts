import { Attraction, Money, newId } from '@atracciones/domain';
import type { AttractionSort, UnitOfWork, UnitOfWorkFactory } from '@atracciones/data-management';
import { ConflictError, NotFoundError } from '../errors';
import {
  PaginationResult,
  type AttractionResult,
  type AvailabilityResult,
  type CreateAttractionCommand,
  type DeleteAttractionCommand,
  type PatchAttractionCommand,
  type ReplaceAttractionCommand,
  type SearchAttractionsQuery,
  type SearchAttractionsResult,
} from '../models';
import { LocalPermissions, type AuthenticatedUser } from '../shared/auth';
import type { BusinessClock } from '../shared/clock';
import type { LocalPermissionService } from '../shared/customers';
import { mergePatch, toAttractionData, toAttractionResult, toDomainDetails } from '../shared/mappers';
import { PageTokenService } from '../shared/page-tokens';
import { IdempotentOperations, type IdempotencyService } from '../shared/transactions';
import { isCountry, isLanguage, MAX_PAGE_LIMIT, validateAttraction, validatePagination, ValidationErrors } from '../shared/validation';

const SORT_OPTIONS: Record<string, AttractionSort> = {
  most_popular: 'MOST_POPULAR',
  price_asc: 'PRICE_ASC',
  price_desc: 'PRICE_DESC',
  rating_desc: 'RATING_DESC',
};

const notFound = () => new NotFoundError('La atracción no existe.');

/**
 * Catálogo: búsqueda, detalle y mantenimiento. Las escrituras son administrativas: verifican el permiso local,
 * son idempotentes y registran auditoría.
 */
export class AttractionService {
  constructor(
    private readonly units: UnitOfWorkFactory,
    private readonly idempotency: IdempotencyService,
    private readonly permissions: LocalPermissionService,
    private readonly pageTokens: PageTokenService,
    private readonly clock: BusinessClock,
  ) {}

  async search(query: SearchAttractionsQuery): Promise<SearchAttractionsResult> {
    new ValidationErrors()
      .when(!Number.isInteger(query.rows) || query.rows < 1 || query.rows > MAX_PAGE_LIMIT, 'rows', `rows debe estar entre 1 y ${MAX_PAGE_LIMIT}.`)
      .when(query.currency !== null && !Money.isValidCurrency(query.currency), 'currency', 'currency debe ser un código ISO 4217.')
      .when(!(query.sortBy in SORT_OPTIONS), 'sort.by', 'sort.by no es un criterio de orden permitido.')
      .when(query.countries.some((c) => !isCountry(c)), 'countries', 'Los países deben ser códigos ISO 3166-1 alfa-2.')
      .when(query.startDate !== null && query.endDate !== null && query.endDate < query.startDate, 'dates', 'endDate debe ser igual o posterior a startDate.')
      .when(query.minimumReviewScore !== null && (query.minimumReviewScore < 0 || query.minimumReviewScore > 5), 'filters.rating.minimumReviewScore', 'Debe estar entre 0 y 5.')
      .when(query.minimumReviewCount !== null && query.minimumReviewCount < 0, 'filters.rating.minimumReviewCount', 'No puede ser negativo.')
      .throwIfAny();

    const criteria = {
      currency: query.currency,
      cities: query.cities,
      countries: query.countries,
      startDate: query.startDate,
      endDate: query.endDate,
      minimumReviewScore: query.minimumReviewScore,
      minimumReviewCount: query.minimumReviewCount,
      sort: SORT_OPTIONS[query.sortBy],
    };
    // El token queda ligado a los criterios y al tamaño de página: no puede reutilizarse con otra búsqueda.
    const criteriaHash = PageTokenService.hashCriteria({ criteria, rows: query.rows });
    const offset = query.nextPage ? this.pageTokens.readOffset(query.nextPage, criteriaHash) : 0;
    const page = await this.units.create().attractions.search(criteria, { limit: query.rows, offset });
    const nextOffset = offset + page.items.length;
    const nextPage = page.items.length > 0 && nextOffset < page.totalItems ? this.pageTokens.create(nextOffset, criteriaHash) : null;
    return { items: page.items.map(toAttractionResult), totalResults: page.totalItems, nextPage };
  }

  async getDetails(attractionIds: string[], languages: string[]): Promise<AttractionResult[]> {
    new ValidationErrors()
      .when(attractionIds.length === 0 || attractionIds.length > MAX_PAGE_LIMIT, 'attractions', `Se aceptan entre 1 y ${MAX_PAGE_LIMIT} identificadores.`)
      .when(languages.some((l) => !isLanguage(l)), 'languages', 'Los idiomas deben ser códigos ISO 639-1.')
      .throwIfAny();
    const ids = [...new Set(attractionIds)];
    const found = new Map((await this.units.create().attractions.getByIds(ids)).map((a) => [a.id, a]));
    // Se conserva el orden solicitado; los identificadores inexistentes se omiten del lote.
    return ids.filter((id) => found.has(id)).map((id) => toAttractionResult(found.get(id)!));
  }

  async list(limit: number, offset: number): Promise<PaginationResult<AttractionResult>> {
    validatePagination(limit, offset);
    const page = await this.units.create().attractions.list({ limit, offset });
    return new PaginationResult(page.items.map(toAttractionResult), page.totalItems, limit, offset);
  }

  async get(attractionId: string): Promise<AttractionResult> {
    const attraction = await this.units.create().attractions.getById(attractionId);
    if (!attraction) throw notFound();
    return toAttractionResult(attraction);
  }

  async create(command: CreateAttractionCommand): Promise<AttractionResult> {
    await this.permissions.ensure(command.actor, LocalPermissions.CatalogWrite);
    validateAttraction(command.data);
    return this.idempotency.execute(command.actor, IdempotentOperations.CreateAttraction, command.idempotencyKey, command.data, async (uow) => {
      const attraction = Attraction.create(toDomainDetails(command.data));
      await uow.attractions.add(attraction);
      await this.audit(uow, command.actor, 'attraction.create', attraction.id);
      return toAttractionResult(attraction);
    });
  }

  async replace(command: ReplaceAttractionCommand): Promise<void> {
    await this.permissions.ensure(command.actor, LocalPermissions.CatalogWrite);
    validateAttraction(command.data);
    await this.idempotency.execute(
      command.actor, IdempotentOperations.ReplaceAttraction, command.idempotencyKey,
      { attractionId: command.attractionId, data: command.data },
      async (uow) => {
        const attraction = await uow.attractions.getById(command.attractionId);
        if (!attraction) throw notFound();
        attraction.replace(toDomainDetails(command.data));
        await uow.attractions.update(attraction);
        await this.audit(uow, command.actor, 'attraction.replace', attraction.id);
        return true;
      },
    );
  }

  async patch(command: PatchAttractionCommand): Promise<AttractionResult> {
    await this.permissions.ensure(command.actor, LocalPermissions.CatalogWrite);
    return this.idempotency.execute(
      command.actor, IdempotentOperations.PatchAttraction, command.idempotencyKey,
      { attractionId: command.attractionId, patch: command.patch },
      async (uow) => {
        const attraction = await uow.attractions.getById(command.attractionId);
        if (!attraction) throw notFound();
        const merged = mergePatch(toAttractionData(attraction.details), command.patch);
        validateAttraction(merged);
        attraction.replace(toDomainDetails(merged));
        await uow.attractions.update(attraction);
        await this.audit(uow, command.actor, 'attraction.patch', attraction.id);
        return toAttractionResult(attraction);
      },
    );
  }

  /** Eliminación física. Se bloquea con 409 si la atracción tiene reservas activas futuras. */
  async delete(command: DeleteAttractionCommand): Promise<void> {
    await this.permissions.ensure(command.actor, LocalPermissions.CatalogWrite);
    await this.idempotency.execute(
      command.actor, IdempotentOperations.DeleteAttraction, command.idempotencyKey,
      { attractionId: command.attractionId },
      async (uow) => {
        if (!(await uow.attractions.exists(command.attractionId))) throw notFound();
        if (await uow.reservations.hasActiveByAttraction(command.attractionId, this.clock.today)) {
          throw new ConflictError('ATTRACTION_HAS_ACTIVE_RESERVATIONS', 'La atracción tiene reservas activas futuras y no puede eliminarse.');
        }
        await uow.attractions.delete(command.attractionId);
        await this.audit(uow, command.actor, 'attraction.delete', command.attractionId);
        return true;
      },
    );
  }

  private async audit(uow: UnitOfWork, actor: AuthenticatedUser, action: string, attractionId: string): Promise<void> {
    const user = await uow.users.getByIdentity(actor.issuer, actor.subject);
    await uow.auditEvents.add({
      id: newId(),
      actorUserId: user?.id ?? null,
      actorSubject: actor.subject,
      action,
      resourceType: 'attraction',
      resourceId: attractionId,
      reason: null,
      createdAt: this.clock.utcNow,
    });
  }
}

/** Disponibilidad por fecha local: cupos por franja (`capacity - reservedQuantity`) y total del día. */
export class AvailabilityService {
  constructor(
    private readonly units: UnitOfWorkFactory,
    private readonly clock: BusinessClock,
  ) {}

  async get(attractionId: string, date: string): Promise<AvailabilityResult> {
    const uow = this.units.create();
    if (!(await uow.attractions.exists(attractionId))) throw notFound();
    const slots = await uow.availability.getSlots(attractionId, date);
    // Las franjas ya iniciadas no se ofrecen como disponibles.
    const results = [...slots]
      .sort((a, b) => a.time.localeCompare(b.time))
      .map((s) => ({ time: s.time, availableSpots: this.clock.isFuture(s.date, s.time) ? s.availableSpots : 0 }));
    return {
      date,
      timeZone: this.clock.timeZone,
      availableSpots: results.reduce((sum, s) => sum + s.availableSpots, 0),
      slots: results,
    };
  }
}
