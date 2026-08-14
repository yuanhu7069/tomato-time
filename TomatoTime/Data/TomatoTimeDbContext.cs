using Microsoft.EntityFrameworkCore;
using TomatoTime.Data.Entities;

namespace TomatoTime.Data;

public class TomatoTimeDbContext : DbContext
{
    public TomatoTimeDbContext(DbContextOptions<TomatoTimeDbContext> options) : base(options) { }

    public TomatoTimeDbContext() { }

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<WorkSessionEntity> WorkSessions => Set<WorkSessionEntity>();
    public DbSet<SettingsEntity> Settings => Set<SettingsEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // 设计时(dotnet ef)与无 DI 场景的兜底;运行时由 DI 传入 options 优先。
        if (!optionsBuilder.IsConfigured)
        {
            Models.AppPaths.EnsureDir();
            optionsBuilder.UseSqlite($"Data Source={Models.AppPaths.DbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<TaskEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired();
            e.HasMany(x => x.WorkSessions).WithOne(x => x.Task!).HasForeignKey(x => x.TaskId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<WorkSessionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TaskId).IsRequired(false);
        });

        mb.Entity<SettingsEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasData(new SettingsEntity { Id = 1 });
        });
    }
}
