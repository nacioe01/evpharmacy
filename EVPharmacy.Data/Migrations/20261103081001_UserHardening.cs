using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVPharmacy.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "users",
                type: "TEXT",
                maxLength: 36,
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 36);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "users",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20,
                oldDefaultValue: "active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "users",
                type: "TEXT",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 36,
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "users",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "active",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20);
        }
    }
}
