import { newId, Reservation, RESERVATION_STATUSES, type ReservationStatus } from '@atracciones/domain';
import type { UnitOfWorkFactory } from '@atracciones/data-management';
import { ConflictError, NotFoundError } from '../errors';
import {
  PaginationResult,
  type CancelReservationCommand,
  type CreateReservationCommand,
  type GetReservationsQuery,
  type ReservationResult,
} from '../models';
import type { AuthenticatedUser } from '../shared/auth';
import type { BusinessClock } from '../shared/clock';
import { PriceCalculator, type CustomerResolver } from '../shared/customers';
import { toReservationResult } from '../shared/mappers';
import { IdempotentOperations, type IdempotencyService } from '../shared/transactions';
import { validatePagination, ValidationErrors, type SlotRequestValidator } from '../shared/validation';

const notFound = () => new NotFoundError('La reserva no existe.');

/**
 * Reserva independiente (`POST /atracciones/{id}/reservations`): se confirma al crearse, no genera pedido ni pago.
 * El propietario es el cliente resuelto desde la identidad autenticada.
 */
export class ReservationService {
  constructor(
    private readonly units: UnitOfWorkFactory,
    private readonly idempotency: IdempotencyService,
    private readonly customers: CustomerResolver,
    private readonly slots: SlotRequestValidator,
    private readonly clock: BusinessClock,
  ) {}

  create(command: CreateReservationCommand): Promise<ReservationResult> {
    this.slots.validate(command.date, command.time, command.ticketCount, 'ticketCount', (errors) => {
      errors.requiredText(command.customerName, 'customerName', 200).email(command.customerEmail, 'customerEmail', true);
    });
    const payload = {
      attractionId: command.attractionId,
      date: command.date,
      time: command.time,
      ticketCount: command.ticketCount,
      customerName: command.customerName,
      customerEmail: command.customerEmail,
    };
    return this.idempotency.execute(command.user, IdempotentOperations.CreateReservation, command.idempotencyKey, payload, async (uow) => {
      const customer = await this.customers.require(uow, command.user);
      const attraction = await uow.attractions.getById(command.attractionId);
      if (!attraction) throw new NotFoundError('La atracción no existe.');
      const slot = await uow.availability.getSlot(command.attractionId, command.date, command.time);
      if (!slot) throw new ConflictError(ConflictError.SLOT_UNAVAILABLE, 'La atracción no opera en la fecha y hora seleccionadas.');
      if (!(await uow.availability.tryReserveQuantity(slot.id, command.ticketCount))) {
        throw new ConflictError(ConflictError.INSUFFICIENT_AVAILABILITY, 'No hay cupos suficientes para la franja solicitada.');
      }
      const now = this.clock.utcNow;
      const reservation = Reservation.create({
        attractionId: command.attractionId,
        customerId: customer.customer.id,
        date: command.date,
        time: command.time,
        ticketCount: command.ticketCount,
        totalPrice: PriceCalculator.total(attraction, command.ticketCount),
        customerName: command.customerName,
        customerEmail: command.customerEmail,
        initialStatus: 'CONFIRMED',
        now,
      });
      await uow.reservations.add(reservation);
      await uow.inventory.add({
        id: newId(), attractionId: command.attractionId, availabilitySlotId: slot.id, quantity: command.ticketCount,
        movementType: 'CONFIRMED', purchaseId: null, reservationId: reservation.id, createdAt: now,
      });
      return toReservationResult(reservation);
    });
  }

  cancel(command: CancelReservationCommand): Promise<ReservationResult> {
    new ValidationErrors().requiredText(command.reason, 'reason', 500).throwIfAny();
    return this.idempotency.execute(
      command.user, IdempotentOperations.CancelReservation, command.idempotencyKey,
      { reservationId: command.reservationId, reason: command.reason },
      async (uow) => {
        const customer = await this.customers.find(uow, command.user);
        const reservation = customer ? await uow.reservations.getByIdForCustomer(command.reservationId, customer.customer.id) : null;
        if (!reservation) throw notFound();
        if (await uow.orders.getByReservationId(reservation.id)) {
          throw new ConflictError('RESERVATION_MANAGED_BY_ORDER', 'La reserva pertenece a un pedido; debe cancelarse mediante el pedido.');
        }
        if (!reservation.canCancel) {
          throw new ConflictError(ConflictError.INVALID_STATE_TRANSITION, `No se puede cancelar una reserva ${reservation.status}.`);
        }
        if (!this.clock.isFuture(reservation.date, reservation.time)) {
          throw new ConflictError('RESERVATION_ALREADY_STARTED', 'No se puede cancelar una reserva cuya franja ya comenzó.');
        }
        reservation.cancel(command.reason);
        await uow.reservations.update(reservation);
        const slot = await uow.availability.getSlot(reservation.attractionId, reservation.date, reservation.time);
        if (slot) {
          await uow.availability.releaseQuantity(slot.id, reservation.ticketCount);
          await uow.inventory.add({
            id: newId(), attractionId: reservation.attractionId, availabilitySlotId: slot.id, quantity: -reservation.ticketCount,
            movementType: 'RELEASED', purchaseId: null, reservationId: reservation.id, createdAt: this.clock.utcNow,
          });
        }
        return toReservationResult(reservation);
      },
    );
  }

  async list(query: GetReservationsQuery): Promise<PaginationResult<ReservationResult>> {
    validatePagination(query.limit, query.offset);
    new ValidationErrors()
      .when(query.status !== null && !RESERVATION_STATUSES.includes(query.status as ReservationStatus), 'status', 'status no es un estado de reserva válido.')
      .when(query.fromDate !== null && query.toDate !== null && query.toDate < query.fromDate, 'toDate', 'toDate debe ser igual o posterior a fromDate.')
      .throwIfAny();
    const uow = this.units.create();
    const customer = await this.customers.find(uow, query.user);
    if (!customer) return new PaginationResult<ReservationResult>([], 0, query.limit, query.offset);
    const page = await uow.reservations.getByCustomer(
      customer.customer.id,
      { status: query.status as ReservationStatus | null, fromDate: query.fromDate, toDate: query.toDate, sortDescending: query.sortDescending },
      { limit: query.limit, offset: query.offset },
    );
    return new PaginationResult(page.items.map(toReservationResult), page.totalItems, query.limit, query.offset);
  }

  async get(reservationId: string, user: AuthenticatedUser): Promise<ReservationResult> {
    const uow = this.units.create();
    const customer = await this.customers.find(uow, user);
    const reservation = customer ? await uow.reservations.getByIdForCustomer(reservationId, customer.customer.id) : null;
    // Las reservas ajenas se informan como inexistentes para no revelar su existencia.
    if (!reservation) throw notFound();
    return toReservationResult(reservation);
  }
}
