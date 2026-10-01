using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_AggregateId",
                table: "Events");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Events",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill: number each aggregate's events 1..N in insertion order.
            // Must run BEFORE the unique index is created, otherwise every
            // existing event gets Version = 0 and the index creation fails
            // with a duplicate key error.
            migrationBuilder.Sql(@"
                WITH Numbered AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY AggregateId ORDER BY Id) AS Version
                    FROM Events
                )
                UPDATE Events
                SET Version = Numbered.Version
                FROM Events
                INNER JOIN Numbered ON Events.Id = Numbered.Id;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Events_AggregateId_Version",
                table: "Events",
                columns: new[] { "AggregateId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_AggregateId_Version",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Events");

            migrationBuilder.CreateIndex(
                name: "IX_Events_AggregateId",
                table: "Events",
                column: "AggregateId");
        }
    }
}