import { Check, Column, Entity, Index, JoinColumn, ManyToOne, OneToMany, OneToOne, PrimaryColumn, Unique } from 'typeorm';
import { instant } from './columns';

@Entity('users')
@Unique('UQ_users_identity', ['oauthIssuer', 'oauthSubject'])
@Check('CK_users_status', `"status" IN ('ACTIVE', 'LOCKED', 'DISABLED')`)
export class UserEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ name: 'oauth_issuer', type: 'varchar', length: 300 })
  oauthIssuer!: string;

  @Column({ name: 'oauth_subject', type: 'varchar', length: 300 })
  oauthSubject!: string;

  @Index()
  @Column({ type: 'varchar', length: 254 })
  email!: string;

  @Column({ type: 'varchar', length: 20 })
  status!: string;

  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;

  @Column({ name: 'updated_at', ...instant })
  updatedAt!: Date;
}

@Entity('customers')
export class CustomerEntity {
  @PrimaryColumn('uuid')
  id!: string;

  /** Único por la relación 1:1 con `users`. */
  @Column({ name: 'user_id', type: 'uuid' })
  userId!: string;

  @OneToOne(() => UserEntity, { onDelete: 'RESTRICT' })
  @JoinColumn({ name: 'user_id' })
  user?: UserEntity;

  @Column({ name: 'billing_name', type: 'varchar', length: 200, nullable: true })
  billingName!: string | null;

  @Column({ name: 'billing_email', type: 'varchar', length: 254, nullable: true })
  billingEmail!: string | null;

  @Column({ name: 'billing_address', type: 'varchar', length: 300, nullable: true })
  billingAddress!: string | null;

  @Column({ name: 'tax_id', type: 'varchar', length: 30, nullable: true })
  taxId!: string | null;

  @Column({ name: 'payment_method_reference', type: 'varchar', length: 64, nullable: true })
  paymentMethodReference!: string | null;

  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;

  @Column({ name: 'updated_at', ...instant })
  updatedAt!: Date;
}

/** Rol local con sus permisos. No representa scopes del emisor de tokens. */
@Entity('roles')
export class RoleEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ type: 'varchar', length: 60, unique: true })
  name!: string;

  @Column({ type: 'varchar', length: 300, nullable: true })
  description!: string | null;

  @Column({ name: 'created_at', ...instant })
  createdAt!: Date;

  @OneToMany(() => RolePermissionEntity, (p) => p.role)
  permissions?: RolePermissionEntity[];
}

@Entity('role_permissions')
export class RolePermissionEntity {
  @PrimaryColumn({ name: 'role_id', type: 'uuid' })
  roleId!: string;

  @PrimaryColumn({ type: 'varchar', length: 100 })
  permission!: string;

  @ManyToOne(() => RoleEntity, (r) => r.permissions, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'role_id' })
  role?: RoleEntity;
}

/** Asignación de rol con actor, motivo y fecha; `revokedAt` la desactiva sin borrarla. */
@Entity('user_roles')
@Index(['userId', 'roleId'])
export class UserRoleEntity {
  @PrimaryColumn('uuid')
  id!: string;

  @Column({ name: 'user_id', type: 'uuid' })
  userId!: string;

  @ManyToOne(() => UserEntity, { onDelete: 'RESTRICT' })
  @JoinColumn({ name: 'user_id' })
  user?: UserEntity;

  @Column({ name: 'role_id', type: 'uuid' })
  roleId!: string;

  @ManyToOne(() => RoleEntity, { onDelete: 'RESTRICT' })
  @JoinColumn({ name: 'role_id' })
  role?: RoleEntity;

  @Column({ name: 'assigned_by_user_id', type: 'uuid', nullable: true })
  assignedByUserId!: string | null;

  @ManyToOne(() => UserEntity, { onDelete: 'RESTRICT', nullable: true })
  @JoinColumn({ name: 'assigned_by_user_id' })
  assignedBy?: UserEntity | null;

  @Column({ type: 'varchar', length: 500 })
  reason!: string;

  @Column({ name: 'assigned_at', ...instant })
  assignedAt!: Date;

  @Column({ name: 'revoked_at', ...instant, nullable: true })
  revokedAt!: Date | null;
}
