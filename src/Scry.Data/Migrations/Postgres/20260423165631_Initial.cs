using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scry.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Expression = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ForDuration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ProbeIdFilter = table.Column<Guid>(type: "uuid", nullable: true),
                    NotifierConfig = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                    table.UniqueConstraint("AK_AlertRules_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.ForeignKey(
                        name: "FK_AlertRules_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Attributes = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.UniqueConstraint("AK_Assets_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.ForeignKey(
                        name: "FK_Assets_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssetIds = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceWindows_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastNotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Labels = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertEvents_AlertRules_WorkspaceId_AlertRuleId",
                        columns: x => new { x.WorkspaceId, x.AlertRuleId },
                        principalTable: "AlertRules",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlertEvents_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetRelationships_Assets_WorkspaceId_SourceAssetId",
                        columns: x => new { x.WorkspaceId, x.SourceAssetId },
                        principalTable: "Assets",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetRelationships_Assets_WorkspaceId_TargetAssetId",
                        columns: x => new { x.WorkspaceId, x.TargetAssetId },
                        principalTable: "Assets",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetRelationships_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Probes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Definition = table.Column<string>(type: "text", nullable: false),
                    Interval = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Probes", x => x.Id);
                    table.UniqueConstraint("AK_Probes_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.ForeignKey(
                        name: "FK_Probes_Assets_WorkspaceId_AssetId",
                        columns: x => new { x.WorkspaceId, x.AssetId },
                        principalTable: "Assets",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Probes_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProbeResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProbeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attributes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProbeResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProbeResults_Probes_WorkspaceId_ProbeId",
                        columns: x => new { x.WorkspaceId, x.ProbeId },
                        principalTable: "Probes",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProbeResults_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_WorkspaceId",
                table: "AlertEvents",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_WorkspaceId_AlertRuleId_Fingerprint_State",
                table: "AlertEvents",
                columns: new[] { "WorkspaceId", "AlertRuleId", "Fingerprint", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_WorkspaceId_AlertRuleId_State",
                table: "AlertEvents",
                columns: new[] { "WorkspaceId", "AlertRuleId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_WorkspaceId",
                table: "AlertRules",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_WorkspaceId_Enabled",
                table: "AlertRules",
                columns: new[] { "WorkspaceId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetRelationships_WorkspaceId",
                table: "AssetRelationships",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetRelationships_WorkspaceId_SourceAssetId_Kind",
                table: "AssetRelationships",
                columns: new[] { "WorkspaceId", "SourceAssetId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetRelationships_WorkspaceId_SourceAssetId_TargetAssetId_~",
                table: "AssetRelationships",
                columns: new[] { "WorkspaceId", "SourceAssetId", "TargetAssetId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetRelationships_WorkspaceId_TargetAssetId_Kind",
                table: "AssetRelationships",
                columns: new[] { "WorkspaceId", "TargetAssetId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_WorkspaceId",
                table: "Assets",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_WorkspaceId_Kind",
                table: "Assets",
                columns: new[] { "WorkspaceId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_WorkspaceId_Provider_ExternalId",
                table: "Assets",
                columns: new[] { "WorkspaceId", "Provider", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWindows_WorkspaceId",
                table: "MaintenanceWindows",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceWindows_WorkspaceId_StartsAt_EndsAt",
                table: "MaintenanceWindows",
                columns: new[] { "WorkspaceId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProbeResults_WorkspaceId_CompletedAt",
                table: "ProbeResults",
                columns: new[] { "WorkspaceId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProbeResults_WorkspaceId_ProbeId_CompletedAt",
                table: "ProbeResults",
                columns: new[] { "WorkspaceId", "ProbeId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Probes_WorkspaceId",
                table: "Probes",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Probes_WorkspaceId_AssetId",
                table: "Probes",
                columns: new[] { "WorkspaceId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Probes_WorkspaceId_Enabled",
                table: "Probes",
                columns: new[] { "WorkspaceId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_Name",
                table: "Workspaces",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertEvents");

            migrationBuilder.DropTable(
                name: "AssetRelationships");

            migrationBuilder.DropTable(
                name: "MaintenanceWindows");

            migrationBuilder.DropTable(
                name: "ProbeResults");

            migrationBuilder.DropTable(
                name: "AlertRules");

            migrationBuilder.DropTable(
                name: "Probes");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "Workspaces");
        }
    }
}
