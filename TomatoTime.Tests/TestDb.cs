using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TomatoTime.Data;
using TomatoTime.Data.Entities;

namespace TomatoTime.Tests;

/// <summary>
/// SQLite in-memory DbContext 工厂 helper。
/// 使用真实 SQLite(而非 EF InMemory provider),以支持事务与 DeleteBehavior.SetNull 等关系行为验证。
/// </summary>
public static class TestDb
{
    public static TomatoTimeDbContext Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TomatoTimeDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new TomatoTimeDbContext(options);
        db.Database.EnsureCreated();
        if (!db.Settings.Any()) db.Settings.Add(new SettingsEntity { Id = 1 });
        db.SaveChanges();
        return db;
    }
}
