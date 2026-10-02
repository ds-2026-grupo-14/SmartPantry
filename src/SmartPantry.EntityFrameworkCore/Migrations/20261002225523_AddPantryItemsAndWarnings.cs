using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPantry.Migrations
{
    /// <inheritdoc />
    public partial class AddPantryItemsAndWarnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppPantryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPantryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppPantryItems_AppProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "AppProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppPantryItemWarnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PantryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarningType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPantryItemWarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppPantryItemWarnings_AppPantryItems_PantryItemId",
                        column: x => x.PantryItemId,
                        principalTable: "AppPantryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppPantryItems_ProductId",
                table: "AppPantryItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPantryItemWarnings_PantryItemId_WarningType",
                table: "AppPantryItemWarnings",
                columns: new[] { "PantryItemId", "WarningType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPantryItemWarnings");

            migrationBuilder.DropTable(
                name: "AppPantryItems");
        }
    }
}
