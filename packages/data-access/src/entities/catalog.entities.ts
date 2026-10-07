import { Check, Column, Entity, Index, JoinColumn, JoinTable, ManyToMany, ManyToOne, OneToMany, PrimaryColumn, PrimaryGeneratedColumn, Unique } from 'typeorm';
import { currency, instant, money, slotTime } from './columns';

@Entity('operators')
export class OperatorEntity {
  @PrimaryColumn({ type: 'int' })
  id!: number;

  @Column({ type: 'varchar', length: 200 })
  name!: string;
}

/** Valores de catálogo normalizados (categorías, insignias, incluidos, idiomas). */
abstract class CatalogValueEntity {
  @PrimaryGeneratedColumn()
  id!: number;

  @Column({ type: 'varchar', length: 200, unique: true })
  name!: string;
}

@Entity('categories')
export class CategoryEntity extends CatalogValueEntity {}

@Entity('badges')
export class BadgeEntity extends CatalogValueEntity {}

@Entity('includes')
export class InclusionEntity extends CatalogValueEntity {}

@Entity('supported_languages')
export class LanguageEntity extends CatalogValueEntity {}

@Entity('attractions')
@Check('CK_attractions_product_type', `"product_type" IN ('SINGLE_TICKET', 'GUIDED_TOUR', 'PACKAGE')`)
@Check('CK_attractions_price', `"price_amount" > 0`)
export class AttractionEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ type: 'varchar', length: 200 })
  name!: string;

  @Column({ name: 'long_description', type: 'varchar', length: 5000 })
  longDescription!: string;

  @Column({ type: 'varchar', length: 50 })
  duration!: string;

  @Column({ name: 'price_currency', ...currency })
  priceCurrency!: string;

  @Column({ name: 'price_amount', ...money })
  priceAmount!: number;

  @Index()
  @Column({ name: 'product_type', type: 'varchar', length: 20 })
  productType!: string;

  @Column({ name: 'free_cancellation', type: 'boolean', default: false })
  freeCancellation!: boolean;

  @Column({ name: 'operator_id', type: 'int', nullable: true })
  operatorId!: number | null;

  @ManyToOne(() => OperatorEntity, { nullable: true, onDelete: 'RESTRICT' })
  @JoinColumn({ name: 'operator_id' })
  operator!: OperatorEntity | null;

  @Column({ name: 'rating_review_count', type: 'int', nullable: true })
  ratingReviewCount!: number | null;

  @Column({ name: 'rating_score', type: 'double precision', nullable: true })
  ratingScore!: number | null;

  @Column({ name: 'url_web', type: 'varchar', length: 500, nullable: true })
  urlWeb!: string | null;

  @Column({ name: 'url_app', type: 'varchar', length: 500, nullable: true })
  urlApp!: string | null;

  @ManyToMany(() => CategoryEntity)
  @JoinTable({ name: 'attraction_categories', joinColumn: { name: 'attraction_id' }, inverseJoinColumn: { name: 'category_id' } })
  categories!: CategoryEntity[];

  @ManyToMany(() => BadgeEntity)
  @JoinTable({ name: 'attraction_badges', joinColumn: { name: 'attraction_id' }, inverseJoinColumn: { name: 'badge_id' } })
  badges!: BadgeEntity[];

  @ManyToMany(() => InclusionEntity)
  @JoinTable({ name: 'attraction_includes', joinColumn: { name: 'attraction_id' }, inverseJoinColumn: { name: 'inclusion_id' } })
  inclusions!: InclusionEntity[];

  @ManyToMany(() => LanguageEntity)
  @JoinTable({ name: 'attraction_languages', joinColumn: { name: 'attraction_id' }, inverseJoinColumn: { name: 'language_id' } })
  languages!: LanguageEntity[];

  @OneToMany(() => LocationEntity, (l) => l.attraction, { cascade: ['insert'] })
  locations!: LocationEntity[];

  @OneToMany(() => PhotoEntity, (p) => p.attraction, { cascade: ['insert'] })
  photos!: PhotoEntity[];
}

@Entity('locations')
export class LocationEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ name: 'attraction_id', type: 'uuid' })
  attractionId!: string;

  @ManyToOne(() => AttractionEntity, (a) => a.locations, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'attraction_id' })
  attraction?: AttractionEntity;

  @Column({ type: 'int' })
  position!: number;

  @Column({ type: 'varchar', length: 300 })
  address!: string;

  @Index()
  @Column({ type: 'varchar', length: 120 })
  city!: string;

  @Index()
  @Column({ type: 'char', length: 2 })
  country!: string;

  @Column({ type: 'double precision', nullable: true })
  latitude!: number | null;

  @Column({ type: 'double precision', nullable: true })
  longitude!: number | null;

  @Column({ type: 'varchar', length: 60, nullable: true })
  type!: string | null;
}

@Entity('photos')
export class PhotoEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ name: 'attraction_id', type: 'uuid' })
  attractionId!: string;

  @ManyToOne(() => AttractionEntity, (a) => a.photos, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'attraction_id' })
  attraction?: AttractionEntity;

  @Column({ type: 'int' })
  position!: number;

  @Column({ type: 'varchar', length: 1000 })
  url!: string;
}

@Entity('attraction_availability')
@Unique('UQ_availability_slot', ['attractionId', 'date', 'time'])
@Check('CK_availability_capacity', `"capacity" >= 0 AND "reserved_quantity" >= 0 AND "reserved_quantity" <= "capacity"`)
export class AvailabilityEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ name: 'attraction_id', type: 'uuid' })
  attractionId!: string;

  @ManyToOne(() => AttractionEntity, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'attraction_id' })
  attraction?: AttractionEntity;

  @Column({ type: 'date' })
  date!: string;

  @Column({ name: 'time', ...slotTime })
  time!: string;

  @Column({ type: 'int' })
  capacity!: number;

  @Column({ name: 'reserved_quantity', type: 'int', default: 0 })
  reservedQuantity!: number;

  /** Versión de fila para concurrencia optimista. */
  @Column({ type: 'int', default: 0 })
  version!: number;
}

@Entity('reservations')
@Check('CK_reservations_status', `"status" IN ('PENDING', 'CONFIRMED', 'CANCELLED')`)
@Check('CK_reservations_ticket_count', `"ticket_count" > 0`)
@Index(['customerId', 'date'])
export class ReservationEntity {
  @PrimaryColumn('uuid')
  id!: string;

  /** Sin FK: la reserva es histórica y debe sobrevivir al borrado de una atracción sin reservas activas. */
  @Index()
  @Column({ name: 'attraction_id', type: 'uuid' })
  attractionId!: string;

  @Column({ name: 'customer_id', type: 'uuid' })
  customerId!: string;

  @Column({ type: 'date' })
  date!: string;

  @Column({ name: 'time', ...slotTime })
  time!: string;

  @Column({ name: 'ticket_count', type: 'int' })
  ticketCount!: number;

  @Column({ name: 'total_currency', ...currency })
  totalCurrency!: string;

  @Column({ name: 'total_amount', ...money })
  totalAmount!: number;

  @Column({ name: 'customer_name', type: 'varchar', length: 200 })
  customerName!: string;

  @Index()
  @Column({ name: 'customer_email', type: 'varchar', length: 254 })
  customerEmail!: string;

  @Index()
  @Column({ type: 'varchar', length: 20 })
  status!: string;

  @Column({ name: 'cancellation_reason', type: 'varchar', length: 500, nullable: true })
  cancellationReason!: string | null;

  @Index()
  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;
}
