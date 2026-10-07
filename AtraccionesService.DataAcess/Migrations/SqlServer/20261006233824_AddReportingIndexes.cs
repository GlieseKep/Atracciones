using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtraccionesService.DataAcess.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_reservations_CreatedAt",
                table: "reservations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_payment_simulations_CreatedAt",
                table: "payment_simulations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_orders_CreatedAt",
                table: "orders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_AvailabilityId",
                table: "inventory_movements",
                column: "AvailabilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reservations_CreatedAt",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_payment_simulations_CreatedAt",
                table: "payment_simulations");

            migrationBuilder.DropIndex(
                name: "IX_orders_CreatedAt",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_inventory_movements_AvailabilityId",
                table: "inventory_movements");
        }
    }
}
