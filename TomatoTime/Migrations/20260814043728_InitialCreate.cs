using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TomatoTime.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ShortBreakMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LongBreakMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LongBreakInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    OverlayOpacity = table.Column<double>(type: "REAL", nullable: false),
                    RestoreOnStartup = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartWithWindows = table.Column<bool>(type: "INTEGER", nullable: false),
                    BellVolume = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskId = table.Column<int>(type: "INTEGER", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSessions_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Id", "BellVolume", "LongBreakInterval", "LongBreakMinutes", "OverlayOpacity", "RestoreOnStartup", "ShortBreakMinutes", "StartWithWindows", "WorkMinutes" },
                values: new object[] { 1, 70, 4, 15, 0.69999999999999996, true, 5, false, 25 });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_TaskId",
                table: "WorkSessions",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "WorkSessions");

            migrationBuilder.DropTable(
                name: "Tasks");
        }
    }
}
