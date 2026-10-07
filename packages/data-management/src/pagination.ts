/**
 * Página solicitada: `offset` y `limit`, con orden opcional (`campo` ascendente o `-campo` descendente).
 * Business valida los valores antes de llegar aquí.
 */
export interface PaginationRequest {
  limit: number;
  offset: number;
  sort?: string;
}

export class PagedResult<T> {
  constructor(
    readonly items: T[],
    readonly totalItems: number,
    readonly pageSize = 0,
    readonly offset = 0,
  ) {}

  get pageNumber(): number {
    return this.pageSize <= 0 ? 1 : Math.floor(this.offset / this.pageSize) + 1;
  }

  get totalPages(): number {
    if (this.pageSize <= 0) return this.totalItems > 0 ? 1 : 0;
    return Math.ceil(this.totalItems / this.pageSize);
  }

  static create<T>(items: T[], totalItems: number, page: PaginationRequest): PagedResult<T> {
    return new PagedResult(items, totalItems, page.limit, page.offset);
  }

  map<TOut>(fn: (item: T) => TOut): PagedResult<TOut> {
    return new PagedResult(this.items.map(fn), this.totalItems, this.pageSize, this.offset);
  }
}

/** Error de persistencia expresado sin detalles del proveedor. */
export class DataManagementError extends Error {
  constructor(message: string, options?: { cause?: unknown }) {
    super(message, options);
    this.name = 'DataManagementError';
  }
}

/** Otra transacción modificó el registro o se violó una restricción de unicidad. */
export class ConcurrencyError extends DataManagementError {
  constructor(message: string, options?: { cause?: unknown }) {
    super(message, options);
    this.name = 'ConcurrencyError';
  }
}
