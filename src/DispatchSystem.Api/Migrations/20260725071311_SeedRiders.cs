using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DispatchSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedRiders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Riders",
                columns: new[] { "Id", "IsAvailable", "Name" },
                values: new object[,]
                {
                    { 1, true, "張飛" },
                    { 2, false, "劉備" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
