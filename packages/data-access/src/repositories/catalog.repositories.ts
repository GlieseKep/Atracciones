import { randomUUID } from 'node:crypto';
import type { Attraction, AvailabilitySlot, Reservation } from '@atracciones/domain';
import {
  PagedResult,
  type AttractionRepository,
  type AttractionSearchCriteria,
  type AvailabilityRepository,
  type PaginationRequest,
  type ReservationFilter,
  type ReservationRepository,
} from '@atracciones/data-management';
import { In, type EntityManager, type EntityTarget, type ObjectLiteral, type SelectQueryBuilder } from 'typeorm';
import {
  AttractionEntity,
  AvailabilityEntity,
  BadgeEntity,
  CategoryEntity,
  InclusionEntity,
  LanguageEntity,
  LocationEntity,
  OperatorEntity,
  PhotoEntity,
  ReservationEntity,
} from '../entities/catalog.entities';
import { fromReservation, toAttraction, toReservation, toSlot } from '../mapping';

const RELATIONS = ['operator', 'categories', 'badges', 'inclusions', 'languages', 'locations', 'photos'];

export class TypeOrmAttractionRepository implements AttractionRepository {
  constructor(private readonly manager: EntityManager) {}

  private get repo() {
    return this.manager.getRepository(AttractionEntity);
  }

  async getById(id: string): Promise<Attraction | null> {
    const entity = await this.repo.findOne({ where: { id }, relations: RELATIONS });
    return entity ? toAttraction(entity) : null;
  }

  async getByIds(ids: string[]): Promise<Attraction[]> {
    if (ids.length === 0) return [];
    return (await this.repo.find({ where: { id: In(ids) }, relations: RELATIONS })).map(toAttraction);
  }

  list(page: PaginationRequest): Promise<PagedResult<Attraction>> {
    return this.page(this.repo.createQueryBuilder('a').orderBy('a.name', 'ASC').addOrderBy('a.id', 'ASC'), page);
  }

  search(criteria: AttractionSearchCriteria, page: PaginationRequest): Promise<PagedResult<Attraction>> {
    const qb = this.repo.createQueryBuilder('a');
    if (criteria.currency) qb.andWhere('a.price_currency = :currency', { currency: criteria.currency });
    if (criteria.cities.length > 0) {
      qb.andWhere('EXISTS (SELECT 1 FROM locations l WHERE l.attraction_id = a.id AND lower(l.city) IN (:...cities))', {
        cities: criteria.cities.map((c) => c.toLowerCase()),
      });
    }
    if (criteria.countries.length > 0) {
      qb.andWhere('EXISTS (SELECT 1 FROM locations l WHERE l.attraction_id = a.id AND l.country IN (:...countries))', {
        countries: criteria.countries,
      });
    }
    if (criteria.minimumReviewScore !== null) qb.andWhere('a.rating_score >= :score', { score: criteria.minimumReviewScore });
    if (criteria.minimumReviewCount !== null) qb.andWhere('a.rating_review_count >= :count', { count: criteria.minimumReviewCount });
    if (criteria.startDate !== null || criteria.endDate !== null) {
      qb.andWhere(
        `EXISTS (SELECT 1 FROM attraction_availability s WHERE s.attraction_id = a.id AND s.capacity > s.reserved_quantity
           AND s.date >= :start AND s.date <= :end)`,
        { start: criteria.startDate ?? '0001-01-01', end: criteria.endDate ?? '9999-12-31' },
      );
    }
    switch (criteria.sort) {
      case 'PRICE_ASC':
        qb.orderBy('a.price_amount', 'ASC');
        break;
      case 'PRICE_DESC':
        qb.orderBy('a.price_amount', 'DESC');
        break;
      case 'RATING_DESC':
        qb.orderBy('a.rating_score', 'DESC', 'NULLS LAST');
        break;
      default:
        qb.orderBy('a.rating_review_count', 'DESC', 'NULLS LAST');
    }
    return this.page(qb.addOrderBy('a.name', 'ASC').addOrderBy('a.id', 'ASC'), page);
  }

  async exists(id: string): Promise<boolean> {
    return this.repo.exists({ where: { id } });
  }

  async add(attraction: Attraction): Promise<void> {
    const entity = this.repo.create({
      id: attraction.id,
      ratingReviewCount: attraction.rating?.numberOfReviews ?? null,
      ratingScore: attraction.rating?.score ?? null,
    });
    await this.apply(entity, attraction);
    await this.repo.save(entity);
  }

  async update(attraction: Attraction): Promise<void> {
    const entity = await this.repo.findOneOrFail({ where: { id: attraction.id }, relations: RELATIONS });
    await this.manager.delete(LocationEntity, { attractionId: attraction.id });
    await this.manager.delete(PhotoEntity, { attractionId: attraction.id });
    await this.apply(entity, attraction);
    await this.repo.save(entity);
  }

  async delete(id: string): Promise<void> {
    await this.repo.delete({ id });
  }

  /** Cuenta y pagina por identificador; después carga los agregados completos conservando el orden. */
  private async page(qb: SelectQueryBuilder<AttractionEntity>, page: PaginationRequest): Promise<PagedResult<Attraction>> {
    const total = await qb.getCount();
    const ids = (await qb.clone().select('a.id', 'id').offset(page.offset).limit(page.limit).getRawMany<{ id: string }>()).map((r) => r.id);
    const loaded = new Map((await this.getByIds(ids)).map((a) => [a.id, a]));
    return PagedResult.create(ids.map((id) => loaded.get(id)!).filter(Boolean), total, page);
  }

  private async apply(entity: AttractionEntity, attraction: Attraction): Promise<void> {
    const d = attraction.details;
    entity.name = d.name;
    entity.longDescription = d.longDescription;
    entity.duration = d.duration;
    entity.priceCurrency = d.price.currency;
    entity.priceAmount = d.price.amount;
    entity.productType = d.productType;
    entity.freeCancellation = d.freeCancellation;
    entity.urlWeb = attraction.urls?.web ?? null;
    entity.urlApp = attraction.urls?.app ?? null;
    if (d.operator) {
      await this.manager.upsert(OperatorEntity, { id: d.operator.id, name: d.operator.name }, ['id']);
      entity.operator = await this.manager.findOneByOrFail(OperatorEntity, { id: d.operator.id });
      entity.operatorId = d.operator.id;
    } else {
      entity.operator = null;
      entity.operatorId = null;
    }
    entity.categories = await this.resolve(CategoryEntity, d.categories);
    entity.badges = await this.resolve(BadgeEntity, d.badges);
    entity.inclusions = await this.resolve(InclusionEntity, d.includes);
    entity.languages = await this.resolve(LanguageEntity, d.supportedLanguages);
    entity.locations = d.locations.map((l, i) =>
      this.manager.create(LocationEntity, {
        id: randomUUID(), attractionId: attraction.id, position: i, address: l.address, city: l.city, country: l.country,
        latitude: l.latitude, longitude: l.longitude, type: l.type,
      }),
    );
    entity.photos = d.photoUrls.map((url, i) => this.manager.create(PhotoEntity, { id: randomUUID(), attractionId: attraction.id, position: i, url }));
  }

  /** Reutiliza los valores existentes por nombre y crea los que faltan (sin duplicados). */
  private async resolve<T extends ObjectLiteral & { name: string }>(target: EntityTarget<T>, names: string[]): Promise<T[]> {
    const distinct = [...new Set(names)];
    if (distinct.length === 0) return [];
    await this.manager
      .createQueryBuilder()
      .insert()
      .into(target)
      .values(distinct.map((name) => ({ name }) as never))
      .orIgnore()
      .execute();
    const found = await this.manager.getRepository(target).find({ where: { name: In(distinct) } as never });
    const byName = new Map(found.map((v) => [v.name, v]));
    return distinct.map((n) => byName.get(n)!);
  }
}

export class TypeOrmAvailabilityRepository implements AvailabilityRepository {
  constructor(private readonly manager: EntityManager) {}

  async getSlots(attractionId: string, date: string): Promise<AvailabilitySlot[]> {
    const rows = await this.manager.find(AvailabilityEntity, { where: { attractionId, date }, order: { time: 'ASC' } });
    return rows.map(toSlot);
  }

  async getSlot(attractionId: string, date: string, time: string): Promise<AvailabilitySlot | null> {
    const row = await this.manager.findOne(AvailabilityEntity, { where: { attractionId, date, time } });
    return row ? toSlot(row) : null;
  }

  async add(slot: AvailabilitySlot): Promise<void> {
    await this.manager.insert(AvailabilityEntity, {
      id: slot.id, attractionId: slot.attractionId, date: slot.date, time: slot.time, capacity: slot.capacity,
      reservedQuantity: slot.reservedQuantity, version: slot.version,
    });
  }

  async tryReserveQuantity(slotId: string, quantity: number): Promise<boolean> {
    const result = await this.manager
      .createQueryBuilder()
      .update(AvailabilityEntity)
      .set({ reservedQuantity: () => `reserved_quantity + ${Number(quantity)}`, version: () => 'version + 1' })
      .where('id = :slotId AND capacity - reserved_quantity >= :quantity', { slotId, quantity })
      .execute();
    return result.affected === 1;
  }

  async releaseQuantity(slotId: string, quantity: number): Promise<void> {
    await this.manager
      .createQueryBuilder()
      .update(AvailabilityEntity)
      .set({ reservedQuantity: () => `GREATEST(reserved_quantity - ${Number(quantity)}, 0)`, version: () => 'version + 1' })
      .where('id = :slotId', { slotId })
      .execute();
  }
}

export class TypeOrmReservationRepository implements ReservationRepository {
  constructor(private readonly manager: EntityManager) {}

  async getById(reservationId: string): Promise<Reservation | null> {
    const row = await this.manager.findOneBy(ReservationEntity, { id: reservationId });
    return row ? toReservation(row) : null;
  }

  async getByIdForCustomer(reservationId: string, customerId: string): Promise<Reservation | null> {
    const row = await this.manager.findOneBy(ReservationEntity, { id: reservationId, customerId });
    return row ? toReservation(row) : null;
  }

  async getByCustomer(customerId: string, filter: ReservationFilter, page: PaginationRequest): Promise<PagedResult<Reservation>> {
    const qb = this.manager.createQueryBuilder(ReservationEntity, 'r').where('r.customer_id = :customerId', { customerId });
    if (filter.status) qb.andWhere('r.status = :status', { status: filter.status });
    if (filter.fromDate) qb.andWhere('r.date >= :fromDate', { fromDate: filter.fromDate });
    if (filter.toDate) qb.andWhere('r.date <= :toDate', { toDate: filter.toDate });
    const direction = filter.sortDescending ? 'DESC' : 'ASC';
    qb.orderBy('r.date', direction).addOrderBy('r.time', direction).addOrderBy('r.id', 'ASC');
    const [rows, total] = await qb.skip(page.offset).take(page.limit).getManyAndCount();
    return PagedResult.create(rows.map(toReservation), total, page);
  }

  hasActiveByAttraction(attractionId: string, fromDate: string): Promise<boolean> {
    return this.manager
      .createQueryBuilder(ReservationEntity, 'r')
      .where('r.attraction_id = :attractionId AND r.date >= :fromDate AND r.status <> :cancelled', { attractionId, fromDate, cancelled: 'CANCELLED' })
      .getExists();
  }

  async add(reservation: Reservation): Promise<void> {
    await this.manager.insert(ReservationEntity, fromReservation(reservation));
  }

  async update(reservation: Reservation): Promise<void> {
    await this.manager.update(ReservationEntity, { id: reservation.id }, { status: reservation.status, cancellationReason: reservation.cancellationReason });
  }
}
