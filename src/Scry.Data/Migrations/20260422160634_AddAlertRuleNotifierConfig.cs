using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scry.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertRuleNotifierConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotifierConfig",
                table: "AlertRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProbeIdFilter",
                table: "AlertRules",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifierConfig",
                table: "AlertRules");

            migrationBuilder.DropColumn(
                name: "ProbeIdFilter",
                table: "AlertRules");
        }
    }
}
