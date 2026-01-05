using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherAcknowledgementToDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedDate",
                table: "AttendanceDisputes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAcknowledgedByTeacher",
                table: "AttendanceDisputes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcknowledgedDate",
                table: "AttendanceDisputes");

            migrationBuilder.DropColumn(
                name: "IsAcknowledgedByTeacher",
                table: "AttendanceDisputes");
        }
    }
}
