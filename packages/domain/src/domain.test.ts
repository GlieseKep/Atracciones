import { Attraction, AvailabilitySlot, DomainError, Money, Order, PaymentSimulation, Reservation } from './index';

const now = new Date('2026-10-07T12:00:00Z');
const usd = (amount: number) => new Money('USD', amount);

describe('Money', () => {
  it('rechaza monedas no ISO, importes no positivos y más de dos decimales', () => {
    expect(() => new Money('usd', 1)).toThrow(DomainError);
    expect(() => new Money('USD', 0)).toThrow(DomainError);
    expect(() => new Money('USD', 1.005)).toThrow(DomainError);
  });

  it('multiplica sin errores de coma flotante', () => {
    expect(usd(0.1).multiply(3).amount).toBe(0.3);
    expect(usd(9.5).multiply(2).amount).toBe(19);
  });
});

describe('AvailabilitySlot', () => {
  it('reserva y libera cupos sin superar la capacidad', () => {
    const slot = new AvailabilitySlot('s', 'a', '2026-12-01', '09:00', 3, 1, 0);
    expect(slot.availableSpots).toBe(2);
    slot.reserve(2);
    expect(slot.availableSpots).toBe(0);
    expect(() => slot.reserve(1)).toThrow(DomainError);
    slot.release(5);
    expect(slot.reservedQuantity).toBe(0);
    expect(slot.version).toBe(2);
  });

  it('no admite más reservados que capacidad', () => {
    expect(() => new AvailabilitySlot('s', 'a', '2026-12-01', '09:00', 1, 2, 0)).toThrow(DomainError);
  });
});

describe('Reservation', () => {
  const create = (status: 'PENDING' | 'CONFIRMED') =>
    Reservation.create({
      attractionId: 'a', customerId: 'c', date: '2026-12-01', time: '09:00', ticketCount: 2, totalPrice: usd(20),
      customerName: 'Ana', customerEmail: 'ana@example.com', initialStatus: status, now,
    });

  it('confirma solo desde PENDING y cancela con motivo', () => {
    const reservation = create('PENDING');
    reservation.confirm();
    expect(reservation.status).toBe('CONFIRMED');
    expect(() => reservation.confirm()).toThrow(DomainError);
    reservation.cancel('Cambio de planes');
    expect(reservation.status).toBe('CANCELLED');
    expect(reservation.cancellationReason).toBe('Cambio de planes');
    expect(() => reservation.cancel('otra vez')).toThrow(DomainError);
  });

  it('no puede crearse cancelada ni con cero entradas', () => {
    expect(() =>
      Reservation.create({
        attractionId: 'a', customerId: 'c', date: '2026-12-01', time: '09:00', ticketCount: 1, totalPrice: usd(1),
        customerName: 'Ana', customerEmail: 'a@b.co', initialStatus: 'CANCELLED', now,
      }),
    ).toThrow(DomainError);
    expect(() =>
      Reservation.create({
        attractionId: 'a', customerId: 'c', date: '2026-12-01', time: '09:00', ticketCount: 0, totalPrice: usd(1),
        customerName: 'Ana', customerEmail: 'a@b.co', initialStatus: 'PENDING', now,
      }),
    ).toThrow(DomainError);
  });
});

describe('Order', () => {
  const item = (price: Money, quantity = 2) => ({
    id: 'i', attractionId: 'a', serviceDate: '2026-12-01', serviceTime: '09:00', quantity, unitPrice: price, availabilitySlotId: 's',
  });

  it('calcula el total y aplica las transiciones permitidas', () => {
    const order = Order.placePending({ customerId: 'c', purchaseId: 'p', reservationId: 'r', items: [item(usd(12.5))], holdExpiresAt: new Date(now.getTime() + 60_000), now });
    expect(order.total.amount).toBe(25);
    expect(order.isHoldExpired(new Date(now.getTime() + 120_000))).toBe(true);
    expect(order.transitionTo('PAID', now)).toBe('PENDING_PAYMENT');
    expect(order.holdExpiresAt).toBeNull();
    expect(order.canTransitionTo('PENDING_PAYMENT')).toBe(false);
    expect(() => order.transitionTo('PENDING_PAYMENT', now)).toThrow(DomainError);
  });

  it('exige motivo al cancelar y rechaza monedas mezcladas', () => {
    const order = Order.placePending({ customerId: 'c', purchaseId: null, reservationId: null, items: [item(usd(1))], holdExpiresAt: now, now });
    expect(() => order.transitionTo('CANCELLED', now)).toThrow(DomainError);
    expect(() =>
      Order.placePending({ customerId: 'c', purchaseId: null, reservationId: null, items: [item(usd(1)), item(new Money('EUR', 1))], holdExpiresAt: now, now }),
    ).toThrow(DomainError);
  });
});

describe('PaymentSimulation', () => {
  it('sigue PENDING → AUTHORIZED → SETTLED y registra el motivo de rechazo', () => {
    const payment = PaymentSimulation.start('o', 'CARD', usd(10), now);
    expect(payment.gatewayReference).toMatch(/^SIM-[0-9A-F]{12}$/);
    payment.transitionTo('AUTHORIZED', now);
    payment.transitionTo('SETTLED', now);
    expect(() => payment.transitionTo('PENDING', now)).toThrow(DomainError);

    const rejected = PaymentSimulation.start('o', 'CARD', usd(10), now);
    rejected.transitionTo('REJECTED', now, 'Fondos insuficientes');
    expect(rejected.failureReason).toBe('Fondos insuficientes');
  });
});

describe('Attraction', () => {
  it('exige categorías y ubicaciones', () => {
    const details = {
      name: 'Tour', longDescription: 'x', duration: 'PT1H', price: usd(5), categories: [], badges: [], locations: [],
      photoUrls: [], operator: null, productType: 'GUIDED_TOUR' as const, includes: [], supportedLanguages: [], freeCancellation: true,
    };
    expect(() => Attraction.create(details)).toThrow(DomainError);
  });
});
