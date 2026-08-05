using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DispatchSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedMoreRiders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Riders",
                columns: new[] { "Id", "IsAvailable", "Name" },
                values: new object[,]
                {
                    { 3, false, "關羽" },
                    { 4, false, "趙雲" },
                    { 5, false, "馬超" },
                    { 6, false, "黃忠" },
                    { 7, false, "曹操" },
                    { 8, false, "孫權" },
                    { 9, false, "周瑜" },
                    { 10, false, "呂布" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: 10);
        }
    }
}
