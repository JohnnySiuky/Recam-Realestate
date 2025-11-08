using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recam.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Add_SelectedMedia2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SelectedMedia_Agents_AgentId",
                table: "SelectedMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_SelectedMedia_ListingCases_ListingCaseId",
                table: "SelectedMedia");

            migrationBuilder.AlterColumn<string>(
                name: "AgentId",
                table: "SelectedMedia",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddForeignKey(
                name: "FK_SelectedMedia_Agents_AgentId",
                table: "SelectedMedia",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SelectedMedia_ListingCases_ListingCaseId",
                table: "SelectedMedia",
                column: "ListingCaseId",
                principalTable: "ListingCases",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SelectedMedia_Agents_AgentId",
                table: "SelectedMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_SelectedMedia_ListingCases_ListingCaseId",
                table: "SelectedMedia");

            migrationBuilder.AlterColumn<string>(
                name: "AgentId",
                table: "SelectedMedia",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SelectedMedia_Agents_AgentId",
                table: "SelectedMedia",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SelectedMedia_ListingCases_ListingCaseId",
                table: "SelectedMedia",
                column: "ListingCaseId",
                principalTable: "ListingCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
