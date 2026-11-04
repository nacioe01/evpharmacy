using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVPharmacy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sale_id",
                table: "stock_movements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sales",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    receipt_number = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    sold_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    cashier_user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    subtotal_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    total_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    payment_method = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    amount_tendered_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    change_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    payment_reference = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    rx_verified = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    void_reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    voided_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    voided_by_user_id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales", x => x.id);
                    table.CheckConstraint("CK_sales_amounts", "subtotal_centavos >= 0 AND total_centavos >= 0 AND amount_tendered_centavos >= total_centavos AND change_centavos >= 0");
                    table.CheckConstraint("CK_sales_payment", "payment_method IN ('Cash', 'Card', 'EWallet')");
                    table.CheckConstraint("CK_sales_status", "status IN ('Completed', 'Voided')");
                    table.ForeignKey(
                        name: "FK_sales_users_cashier_user_id",
                        column: x => x.cashier_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_users_voided_by_user_id",
                        column: x => x.voided_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    sale_id = table.Column<int>(type: "INTEGER", nullable: false),
                    medicine_id = table.Column<int>(type: "INTEGER", nullable: false),
                    batch_id = table.Column<int>(type: "INTEGER", nullable: false),
                    medicine_name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    unit_price_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    unit_cost_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    line_total_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    prescription_class = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_items", x => x.id);
                    table.CheckConstraint("CK_sale_items_money", "unit_price_centavos >= 0 AND unit_cost_centavos >= 0 AND line_total_centavos >= 0");
                    table.CheckConstraint("CK_sale_items_qty", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_sale_items_inventory_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_items_medicines_medicine_id",
                        column: x => x.medicine_id,
                        principalTable: "medicines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_items_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_sale_id",
                table: "stock_movements",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_batch_id",
                table: "sale_items",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_medicine_id",
                table: "sale_items",
                column: "medicine_id");

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_sale_id",
                table: "sale_items",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_cashier_user_id",
                table: "sales",
                column: "cashier_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_receipt_number",
                table: "sales",
                column: "receipt_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_sold_at",
                table: "sales",
                column: "sold_at");

            migrationBuilder.CreateIndex(
                name: "IX_sales_voided_by_user_id",
                table: "sales",
                column: "voided_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_sales_sale_id",
                table: "stock_movements",
                column: "sale_id",
                principalTable: "sales",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_sales_sale_id",
                table: "stock_movements");

            migrationBuilder.DropTable(
                name: "sale_items");

            migrationBuilder.DropTable(
                name: "sales");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_sale_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "sale_id",
                table: "stock_movements");
        }
    }
}
