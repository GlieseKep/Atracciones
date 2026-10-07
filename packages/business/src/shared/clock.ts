import type { IsoDate, LocalTime } from '@atracciones/domain';

/** Fecha y hora actuales en la zona horaria de negocio configurada. */
export class BusinessClock {
  private readonly formatter: Intl.DateTimeFormat;

  constructor(
    readonly timeZone: string,
    private readonly now: () => Date = () => new Date(),
  ) {
    this.formatter = new Intl.DateTimeFormat('en-CA', {
      timeZone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hourCycle: 'h23',
    });
  }

  get utcNow(): Date {
    return this.now();
  }

  /** `YYYY-MM-DDTHH:mm:ss` en la zona de negocio. */
  get localNow(): string {
    const parts = Object.fromEntries(this.formatter.formatToParts(this.now()).map((p) => [p.type, p.value]));
    return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}:${parts.second}`;
  }

  get today(): IsoDate {
    return this.localNow.slice(0, 10);
  }

  /** Una franja es futura si su fecha y hora locales todavía no han llegado. */
  isFuture(date: IsoDate, time: LocalTime): boolean {
    return `${date}T${time}:00` > this.localNow;
  }
}
