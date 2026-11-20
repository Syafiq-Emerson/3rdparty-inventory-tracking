using System;
using IndustrialInventory.Api.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndustrialInventory.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovement_EventType",
                schema: "industrialinventory",
                table: "InventoryMovement");

            migrationBuilder.AddColumn<string>(
                name: "StorageCycleId",
                schema: "industrialinventory",
                table: "InventoryMovement",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StorageCycle",
                schema: "industrialinventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    InventoryItemId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    LocationId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    StoredAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    StoredByPersonnelId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    IssuedByPersonnelId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    LocationCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LocationNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StoredByPersonnelCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StoredByPersonnelNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IssuedByPersonnelCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IssuedByPersonnelNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageCycle", x => x.Id);
                    table.UniqueConstraint("UQ_StorageCycle_Id_InventoryItemId", x => new { x.Id, x.InventoryItemId });
                    table.CheckConstraint("CK_StorageCycle_Chronology", "[IssuedAtUtc] IS NULL OR [IssuedAtUtc] >= [StoredAtUtc]");
                    table.CheckConstraint("CK_StorageCycle_IssuePair", "([IssuedAtUtc] IS NULL AND [IssuedByPersonnelId] IS NULL AND [IssuedByPersonnelCodeSnapshot] IS NULL AND [IssuedByPersonnelNameSnapshot] IS NULL) OR ([IssuedAtUtc] IS NOT NULL AND [IssuedByPersonnelId] IS NOT NULL AND [IssuedByPersonnelCodeSnapshot] IS NOT NULL AND [IssuedByPersonnelNameSnapshot] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_StorageCycle_InventoryItem",
                        column: x => x.InventoryItemId,
                        principalSchema: "industrialinventory",
                        principalTable: "InventoryItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageCycle_IssuedByPersonnel",
                        column: x => x.IssuedByPersonnelId,
                        principalSchema: "industrialinventory",
                        principalTable: "Personnel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageCycle_StoreLocation",
                        column: x => x.LocationId,
                        principalSchema: "industrialinventory",
                        principalTable: "StoreLocation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageCycle_StoredByPersonnel",
                        column: x => x.StoredByPersonnelId,
                        principalSchema: "industrialinventory",
                        principalTable: "Personnel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_StorageCycle_Item",
                schema: "industrialinventory",
                table: "InventoryMovement",
                columns: new[] { "StorageCycleId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "UX_InventoryMovement_InitialRegistrationCycle",
                schema: "industrialinventory",
                table: "InventoryMovement",
                column: "StorageCycleId",
                unique: true,
                filter: "[StorageCycleId] IS NOT NULL AND [EventType] = 'INITIAL_REGISTRATION'");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryMovement_IssueCycle",
                schema: "industrialinventory",
                table: "InventoryMovement",
                column: "StorageCycleId",
                unique: true,
                filter: "[StorageCycleId] IS NOT NULL AND [EventType] = 'ISSUE_FOR_PACKING'");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryMovement_ReturnCycle",
                schema: "industrialinventory",
                table: "InventoryMovement",
                column: "StorageCycleId",
                unique: true,
                filter: "[StorageCycleId] IS NOT NULL AND [EventType] = 'RETURNED_TO_STORAGE'");

            migrationBuilder.CreateIndex(
                name: "IX_StorageCycle_IssuedByPersonnelId",
                schema: "industrialinventory",
                table: "StorageCycle",
                column: "IssuedByPersonnelId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageCycle_Item_StoredAtUtc",
                schema: "industrialinventory",
                table: "StorageCycle",
                columns: new[] { "InventoryItemId", "StoredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StorageCycle_LocationId",
                schema: "industrialinventory",
                table: "StorageCycle",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageCycle_StoredByPersonnelId",
                schema: "industrialinventory",
                table: "StorageCycle",
                column: "StoredByPersonnelId");

            migrationBuilder.CreateIndex(
                name: "UX_StorageCycle_OneActivePerItem",
                schema: "industrialinventory",
                table: "StorageCycle",
                column: "InventoryItemId",
                unique: true,
                filter: "[IssuedAtUtc] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovement_StorageCycle_Item",
                schema: "industrialinventory",
                table: "InventoryMovement",
                columns: new[] { "StorageCycleId", "InventoryItemId" },
                principalSchema: "industrialinventory",
                principalTable: "StorageCycle",
                principalColumns: new[] { "Id", "InventoryItemId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(SqlObjects.BackfillLegacyStorageCycles);
            migrationBuilder.Sql(SqlObjects.AsBatch(SqlObjects.CreateInventoryMovementImmutableTrigger));
            migrationBuilder.Sql(SqlObjects.AsBatch(SqlObjects.CreateStorageCycleImmutableTrigger));
            migrationBuilder.Sql(SqlObjects.AsBatch(SqlObjects.CreateInventoryMovementLifecycleTrigger));

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovement_EventType",
                schema: "industrialinventory",
                table: "InventoryMovement",
                sql: "[EventType] IN ('INITIAL_REGISTRATION', 'ISSUE_FOR_PACKING', 'RETURNED_TO_STORAGE') AND [StorageCycleId] IS NOT NULL AND (([EventType] IN ('INITIAL_REGISTRATION', 'RETURNED_TO_STORAGE') AND [FromLocationId] IS NULL AND [ToLocationId] IS NOT NULL) OR ([EventType] = 'ISSUE_FOR_PACKING' AND [FromLocationId] IS NOT NULL AND [ToLocationId] IS NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlObjects.ValidateStorageCycleRollback);
            migrationBuilder.Sql(SqlObjects.DropInventoryMovementLifecycleTrigger);
            migrationBuilder.Sql(SqlObjects.DropStorageCycleImmutableTrigger);
            migrationBuilder.Sql(SqlObjects.DropInventoryMovementImmutableTrigger);

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovement_StorageCycle_Item",
                schema: "industrialinventory",
                table: "InventoryMovement");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovement_EventType",
                schema: "industrialinventory",
                table: "InventoryMovement");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovement_StorageCycle_Item",
                schema: "industrialinventory",
                table: "InventoryMovement");
            migrationBuilder.DropIndex(
                name: "UX_InventoryMovement_InitialRegistrationCycle",
                schema: "industrialinventory",
                table: "InventoryMovement");
            migrationBuilder.DropIndex(
                name: "UX_InventoryMovement_IssueCycle",
                schema: "industrialinventory",
                table: "InventoryMovement");
            migrationBuilder.DropIndex(
                name: "UX_InventoryMovement_ReturnCycle",
                schema: "industrialinventory",
                table: "InventoryMovement");

            migrationBuilder.Sql(
                $"UPDATE [{SqlObjects.Schema}].[InventoryMovement] SET [StorageCycleId] = NULL");
            migrationBuilder.DropTable(
                name: "StorageCycle",
                schema: "industrialinventory");
            migrationBuilder.DropColumn(
                name: "StorageCycleId",
                schema: "industrialinventory",
                table: "InventoryMovement");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovement_EventType",
                schema: "industrialinventory",
                table: "InventoryMovement",
                sql: "[EventType] IN ('INITIAL_REGISTRATION', 'ISSUE_FOR_PACKING')");

            migrationBuilder.Sql(SqlObjects.AsBatch(SqlObjects.CreateInventoryMovementImmutableTrigger));
        }
    }
}
