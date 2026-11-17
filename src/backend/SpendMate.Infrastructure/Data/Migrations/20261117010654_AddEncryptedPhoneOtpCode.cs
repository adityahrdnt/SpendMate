using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendMate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptedPhoneOtpCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodeCiphertext",
                table: "PhoneVerificationChallenges",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionNonce",
                table: "PhoneVerificationChallenges",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionTag",
                table: "PhoneVerificationChallenges",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodeCiphertext",
                table: "PhoneVerificationChallenges");

            migrationBuilder.DropColumn(
                name: "EncryptionNonce",
                table: "PhoneVerificationChallenges");

            migrationBuilder.DropColumn(
                name: "EncryptionTag",
                table: "PhoneVerificationChallenges");
        }
    }
}
