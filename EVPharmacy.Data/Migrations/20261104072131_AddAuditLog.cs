using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVPharmacy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    occurred_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    username = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    action = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    entity_type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    entity_id = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_occurred_at",
                table: "audit_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");
        }
    }
}
