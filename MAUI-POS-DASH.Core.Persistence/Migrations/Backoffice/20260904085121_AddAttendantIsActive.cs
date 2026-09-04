using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAUIPOSDASH.Core.Persistence.Migrations.Backoffice
{
    /// <inheritdoc />
    public partial class AddAttendantIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Attendants",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Attendants");
        }
    }
}
