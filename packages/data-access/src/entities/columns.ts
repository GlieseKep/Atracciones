import type { ValueTransformer } from 'typeorm';

/** `numeric(18,2)` llega como texto desde `pg`: se convierte a número para no perder el tipo. */
export const decimal: ValueTransformer = {
  to: (value: number | null | undefined) => value,
  from: (value: string | null) => (value === null || value === undefined ? value : Number(value)),
};

/** `time` llega como `HH:mm:ss`; el dominio trabaja con `HH:mm`. */
export const localTime: ValueTransformer = {
  to: (value: string | null | undefined) => value,
  from: (value: string | null) => (value === null || value === undefined ? value : value.slice(0, 5)),
};

export const money = { type: 'numeric', precision: 18, scale: 2, transformer: decimal } as const;
export const currency = { type: 'char', length: 3 } as const;
export const instant = { type: 'timestamptz' } as const;
export const slotTime = { type: 'time', transformer: localTime } as const;
