using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendMate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyTypeAndPersonalOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PersonalOwnerUserId",
                table: "Families",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Families",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Personal");

            // Backfill: assign the existing Owner as PersonalOwnerUserId.
            migrationBuilder.Sql(
                """
                UPDATE "Families" AS f
                SET "PersonalOwnerUserId" = fm."UserId"
                FROM "FamilyMemberships" AS fm
                WHERE fm."FamilyId" = f."Id"
                AND fm."Role" = 'Owner';
                """);

            // Remove the temporary database default.
            // New Family instances must set their Type explicitly.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Families"
                ALTER COLUMN "Type" DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Families_PersonalOwnerUserId",
                table: "Families",
                column: "PersonalOwnerUserId",
                unique: true,
                filter: "\"PersonalOwnerUserId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Families_Type_PersonalOwner",
                table: "Families",
                sql: """
                    ("Type" = 'Personal' AND "PersonalOwnerUserId" IS NOT NULL)
                    OR
                    ("Type" = 'Shared' AND "PersonalOwnerUserId" IS NULL)
                    """);

            migrationBuilder.AddForeignKey(
                name: "FK_Families_Users_PersonalOwnerUserId",
                table: "Families",
                column: "PersonalOwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Families_Users_PersonalOwnerUserId",
                table: "Families");

            migrationBuilder.DropIndex(
                name: "IX_Families_PersonalOwnerUserId",
                table: "Families");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Families_Type_PersonalOwner",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "PersonalOwnerUserId",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Families");
        }
    }
}
