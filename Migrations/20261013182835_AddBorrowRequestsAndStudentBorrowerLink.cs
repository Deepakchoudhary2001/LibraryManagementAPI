using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBorrowRequestsAndStudentBorrowerLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BorrowerId",
                table: "LibraryUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookId = table.Column<int>(type: "int", nullable: false),
                    LibraryUserId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewedByAdminId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LoanId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookRequests_Admins_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "Admins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookRequests_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookRequests_LibraryUsers_LibraryUserId",
                        column: x => x.LibraryUserId,
                        principalTable: "LibraryUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryUsers_BorrowerId",
                table: "LibraryUsers",
                column: "BorrowerId",
                unique: true,
                filter: "[BorrowerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookRequests_BookId",
                table: "BookRequests",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_BookRequests_LibraryUserId",
                table: "BookRequests",
                column: "LibraryUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookRequests_LoanId",
                table: "BookRequests",
                column: "LoanId",
                unique: true,
                filter: "[LoanId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookRequests_ReviewedByAdminId",
                table: "BookRequests",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_BookRequests_Status",
                table: "BookRequests",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryUsers_Borrowers_BorrowerId",
                table: "LibraryUsers",
                column: "BorrowerId",
                principalTable: "Borrowers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LibraryUsers_Borrowers_BorrowerId",
                table: "LibraryUsers");

            migrationBuilder.DropTable(
                name: "BookRequests");

            migrationBuilder.DropIndex(
                name: "IX_LibraryUsers_BorrowerId",
                table: "LibraryUsers");

            migrationBuilder.DropColumn(
                name: "BorrowerId",
                table: "LibraryUsers");
        }
    }
}
