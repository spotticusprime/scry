using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scry.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Kind = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "longtext", nullable: false),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    ClaimedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    RunAfter = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_RunAfter",
                table: "Jobs",
                columns: new[] { "Status", "RunAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_WorkspaceId_Status_LeaseExpiresAt",
                table: "Jobs",
                columns: new[] { "WorkspaceId", "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_WorkspaceId_Status_RunAfter",
                table: "Jobs",
                columns: new[] { "WorkspaceId", "Status", "RunAfter" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jobs");
        }
    }
}
