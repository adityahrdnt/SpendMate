using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendMate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneNumberToPhoneVerificationChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "PhoneVerificationChallenges",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "PhoneVerificationChallenges" AS challenge
                SET "PhoneNumber" = "Users"."PhoneNumber"
                FROM "Users"
                WHERE challenge."UserId" = "Users"."Id"
                AND "Users"."PhoneNumber" IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "PhoneVerificationChallenges"
                WHERE "PhoneNumber" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "PhoneVerificationChallenges",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "PhoneVerificationChallenges");
        }
    }
}
