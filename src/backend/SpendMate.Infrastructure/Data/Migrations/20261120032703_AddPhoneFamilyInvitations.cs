using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpendMate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneFamilyInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FamilyInvitations_FamilyId_NormalizedEmail",
                table: "FamilyInvitations");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "FamilyInvitations",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "FamilyInvitations",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AddColumn<string>(
                name: "InvitationType",
                table: "FamilyInvitations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Email");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "FamilyInvitations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyInvitations_FamilyId_NormalizedEmail",
                table: "FamilyInvitations",
                columns: new[] { "FamilyId", "NormalizedEmail" },
                unique: true,
                filter: "\"Status\" = 'Pending' AND \"InvitationType\" = 'Email'");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyInvitations_FamilyId_PhoneNumber",
                table: "FamilyInvitations",
                columns: new[] { "FamilyId", "PhoneNumber" },
                unique: true,
                filter: "\"Status\" = 'Pending' AND \"InvitationType\" = 'Phone'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FamilyInvitations_Recipient",
                table: "FamilyInvitations",
                sql: "(\r\n    \"InvitationType\" = 'Email'\r\n    AND \"Email\" IS NOT NULL\r\n    AND \"NormalizedEmail\" IS NOT NULL\r\n    AND \"PhoneNumber\" IS NULL\r\n)\r\nOR\r\n(\r\n    \"InvitationType\" = 'Phone'\r\n    AND \"Email\" IS NULL\r\n    AND \"NormalizedEmail\" IS NULL\r\n    AND \"PhoneNumber\" IS NOT NULL\r\n)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FamilyInvitations_FamilyId_NormalizedEmail",
                table: "FamilyInvitations");

            migrationBuilder.DropIndex(
                name: "IX_FamilyInvitations_FamilyId_PhoneNumber",
                table: "FamilyInvitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FamilyInvitations_Recipient",
                table: "FamilyInvitations");

            migrationBuilder.DropColumn(
                name: "InvitationType",
                table: "FamilyInvitations");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "FamilyInvitations");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "FamilyInvitations",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "FamilyInvitations",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyInvitations_FamilyId_NormalizedEmail",
                table: "FamilyInvitations",
                columns: new[] { "FamilyId", "NormalizedEmail" },
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }
    }
}
