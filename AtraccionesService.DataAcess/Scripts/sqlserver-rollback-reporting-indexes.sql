BEGIN TRANSACTION;
DROP INDEX [IX_reservations_CreatedAt] ON [reservations];

DROP INDEX [IX_payment_simulations_CreatedAt] ON [payment_simulations];

DROP INDEX [IX_orders_CreatedAt] ON [orders];

DROP INDEX [IX_inventory_movements_AvailabilityId] ON [inventory_movements];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261006233824_AddReportingIndexes';

COMMIT;
GO

