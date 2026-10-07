import { DomainError, Guard, newId, type IsoDate, type LocalTime, type Money } from './common';

export const RESERVATION_STATUSES = ['PENDING', 'CONFIRMED', 'CANCELLED'] as const;
export type ReservationStatus = (typeof RESERVATION_STATUSES)[number];

/** Reserva de una franja. Transiciones: PENDING → CONFIRMED | CANCELLED; CONFIRMED → CANCELLED. */
export class Reservation {
  readonly ticketCount: number;
  readonly customerName: string;
  readonly customerEmail: string;
  private _status: ReservationStatus;
  private _cancellationReason: string | null;

  private constructor(
    readonly id: string,
    readonly attractionId: string,
    readonly customerId: string,
    readonly date: IsoDate,
    readonly time: LocalTime,
    ticketCount: number,
    readonly totalPrice: Money,
    customerName: string,
    customerEmail: string,
    status: ReservationStatus,
    cancellationReason: string | null,
    readonly createdAt: Date,
  ) {
    this.ticketCount = Guard.positive(ticketCount, 'ticketCount');
    this.customerName = Guard.notBlank(customerName, 'customerName', 200);
    this.customerEmail = Guard.notBlank(customerEmail, 'customerEmail', 254);
    this._status = status;
    this._cancellationReason = cancellationReason;
  }

  get status(): ReservationStatus {
    return this._status;
  }

  get cancellationReason(): string | null {
    return this._cancellationReason;
  }

  get canCancel(): boolean {
    return this._status === 'PENDING' || this._status === 'CONFIRMED';
  }

  static create(args: {
    attractionId: string;
    customerId: string;
    date: IsoDate;
    time: LocalTime;
    ticketCount: number;
    totalPrice: Money;
    customerName: string;
    customerEmail: string;
    initialStatus: ReservationStatus;
    now: Date;
  }): Reservation {
    if (args.initialStatus === 'CANCELLED') {
      throw new DomainError('INVALID_INITIAL_STATUS', 'Una reserva no puede crearse cancelada.');
    }
    return new Reservation(
      newId(), args.attractionId, args.customerId, args.date, args.time, args.ticketCount, args.totalPrice,
      args.customerName, args.customerEmail, args.initialStatus, null, args.now,
    );
  }

  static restore(args: {
    id: string;
    attractionId: string;
    customerId: string;
    date: IsoDate;
    time: LocalTime;
    ticketCount: number;
    totalPrice: Money;
    customerName: string;
    customerEmail: string;
    status: ReservationStatus;
    cancellationReason: string | null;
    createdAt: Date;
  }): Reservation {
    return new Reservation(
      args.id, args.attractionId, args.customerId, args.date, args.time, args.ticketCount, args.totalPrice,
      args.customerName, args.customerEmail, args.status, args.cancellationReason, args.createdAt,
    );
  }

  confirm(): void {
    if (this._status !== 'PENDING') {
      throw new DomainError('INVALID_STATE_TRANSITION', `No se puede confirmar una reserva ${this._status}.`);
    }
    this._status = 'CONFIRMED';
  }

  cancel(reason: string): void {
    if (!this.canCancel) {
      throw new DomainError('INVALID_STATE_TRANSITION', `No se puede cancelar una reserva ${this._status}.`);
    }
    this._cancellationReason = Guard.notBlank(reason, 'reason', 500);
    this._status = 'CANCELLED';
  }
}
