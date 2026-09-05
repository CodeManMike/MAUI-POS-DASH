using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAUIPOSDASH.Core.Persistence.Migrations.Terminal
{
    /// <inheritdoc />
    public partial class AddAttendantSessionAndIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Attendants",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "AttendantSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttendantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SignedInAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SignedOutAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendantSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendantSessions_Attendants_AttendantId",
                        column: x => x.AttendantId,
                        principalTable: "Attendants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendantSessions_AttendantId",
                table: "AttendantSessions",
                column: "AttendantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendantSessions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Attendants");
        }
    }
}
