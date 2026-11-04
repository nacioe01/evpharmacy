using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVPharmacy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSuppliersAndInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false, collation: "NOCASE"),
                    contact_person = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    phone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    address = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_receipts",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    receipt_type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    supplier_id = table.Column<int>(type: "INTEGER", nullable: true),
                    invoice_number = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    received_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    received_by_user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_receipts", x => x.id);
                    table.CheckConstraint("CK_stock_receipts_type", "receipt_type IN ('Delivery', 'OpeningBalance')");
                    table.ForeignKey(
                        name: "FK_stock_receipts_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_receipts_users_received_by_user_id",
                        column: x => x.received_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_batches",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    medicine_id = table.Column<int>(type: "INTEGER", nullable: false),
                    stock_receipt_id = table.Column<int>(type: "INTEGER", nullable: true),
                    batch_number = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    expiry_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    quantity_received = table.Column<int>(type: "INTEGER", nullable: false),
                    quantity_on_hand = table.Column<int>(type: "INTEGER", nullable: false),
                    unit_cost_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    status_note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_batches", x => x.id);
                    table.CheckConstraint("CK_inventory_batches_cost", "unit_cost_centavos >= 0");
                    table.CheckConstraint("CK_inventory_batches_qty", "quantity_on_hand >= 0 AND quantity_received >= 0 AND quantity_on_hand <= quantity_received");
                    table.CheckConstraint("CK_inventory_batches_status", "status IN ('Available', 'Quarantined', 'Recalled')");
                    table.ForeignKey(
                        name: "FK_inventory_batches_medicines_medicine_id",
                        column: x => x.medicine_id,
                        principalTable: "medicines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_batches_stock_receipts_stock_receipt_id",
                        column: x => x.stock_receipt_id,
                        principalTable: "stock_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    medicine_id = table.Column<int>(type: "INTEGER", nullable: false),
                    batch_id = table.Column<int>(type: "INTEGER", nullable: false),
                    movement_type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    quantity_change = table.Column<int>(type: "INTEGER", nullable: false),
                    balance_after = table.Column<int>(type: "INTEGER", nullable: false),
                    stock_receipt_id = table.Column<int>(type: "INTEGER", nullable: true),
                    notes = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.id);
                    table.CheckConstraint("CK_stock_movements_type", "movement_type IN ('OpeningBalance', 'Receipt', 'Sale', 'Adjustment', 'Return')");
                    table.ForeignKey(
                        name: "FK_stock_movements_inventory_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_medicines_medicine_id",
                        column: x => x.medicine_id,
                        principalTable: "medicines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_stock_receipts_stock_receipt_id",
                        column: x => x.stock_receipt_id,
                        principalTable: "stock_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_batches_medicine_id_batch_number",
                table: "inventory_batches",
                columns: new[] { "medicine_id", "batch_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_batches_medicine_id_expiry_date",
                table: "inventory_batches",
                columns: new[] { "medicine_id", "expiry_date" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_batches_stock_receipt_id",
                table: "inventory_batches",
                column: "stock_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_batch_id",
                table: "stock_movements",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_medicine_id_created_at",
                table: "stock_movements",
                columns: new[] { "medicine_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_stock_receipt_id",
                table: "stock_movements",
                column: "stock_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_user_id",
                table: "stock_movements",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_receipts_received_by_user_id",
                table: "stock_receipts",
                column: "received_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_receipts_received_date",
                table: "stock_receipts",
                column: "received_date");

            migrationBuilder.CreateIndex(
                name: "IX_stock_receipts_supplier_id",
                table: "stock_receipts",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_name",
                table: "suppliers",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "inventory_batches");

            migrationBuilder.DropTable(
                name: "stock_receipts");

            migrationBuilder.DropTable(
                name: "suppliers");
        }
    }
}
