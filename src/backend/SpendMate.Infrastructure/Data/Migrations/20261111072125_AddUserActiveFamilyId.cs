using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendMate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActiveFamilyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveFamilyId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Users" AS u
                SET "ActiveFamilyId" = f."Id"
                FROM "Families" AS f
                WHERE f."Type" = 'Personal'
                AND f."PersonalOwnerUserId" = u."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ActiveFamilyId",
                table: "Users",
                column: "ActiveFamilyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Families_ActiveFamilyId",
                table: "Users",
                column: "ActiveFamilyId",
                principalTable: "Families",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Families_ActiveFamilyId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ActiveFamilyId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ActiveFamilyId",
                table: "Users");
        }
    }
}
