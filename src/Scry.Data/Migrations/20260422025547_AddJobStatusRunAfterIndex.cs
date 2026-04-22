using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scry.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobStatusRunAfterIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_RunAfter",
                table: "Jobs",
                columns: new[] { "Status", "RunAfter" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_Status_RunAfter",
                table: "Jobs");
        }
    }
}
