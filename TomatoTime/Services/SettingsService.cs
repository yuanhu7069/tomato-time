using Microsoft.EntityFrameworkCore;
using TomatoTime.Data;
using TomatoTime.Data.Entities;

namespace TomatoTime.Services;

/// <summary>读取/更新单行 Settings 表,缓存到内存供各处读取。</summary>
public class SettingsService : ISettingsService
{
    private readonly TomatoTimeDbContext _db;
    private SettingsEntity _cache;

    public SettingsService(TomatoTimeDbContext db)
    {
        _db = db;
        if (!_db.Settings.Any()) _db.Settings.Add(new SettingsEntity { Id = 1 });
        _db.SaveChanges();
        _cache = _db.Settings.Single(x => x.Id == 1);
    }

    public int WorkMinutes => _cache.WorkMinutes;
    public int ShortBreakMinutes => _cache.ShortBreakMinutes;
    public int LongBreakMinutes => _cache.LongBreakMinutes;
    public int LongBreakInterval => _cache.LongBreakInterval;
    public double OverlayOpacity => _cache.OverlayOpacity;
    public int BellVolume => _cache.BellVolume;
    public bool RestoreOnStartup => _cache.RestoreOnStartup;
    public bool StartWithWindows => _cache.StartWithWindows;

    public void Reload()
    {
        _cache = _db.Settings.Single(x => x.Id == 1);
        _db.Entry(_cache).Reload();
    }

    public void Update(SettingsEntity updated)
    {
        _cache = updated;
        _db.Settings.Update(updated);
        _db.SaveChanges();
    }
}
