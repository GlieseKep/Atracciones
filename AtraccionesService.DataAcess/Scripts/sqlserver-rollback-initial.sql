BEGIN TRANSACTION;
DROP TABLE [attraction_availability];

DROP TABLE [attraction_badges];

DROP TABLE [attraction_categories];

DROP TABLE [attraction_includes];

DROP TABLE [attraction_languages];

DROP TABLE [audit_events];

DROP TABLE [idempotency_keys];

DROP TABLE [inventory_movements];

DROP TABLE [locations];

DROP TABLE [order_events];

DROP TABLE [order_items];

DROP TABLE [payment_attempts];

DROP TABLE [payment_events];

DROP TABLE [photos];

DROP TABLE [refund_simulations];

DROP TABLE [role_permissions];

DROP TABLE [user_roles];

DROP TABLE [badges];

DROP TABLE [categories];

DROP TABLE [includes];

DROP TABLE [supported_languages];

DROP TABLE [attractions];

DROP TABLE [payment_simulations];

DROP TABLE [roles];

DROP TABLE [operators];

DROP TABLE [orders];

DROP TABLE [purchases];

DROP TABLE [reservations];

DROP TABLE [customers];

DROP TABLE [users];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261006232040_InitialCreate';

COMMIT;
GO

