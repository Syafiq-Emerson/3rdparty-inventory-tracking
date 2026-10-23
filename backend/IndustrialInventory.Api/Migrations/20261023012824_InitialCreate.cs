using System;
using IndustrialInventory.Api.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndustrialInventory.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "industrialinventory");

            migrationBuilder.CreateTable(
                name: "AppUser",
                schema: "industrialinventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUser", x => x.Id);
                    table.CheckConstraint("CK_AppUser_Role", "[Role] IN ('Admin', 'Operator')");
                });

            migrationBuilder.CreateTable(
                name: "AuditEvent",
                schema: "industrialinventory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OperatorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Personnel",
                schema: "industrialinventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    PersonnelCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personnel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreLocation",
                schema: "industrialinventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    LocationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LocationName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreLocation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItem",
                schema: "industrialinventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TagNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LocationId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    RegisteredByPersonnelId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    IsIssued = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItem_Personnel",
                        column: x => x.RegisteredByPersonnelId,
                        principalSchema: "industrialinventory",
                        principalTable: "Personnel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItem_StoreLocation",
                        column: x => x.LocationId,
                        principalSchema: "industrialinventory",
                        principalTable: "StoreLocation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovement",
                schema: "industrialinventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    InventoryItemId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    FromLocationId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    ToLocationId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    PerformedByPersonnelId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MovedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovement", x => x.Id);
                    table.CheckConstraint("CK_InventoryMovement_EventType", "[EventType] IN ('INITIAL_REGISTRATION', 'ISSUE_FOR_PACKING')");
                    table.ForeignKey(
                        name: "FK_InventoryMovement_FromLocation",
                        column: x => x.FromLocationId,
                        principalSchema: "industrialinventory",
                        principalTable: "StoreLocation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovement_InventoryItem",
                        column: x => x.InventoryItemId,
                        principalSchema: "industrialinventory",
                        principalTable: "InventoryItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovement_Personnel",
                        column: x => x.PerformedByPersonnelId,
                        principalSchema: "industrialinventory",
                        principalTable: "Personnel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovement_ToLocation",
                        column: x => x.ToLocationId,
                        principalSchema: "industrialinventory",
                        principalTable: "StoreLocation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "industrialinventory",
                table: "AppUser",
                columns: new[] { "Id", "CreatedAtUtc", "IsActive", "MustChangePassword", "PasswordHash", "Role", "UpdatedAtUtc", "UserName" },
                values: new object[] { "usr-admin", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, true, "", "Admin", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin" });

            migrationBuilder.CreateIndex(
                name: "UX_AppUser_UserName",
                schema: "industrialinventory",
                table: "AppUser",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_ActionType",
                schema: "industrialinventory",
                table: "AuditEvent",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_Entity",
                schema: "industrialinventory",
                table: "AuditEvent",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_OccurredAtUtc",
                schema: "industrialinventory",
                table: "AuditEvent",
                column: "OccurredAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_UserName",
                schema: "industrialinventory",
                table: "AuditEvent",
                column: "UserName");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItem_CreatedAtUtc",
                schema: "industrialinventory",
                table: "InventoryItem",
                column: "CreatedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItem_LocationId",
                schema: "industrialinventory",
                table: "InventoryItem",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItem_RegisteredByPersonnelId",
                schema: "industrialinventory",
                table: "InventoryItem",
                column: "RegisteredByPersonnelId");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryItem_SerialNumber",
                schema: "industrialinventory",
                table: "InventoryItem",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_InventoryItem_TagNumber",
                schema: "industrialinventory",
                table: "InventoryItem",
                column: "TagNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_FromLocationId",
                schema: "industrialinventory",
                table: "InventoryMovement",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_InventoryItemId",
                schema: "industrialinventory",
                table: "InventoryMovement",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_MovedAtUtc",
                schema: "industrialinventory",
                table: "InventoryMovement",
                column: "MovedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_PerformedByPersonnelId",
                schema: "industrialinventory",
                table: "InventoryMovement",
                column: "PerformedByPersonnelId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovement_ToLocationId",
                schema: "industrialinventory",
                table: "InventoryMovement",
                column: "ToLocationId");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryMovement_IdempotencyKey",
                schema: "industrialinventory",
                table: "InventoryMovement",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_IsActive",
                schema: "industrialinventory",
                table: "Personnel",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UX_Personnel_PersonnelCode",
                schema: "industrialinventory",
                table: "Personnel",
                column: "PersonnelCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreLocation_IsActive",
                schema: "industrialinventory",
                table: "StoreLocation",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UX_StoreLocation_LocationCode",
                schema: "industrialinventory",
                table: "StoreLocation",
                column: "LocationCode",
                unique: true);

            migrationBuilder.Sql(SqlObjects.AsBatch(SqlObjects.CreateInventoryMovementImmutableTrigger));
            migrationBuilder.Sql(SqlObjects.AsBatch(SqlObjects.CreateAuditEventImmutableTrigger));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlObjects.DropInventoryMovementImmutableTrigger);
            migrationBuilder.Sql(SqlObjects.DropAuditEventImmutableTrigger);

            migrationBuilder.DropTable(
                name: "AppUser",
                schema: "industrialinventory");

            migrationBuilder.DropTable(
                name: "AuditEvent",
                schema: "industrialinventory");

            migrationBuilder.DropTable(
                name: "InventoryMovement",
                schema: "industrialinventory");

            migrationBuilder.DropTable(
                name: "InventoryItem",
                schema: "industrialinventory");

            migrationBuilder.DropTable(
                name: "Personnel",
                schema: "industrialinventory");

            migrationBuilder.DropTable(
                name: "StoreLocation",
                schema: "industrialinventory");
        }
    }
}
