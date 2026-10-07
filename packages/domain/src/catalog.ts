import { DomainError, Guard, newId, type IsoDate, type LocalTime, type Money } from './common';

export const PRODUCT_TYPES = ['SINGLE_TICKET', 'GUIDED_TOUR', 'PACKAGE'] as const;
export type ProductType = (typeof PRODUCT_TYPES)[number];

export interface Location {
  address: string;
  city: string;
  country: string;
  latitude: number | null;
  longitude: number | null;
  type: string | null;
}

export interface OperatorInfo {
  id: number;
  name: string;
}

export interface Rating {
  numberOfReviews: number;
  score: number;
}

export interface AttractionUrls {
  web: string | null;
  app: string | null;
}

/** Valores editables de una atracción; se usan para crear y para reemplazar el recurso completo. */
export interface AttractionDetails {
  name: string;
  longDescription: string;
  duration: string;
  price: Money;
  categories: string[];
  badges: string[];
  locations: Location[];
  photoUrls: string[];
  operator: OperatorInfo | null;
  productType: ProductType;
  includes: string[];
  supportedLanguages: string[];
  freeCancellation: boolean;
}

/** Agregado del catálogo. */
export class Attraction {
  private _details: AttractionDetails;

  private constructor(
    readonly id: string,
    details: AttractionDetails,
    readonly rating: Rating | null,
    readonly urls: AttractionUrls | null,
  ) {
    this._details = Attraction.validate(details);
  }

  get details(): AttractionDetails {
    return this._details;
  }

  static create(details: AttractionDetails): Attraction {
    return new Attraction(newId(), details, null, null);
  }

  /** Reconstruye un agregado persistido. */
  static restore(id: string, details: AttractionDetails, rating: Rating | null, urls: AttractionUrls | null): Attraction {
    return new Attraction(id, details, rating, urls);
  }

  replace(details: AttractionDetails): void {
    this._details = Attraction.validate(details);
  }

  private static validate(details: AttractionDetails): AttractionDetails {
    Guard.notBlank(details.name, 'name', 200);
    Guard.notBlank(details.longDescription, 'longDescription', 5000);
    Guard.notBlank(details.duration, 'duration', 50);
    if (details.categories.length === 0) {
      throw new DomainError('CATEGORIES_REQUIRED', 'La atracción requiere al menos una categoría.');
    }
    if (details.locations.length === 0) {
      throw new DomainError('LOCATIONS_REQUIRED', 'La atracción requiere al menos una ubicación.');
    }
    if (!PRODUCT_TYPES.includes(details.productType)) {
      throw new DomainError('INVALID_PRODUCT_TYPE', 'Tipo de producto no soportado.');
    }
    return details;
  }
}

/**
 * Franja reservable de una atracción (fecha y hora locales). Una fila por atracción, fecha y hora.
 * `version` permite concurrencia optimista en DataAccess.
 */
export class AvailabilitySlot {
  private _reservedQuantity: number;
  private _version: number;

  constructor(
    readonly id: string,
    readonly attractionId: string,
    readonly date: IsoDate,
    readonly time: LocalTime,
    readonly capacity: number,
    reservedQuantity: number,
    version: number,
  ) {
    if (capacity < 0 || reservedQuantity < 0 || reservedQuantity > capacity) {
      throw new DomainError('INVALID_CAPACITY', 'La capacidad reservada no puede superar la capacidad total.');
    }
    this._reservedQuantity = reservedQuantity;
    this._version = version;
  }

  get reservedQuantity(): number {
    return this._reservedQuantity;
  }

  get version(): number {
    return this._version;
  }

  get availableSpots(): number {
    return this.capacity - this._reservedQuantity;
  }

  canReserve(quantity: number): boolean {
    return quantity > 0 && quantity <= this.availableSpots;
  }

  reserve(quantity: number): void {
    if (!this.canReserve(quantity)) {
      throw new DomainError('INSUFFICIENT_AVAILABILITY', 'No hay cupos suficientes en la franja.');
    }
    this._reservedQuantity += quantity;
    this._version++;
  }

  release(quantity: number): void {
    Guard.positive(quantity, 'quantity');
    this._reservedQuantity = Math.max(0, this._reservedQuantity - quantity);
    this._version++;
  }
}
