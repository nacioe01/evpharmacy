using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVPharmacy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_batches_qty",
                table: "inventory_batches");

            migrationBuilder.AddColumn<string>(
                name: "adjustment_reason",
                table: "stock_movements",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_batches_qty",
                table: "inventory_batches",
                sql: "quantity_on_hand >= 0 AND quantity_received >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_batches_qty",
                table: "inventory_batches");

            migrationBuilder.DropColumn(
                name: "adjustment_reason",
                table: "stock_movements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_batches_qty",
                table: "inventory_batches",
                sql: "quantity_on_hand >= 0 AND quantity_received >= 0 AND quantity_on_hand <= quantity_received");
        }
    }
}
