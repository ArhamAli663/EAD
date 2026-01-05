using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceDisputes",
                columns: table => new
                {
                    DisputeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceId = table.Column<int>(type: "int", nullable: false),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReportedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedBy = table.Column<int>(type: "int", nullable: true),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDisputes", x => x.DisputeId);
                    table.ForeignKey(
                        name: "FK_AttendanceDisputes_Attendances_AttendanceId",
                        column: x => x.AttendanceId,
                        principalTable: "Attendances",
                        principalColumn: "AttendanceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDisputes_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "TeacherId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDisputes_Users_ResolvedBy",
                        column: x => x.ResolvedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDisputes_AttendanceId",
                table: "AttendanceDisputes",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDisputes_ResolvedBy",
                table: "AttendanceDisputes",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDisputes_Status",
                table: "AttendanceDisputes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDisputes_TeacherId",
                table: "AttendanceDisputes",
                column: "TeacherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceDisputes");
        }
    }
}
