using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EVPharmacy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicineCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false, collation: "NOCASE"),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dosage_forms",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false, collation: "NOCASE"),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dosage_forms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "units_of_measure",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false, collation: "NOCASE"),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units_of_measure", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "medicines",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    generic_name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    brand_name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    barcode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    category_id = table.Column<int>(type: "INTEGER", nullable: false),
                    dosage_form_id = table.Column<int>(type: "INTEGER", nullable: false),
                    strength = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    unit_of_measure_id = table.Column<int>(type: "INTEGER", nullable: false),
                    purchase_cost_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    selling_price_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    reorder_level = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    prescription_class = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    is_vat_exempt = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medicines", x => x.id);
                    table.CheckConstraint("CK_medicines_prices", "purchase_cost_centavos >= 0 AND selling_price_centavos >= 0");
                    table.CheckConstraint("CK_medicines_reorder", "reorder_level >= 0");
                    table.CheckConstraint("CK_medicines_rx", "prescription_class IN ('otc', 'rx', 'controlled')");
                    table.ForeignKey(
                        name: "FK_medicines_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medicines_dosage_forms_dosage_form_id",
                        column: x => x.dosage_form_id,
                        principalTable: "dosage_forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medicines_units_of_measure_unit_of_measure_id",
                        column: x => x.unit_of_measure_id,
                        principalTable: "units_of_measure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "medicine_price_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    medicine_id = table.Column<int>(type: "INTEGER", nullable: false),
                    old_purchase_cost_centavos = table.Column<long>(type: "INTEGER", nullable: true),
                    new_purchase_cost_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    old_selling_price_centavos = table.Column<long>(type: "INTEGER", nullable: true),
                    new_selling_price_centavos = table.Column<long>(type: "INTEGER", nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    changed_by_user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    changed_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medicine_price_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_medicine_price_history_medicines_medicine_id",
                        column: x => x.medicine_id,
                        principalTable: "medicines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medicine_price_history_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "created_at", "description", "is_active", "name", "updated_at" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Analgesics & Antipyretics", null },
                    { 2, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Antibiotics", null },
                    { 3, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Antihistamines & Allergy", null },
                    { 4, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Cough & Cold", null },
                    { 5, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Gastrointestinal", null },
                    { 6, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Vitamins & Supplements", null },
                    { 7, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Cardiovascular", null },
                    { 8, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Diabetes", null },
                    { 9, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Topical & Skin Care", null },
                    { 10, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "First Aid & Supplies", null }
                });

            migrationBuilder.InsertData(
                table: "dosage_forms",
                columns: new[] { "id", "created_at", "is_active", "name", "updated_at" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Tablet", null },
                    { 2, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Capsule", null },
                    { 3, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Syrup", null },
                    { 4, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Suspension", null },
                    { 5, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Drops", null },
                    { 6, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Injection", null },
                    { 7, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Cream", null },
                    { 8, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Ointment", null },
                    { 9, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Gel", null },
                    { 10, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Lotion", null },
                    { 11, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Solution", null },
                    { 12, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Sachet", null },
                    { 13, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Suppository", null },
                    { 14, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Inhaler", null },
                    { 15, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Patch", null }
                });

            migrationBuilder.InsertData(
                table: "units_of_measure",
                columns: new[] { "id", "created_at", "is_active", "name", "updated_at" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Piece", null },
                    { 2, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Box", null },
                    { 3, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Bottle", null },
                    { 4, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Strip", null },
                    { 5, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Sachet", null },
                    { 6, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Tube", null },
                    { 7, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Vial", null },
                    { 8, new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Utc), true, "Pack", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_name",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dosage_forms_name",
                table: "dosage_forms",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medicine_price_history_changed_by_user_id",
                table: "medicine_price_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_medicine_price_history_medicine_id_changed_at",
                table: "medicine_price_history",
                columns: new[] { "medicine_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_medicines_barcode",
                table: "medicines",
                column: "barcode",
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_medicines_brand_name",
                table: "medicines",
                column: "brand_name");

            migrationBuilder.CreateIndex(
                name: "IX_medicines_category_id",
                table: "medicines",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_medicines_dosage_form_id",
                table: "medicines",
                column: "dosage_form_id");

            migrationBuilder.CreateIndex(
                name: "IX_medicines_generic_name",
                table: "medicines",
                column: "generic_name");

            migrationBuilder.CreateIndex(
                name: "IX_medicines_unit_of_measure_id",
                table: "medicines",
                column: "unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "IX_units_of_measure_name",
                table: "units_of_measure",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medicine_price_history");

            migrationBuilder.DropTable(
                name: "medicines");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "dosage_forms");

            migrationBuilder.DropTable(
                name: "units_of_measure");
        }
    }
}
