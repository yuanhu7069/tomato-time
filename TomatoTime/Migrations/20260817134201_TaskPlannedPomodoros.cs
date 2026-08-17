using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TomatoTime.Migrations
{
    /// <inheritdoc />
    public partial class TaskPlannedPomodoros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlannedPomodoros",
                table: "Tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PomodoroLengthMinutes",
                table: "Tasks",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedPomodoros",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "PomodoroLengthMinutes",
                table: "Tasks");
        }
    }
}
