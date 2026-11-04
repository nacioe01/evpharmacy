using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVPharmacy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "customer_id_number",
                table: "sales",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_name",
                table: "sales",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "discount_bps",
                table: "sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "discount_centavos",
                table: "sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "discount_type",
                table: "sales",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<bool>(
                name: "is_vat_registered",
                table: "sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "non_vat_sales_centavos",
                table: "sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "vat_amount_centavos",
                table: "sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "vat_exempt_sales_centavos",
                table: "sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "vat_rate_bps",
                table: "sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "vat_removed_centavos",
                table: "sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "vatable_sales_centavos",
                table: "sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "discount_centavos",
                table: "sale_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "is_vat_exempt",
                table: "sale_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "net_centavos",
                table: "sale_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "vat_amount_centavos",
                table: "sale_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "vat_removed_centavos",
                table: "sale_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "store_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false),
                    store_name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    address = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    tin = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    permit_info = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    contact_number = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    receipt_footer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    is_vat_registered = table.Column<bool>(type: "INTEGER", nullable: false),
                    vat_rate_bps = table.Column<int>(type: "INTEGER", nullable: false),
                    senior_discount_bps = table.Column<int>(type: "INTEGER", nullable: false),
                    pwd_discount_bps = table.Column<int>(type: "INTEGER", nullable: false),
                    is_confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_settings", x => x.id);
                    table.CheckConstraint("CK_store_settings_rates", "vat_rate_bps BETWEEN 0 AND 10000 AND senior_discount_bps BETWEEN 0 AND 10000 AND pwd_discount_bps BETWEEN 0 AND 10000");
                    table.CheckConstraint("CK_store_settings_singleton", "id = 1");
                });

            migrationBuilder.InsertData(
                table: "store_settings",
                columns: new[] { "id", "address", "contact_number", "is_confirmed", "is_vat_registered", "permit_info", "pwd_discount_bps", "receipt_footer", "senior_discount_bps", "store_name", "tin", "updated_at", "vat_rate_bps" },
                values: new object[] { 1, null, null, false, true, null, 2000, null, 2000, "EV Pharmacy", null, null, 1200 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_sales_discount_type",
                table: "sales",
                sql: "discount_type IN ('None', 'SeniorCitizen', 'Pwd')");

            // Sales recorded before tax columns existed carry no VAT breakdown. Keep their
            // money consistent: the whole amount counts as non-VAT sales and each row's net equals its total.
            migrationBuilder.Sql("UPDATE sales SET non_vat_sales_centavos = total_centavos WHERE non_vat_sales_centavos = 0;");
            migrationBuilder.Sql("UPDATE sale_items SET net_centavos = line_total_centavos WHERE net_centavos = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "store_settings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sales_discount_type",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "customer_id_number",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "customer_name",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "discount_bps",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "discount_centavos",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "discount_type",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "is_vat_registered",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "non_vat_sales_centavos",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "vat_amount_centavos",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "vat_exempt_sales_centavos",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "vat_rate_bps",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "vat_removed_centavos",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "vatable_sales_centavos",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "discount_centavos",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "is_vat_exempt",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "net_centavos",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "vat_amount_centavos",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "vat_removed_centavos",
                table: "sale_items");
        }
    }
}
