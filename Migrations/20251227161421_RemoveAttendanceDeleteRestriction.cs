using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAttendanceDeleteRestriction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceDisputes_Attendances_AttendanceId",
                table: "AttendanceDisputes");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceDisputes_Attendances_AttendanceId",
                table: "AttendanceDisputes",
                column: "AttendanceId",
                principalTable: "Attendances",
                principalColumn: "AttendanceId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceDisputes_Attendances_AttendanceId",
                table: "AttendanceDisputes");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceDisputes_Attendances_AttendanceId",
                table: "AttendanceDisputes",
                column: "AttendanceId",
                principalTable: "Attendances",
                principalColumn: "AttendanceId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
