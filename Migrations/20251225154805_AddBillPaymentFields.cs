using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddBillPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Bills",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentToken",
                table: "Bills",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "Bills",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnpaidBalance",
                table: "Bills",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PaymentToken",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "UnpaidBalance",
                table: "Bills");
        }
    }
}
