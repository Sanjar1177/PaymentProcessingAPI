using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentProcessingAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountBalanceReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReservedBalance",
                table: "Accounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_ReservedBalance_Valid",
                table: "Accounts",
                sql: "\"ReservedBalance\" >= 0 AND \"ReservedBalance\" <= \"Balance\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_ReservedBalance_Valid",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "ReservedBalance",
                table: "Accounts");
        }
    }
}
