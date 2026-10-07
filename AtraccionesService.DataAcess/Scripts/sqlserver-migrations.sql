IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [audit_events] (
        [Id] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [ActorSubject] nvarchar(300) NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [ResourceType] nvarchar(60) NOT NULL,
        [ResourceId] nvarchar(100) NOT NULL,
        [Reason] nvarchar(500) NULL,
        [Metadata] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_audit_events] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [badges] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(60) NOT NULL,
        CONSTRAINT [PK_badges] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(60) NOT NULL,
        CONSTRAINT [PK_categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [idempotency_keys] (
        [Id] uniqueidentifier NOT NULL,
        [Issuer] nvarchar(300) NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [Operation] nvarchar(60) NOT NULL,
        [Key] uniqueidentifier NOT NULL,
        [RequestHash] nvarchar(64) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ResourceId] uniqueidentifier NULL,
        [ResponseBody] nvarchar(max) NULL,
        [ResponseHash] nvarchar(64) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_idempotency_keys] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_idempotency_keys_status] CHECK (Status IN ('IN_PROGRESS', 'COMPLETED'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [includes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_includes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [inventory_movements] (
        [Id] uniqueidentifier NOT NULL,
        [AttractionId] uniqueidentifier NOT NULL,
        [AvailabilityId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [MovementType] nvarchar(20) NOT NULL,
        [PurchaseId] uniqueidentifier NULL,
        [ReservationId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_inventory_movements] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_inventory_movements_type] CHECK (MovementType IN ('HELD', 'CONFIRMED', 'RELEASED', 'CANCELLED'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [operators] (
        [Id] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_operators] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        [Description] nvarchar(300) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [supported_languages] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(10) NOT NULL,
        CONSTRAINT [PK_supported_languages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [users] (
        [Id] uniqueidentifier NOT NULL,
        [OauthIssuer] nvarchar(300) NOT NULL,
        [OauthSubject] nvarchar(300) NOT NULL,
        [Email] nvarchar(254) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_users] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_users_status] CHECK (Status IN ('ACTIVE', 'LOCKED', 'DISABLED'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [attractions] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [LongDescription] nvarchar(max) NOT NULL,
        [Duration] nvarchar(50) NOT NULL,
        [ProductType] nvarchar(20) NOT NULL,
        [FreeCancellation] bit NOT NULL,
        [OperatorId] int NULL,
        [RatingReviewCount] int NULL,
        [RatingScore] float NULL,
        [UrlWeb] nvarchar(500) NULL,
        [UrlApp] nvarchar(500) NULL,
        [PriceAmount] decimal(18,2) NOT NULL,
        [PriceCurrency] nvarchar(3) NOT NULL,
        CONSTRAINT [PK_attractions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_attractions_product_type] CHECK (ProductType IN ('SINGLE_TICKET', 'GUIDED_TOUR', 'PACKAGE')),
        CONSTRAINT [FK_attractions_operators_OperatorId] FOREIGN KEY ([OperatorId]) REFERENCES [operators] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [role_permissions] (
        [RoleId] uniqueidentifier NOT NULL,
        [Permission] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_role_permissions] PRIMARY KEY ([RoleId], [Permission]),
        CONSTRAINT [FK_role_permissions_roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [customers] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [BillingName] nvarchar(200) NULL,
        [BillingEmail] nvarchar(254) NULL,
        [BillingAddress] nvarchar(300) NULL,
        [TaxId] nvarchar(30) NULL,
        [PaymentMethodReference] nvarchar(64) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_customers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_customers_users_UserId] FOREIGN KEY ([UserId]) REFERENCES [users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [user_roles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [AssignedByUserId] uniqueidentifier NULL,
        [Reason] nvarchar(500) NOT NULL,
        [AssignedAt] datetimeoffset NOT NULL,
        [RevokedAt] datetimeoffset NULL,
        CONSTRAINT [PK_user_roles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_user_roles_roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [roles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_user_roles_users_AssignedByUserId] FOREIGN KEY ([AssignedByUserId]) REFERENCES [users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_user_roles_users_UserId] FOREIGN KEY ([UserId]) REFERENCES [users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [attraction_availability] (
        [Id] uniqueidentifier NOT NULL,
        [AttractionId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Time] time NOT NULL,
        [Capacity] int NOT NULL,
        [ReservedQuantity] int NOT NULL,
        [Version] bigint NOT NULL,
        CONSTRAINT [PK_attraction_availability] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_availability_capacity] CHECK (Capacity >= 0 AND ReservedQuantity >= 0 AND ReservedQuantity <= Capacity),
        CONSTRAINT [FK_attraction_availability_attractions_AttractionId] FOREIGN KEY ([AttractionId]) REFERENCES [attractions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [attraction_badges] (
        [AttractionId] uniqueidentifier NOT NULL,
        [BadgeId] int NOT NULL,
        CONSTRAINT [PK_attraction_badges] PRIMARY KEY ([AttractionId], [BadgeId]),
        CONSTRAINT [FK_attraction_badges_attractions_AttractionId] FOREIGN KEY ([AttractionId]) REFERENCES [attractions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_attraction_badges_badges_BadgeId] FOREIGN KEY ([BadgeId]) REFERENCES [badges] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [attraction_categories] (
        [AttractionId] uniqueidentifier NOT NULL,
        [CategoryId] int NOT NULL,
        CONSTRAINT [PK_attraction_categories] PRIMARY KEY ([AttractionId], [CategoryId]),
        CONSTRAINT [FK_attraction_categories_attractions_AttractionId] FOREIGN KEY ([AttractionId]) REFERENCES [attractions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_attraction_categories_categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [categories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [attraction_includes] (
        [AttractionId] uniqueidentifier NOT NULL,
        [InclusionId] int NOT NULL,
        CONSTRAINT [PK_attraction_includes] PRIMARY KEY ([AttractionId], [InclusionId]),
        CONSTRAINT [FK_attraction_includes_attractions_AttractionId] FOREIGN KEY ([AttractionId]) REFERENCES [attractions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_attraction_includes_includes_InclusionId] FOREIGN KEY ([InclusionId]) REFERENCES [includes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [attraction_languages] (
        [AttractionId] uniqueidentifier NOT NULL,
        [LanguageId] int NOT NULL,
        CONSTRAINT [PK_attraction_languages] PRIMARY KEY ([AttractionId], [LanguageId]),
        CONSTRAINT [FK_attraction_languages_attractions_AttractionId] FOREIGN KEY ([AttractionId]) REFERENCES [attractions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_attraction_languages_supported_languages_LanguageId] FOREIGN KEY ([LanguageId]) REFERENCES [supported_languages] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [locations] (
        [Id] uniqueidentifier NOT NULL,
        [AttractionId] uniqueidentifier NOT NULL,
        [Position] int NOT NULL,
        [Address] nvarchar(300) NOT NULL,
        [City] nvarchar(120) NOT NULL,
        [Country] nvarchar(2) NOT NULL,
        [Latitude] float NULL,
        [Longitude] float NULL,
        [Type] nvarchar(60) NULL,
        CONSTRAINT [PK_locations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_locations_attractions_AttractionId] FOREIGN KEY ([AttractionId]) REFERENCES [attractions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [photos] (
        [Id] uniqueidentifier NOT NULL,
        [AttractionId] uniqueidentifier NOT NULL,
        [Position] int NOT NULL,
        [Url] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_photos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_photos_attractions_AttractionId] FOREIGN KEY ([AttractionId]) REFERENCES [attractions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [purchases] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [AttractionId] uniqueidentifier NOT NULL,
        [ServiceDate] date NOT NULL,
        [ServiceTime] time NOT NULL,
        [Quantity] int NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [RequestIdempotencyKey] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UnitPriceAmount] decimal(18,2) NOT NULL,
        [UnitPriceCurrency] nvarchar(3) NOT NULL,
        CONSTRAINT [PK_purchases] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_purchases_quantity] CHECK (Quantity > 0),
        CONSTRAINT [FK_purchases_customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [reservations] (
        [Id] uniqueidentifier NOT NULL,
        [AttractionId] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Time] time NOT NULL,
        [TicketCount] int NOT NULL,
        [CustomerName] nvarchar(200) NOT NULL,
        [CustomerEmail] nvarchar(254) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [TotalCurrency] nvarchar(3) NOT NULL,
        CONSTRAINT [PK_reservations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_reservations_status] CHECK (Status IN ('PENDING', 'CONFIRMED', 'CANCELLED')),
        CONSTRAINT [CK_reservations_ticket_count] CHECK (TicketCount > 0),
        CONSTRAINT [FK_reservations_customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [orders] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [PurchaseId] uniqueidentifier NULL,
        [ReservationId] uniqueidentifier NULL,
        [Status] nvarchar(30) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [HoldExpiresAt] datetimeoffset NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_orders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_orders_status] CHECK (Status IN ('PENDING_PAYMENT', 'PAID', 'FULFILLED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED')),
        CONSTRAINT [FK_orders_customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_orders_purchases_PurchaseId] FOREIGN KEY ([PurchaseId]) REFERENCES [purchases] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_orders_reservations_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [reservations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [order_events] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(60) NOT NULL,
        [PreviousStatus] nvarchar(30) NULL,
        [NewStatus] nvarchar(30) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_order_events] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_order_events_orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [orders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [order_items] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [AttractionId] uniqueidentifier NOT NULL,
        [AvailabilityId] uniqueidentifier NOT NULL,
        [ServiceDate] date NOT NULL,
        [ServiceTime] time NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPriceAmount] decimal(18,2) NOT NULL,
        [UnitPriceCurrency] nvarchar(3) NOT NULL,
        CONSTRAINT [PK_order_items] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_order_items_quantity] CHECK (Quantity > 0),
        CONSTRAINT [FK_order_items_orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [orders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [payment_simulations] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [PaymentMethod] nvarchar(20) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [GatewayReference] nvarchar(64) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ProcessedAt] datetimeoffset NULL,
        [FailureReason] nvarchar(300) NULL,
        CONSTRAINT [PK_payment_simulations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_payment_simulations_amount] CHECK (Amount > 0),
        CONSTRAINT [CK_payment_simulations_method] CHECK (PaymentMethod IN ('CARD', 'BANK_TRANSFER')),
        CONSTRAINT [CK_payment_simulations_status] CHECK (Status IN ('PENDING', 'AUTHORIZED', 'SETTLED', 'REJECTED', 'FAILED', 'CANCELLED', 'PARTIALLY_REFUNDED', 'REFUNDED')),
        CONSTRAINT [FK_payment_simulations_orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [orders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [payment_attempts] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentSimulationId] uniqueidentifier NOT NULL,
        [AttemptNumber] int NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [ResponseCode] nvarchar(10) NOT NULL,
        [ResponseMessage] nvarchar(300) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_payment_attempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_payment_attempts_payment_simulations_PaymentSimulationId] FOREIGN KEY ([PaymentSimulationId]) REFERENCES [payment_simulations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [payment_events] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentSimulationId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(60) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_payment_events] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_payment_events_payment_simulations_PaymentSimulationId] FOREIGN KEY ([PaymentSimulationId]) REFERENCES [payment_simulations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE TABLE [refund_simulations] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentSimulationId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ProcessedAt] datetimeoffset NULL,
        CONSTRAINT [PK_refund_simulations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_refund_simulations_amount] CHECK (Amount > 0),
        CONSTRAINT [CK_refund_simulations_status] CHECK (Status IN ('PENDING', 'SETTLED', 'FAILED', 'CANCELLED')),
        CONSTRAINT [FK_refund_simulations_payment_simulations_PaymentSimulationId] FOREIGN KEY ([PaymentSimulationId]) REFERENCES [payment_simulations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_refund_simulations_users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_attraction_availability_AttractionId_Date_Time] ON [attraction_availability] ([AttractionId], [Date], [Time]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_attraction_badges_BadgeId] ON [attraction_badges] ([BadgeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_attraction_categories_CategoryId] ON [attraction_categories] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_attraction_includes_InclusionId] ON [attraction_includes] ([InclusionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_attraction_languages_LanguageId] ON [attraction_languages] ([LanguageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_attractions_OperatorId] ON [attractions] ([OperatorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_attractions_ProductType] ON [attractions] ([ProductType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_audit_events_CreatedAt] ON [audit_events] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_audit_events_ResourceType_ResourceId] ON [audit_events] ([ResourceType], [ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_badges_Name] ON [badges] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_categories_Name] ON [categories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_customers_UserId] ON [customers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_idempotency_keys_ExpiresAt] ON [idempotency_keys] ([ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_idempotency_keys_Issuer_Subject_Operation_Key] ON [idempotency_keys] ([Issuer], [Subject], [Operation], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_idempotency_keys_Key_Operation] ON [idempotency_keys] ([Key], [Operation]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_includes_Name] ON [includes] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_inventory_movements_AttractionId] ON [inventory_movements] ([AttractionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_inventory_movements_PurchaseId] ON [inventory_movements] ([PurchaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_inventory_movements_ReservationId] ON [inventory_movements] ([ReservationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_locations_AttractionId] ON [locations] ([AttractionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_locations_City] ON [locations] ([City]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_locations_Country] ON [locations] ([Country]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_events_OrderId_CreatedAt] ON [order_events] ([OrderId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_items_AvailabilityId] ON [order_items] ([AvailabilityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_items_OrderId] ON [order_items] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_orders_CustomerId] ON [orders] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_orders_PurchaseId] ON [orders] ([PurchaseId]) WHERE [PurchaseId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_orders_ReservationId] ON [orders] ([ReservationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_orders_Status] ON [orders] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_payment_attempts_PaymentSimulationId_AttemptNumber] ON [payment_attempts] ([PaymentSimulationId], [AttemptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_payment_events_PaymentSimulationId_CreatedAt] ON [payment_events] ([PaymentSimulationId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_payment_simulations_GatewayReference] ON [payment_simulations] ([GatewayReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_payment_simulations_OrderId] ON [payment_simulations] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_payment_simulations_Status] ON [payment_simulations] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_photos_AttractionId] ON [photos] ([AttractionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_purchases_AttractionId_ServiceDate] ON [purchases] ([AttractionId], [ServiceDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_purchases_CustomerId] ON [purchases] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_refund_simulations_CreatedByUserId] ON [refund_simulations] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_refund_simulations_PaymentSimulationId] ON [refund_simulations] ([PaymentSimulationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_reservations_AttractionId] ON [reservations] ([AttractionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_reservations_CustomerEmail] ON [reservations] ([CustomerEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_reservations_CustomerId_Date] ON [reservations] ([CustomerId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_reservations_Status] ON [reservations] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_roles_Name] ON [roles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_supported_languages_Name] ON [supported_languages] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_user_roles_AssignedByUserId] ON [user_roles] ([AssignedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_user_roles_RoleId] ON [user_roles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_user_roles_UserId_RoleId] ON [user_roles] ([UserId], [RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_users_Email] ON [users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_users_OauthIssuer_OauthSubject] ON [users] ([OauthIssuer], [OauthSubject]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006232040_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006232040_InitialCreate', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006233824_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_reservations_CreatedAt] ON [reservations] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006233824_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_payment_simulations_CreatedAt] ON [payment_simulations] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006233824_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_orders_CreatedAt] ON [orders] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006233824_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_inventory_movements_AvailabilityId] ON [inventory_movements] ([AvailabilityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006233824_AddReportingIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006233824_AddReportingIndexes', N'10.0.5');
END;

COMMIT;
GO

