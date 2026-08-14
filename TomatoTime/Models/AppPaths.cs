using System.IO;

namespace TomatoTime.Models;

/// <summary>应用数据目录与文件路径。</summary>
public static class AppPaths
{
    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TomatoTime");

    public static string DbPath => Path.Combine(AppDataDir, "tomato.db");

    public static string StatePath => Path.Combine(AppDataDir, "state.json");

    public static void EnsureDir() => Directory.CreateDirectory(AppDataDir);
}
