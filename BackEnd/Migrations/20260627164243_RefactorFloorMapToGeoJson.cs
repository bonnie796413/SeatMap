using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackEnd.Migrations
{
    /// <inheritdoc />
    public partial class RefactorFloorMapToGeoJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoundsJson",
                table: "FloorMaps");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "FloorMaps");

            migrationBuilder.DropColumn(
                name: "MaxZoom",
                table: "FloorMaps");

            migrationBuilder.DropColumn(
                name: "MinZoom",
                table: "FloorMaps");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "FloorMaps");

            migrationBuilder.RenameColumn(
                name: "TileDirectory",
                table: "FloorMaps",
                newName: "GeoJsonPath");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GeoJsonPath",
                table: "FloorMaps",
                newName: "TileDirectory");

            migrationBuilder.AddColumn<string>(
                name: "BoundsJson",
                table: "FloorMaps",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "FloorMaps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxZoom",
                table: "FloorMaps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinZoom",
                table: "FloorMaps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "FloorMaps",
                type: "integer",
                nullable: true);
        }
    }
}
