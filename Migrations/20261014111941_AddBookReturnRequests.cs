using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBookReturnRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FineAmount",
                table: "Loans",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OverdueDaysAtRequest",
                table: "Loans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnRequestStatus",
                table: "Loans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnRequestedAt",
                table: "Loans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookRequests_LibraryUserId_BookId",
                table: "BookRequests",
                columns: new[] { "LibraryUserId", "BookId" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.AddForeignKey(
                name: "FK_BookRequests_Loans_LoanId",
                table: "BookRequests",
                column: "LoanId",
                principalTable: "Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookRequests_Loans_LoanId",
                table: "BookRequests");

            migrationBuilder.DropIndex(
                name: "IX_BookRequests_LibraryUserId_BookId",
                table: "BookRequests");

            migrationBuilder.DropColumn(
                name: "FineAmount",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "OverdueDaysAtRequest",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ReturnRequestStatus",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ReturnRequestedAt",
                table: "Loans");
        }
    }
}
