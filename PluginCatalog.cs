using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DynamicIslandWin
{
    /// <summary>一个胶囊插件的清单（plugins\&lt;id&gt;\plugin.json）。</summary>
    public class PluginManifest
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Description { get; set; } = "";
    }

    /// <summary>插件目录管理：默认插件补齐、扫描加载、kind 与引擎模块映射。
    /// 引擎当前为固定岛位（每个 kind 一条胶囊），插件作为该胶囊的“身份/文案/预设”配置源。</summary>
    public static class PluginCatalog
    {
        public static string PluginsDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

        // 内置默认插件（当磁盘文件缺失/不可读时的兜底元数据）
        private static readonly (string Id, string Kind, string Name, string Icon, string Desc)[] Defaults =
        {
            ("media", "media", "媒体播放器", "🎵", "全局媒体控制：播放/暂停/切歌与专辑封面。"),
            ("notification", "notification", "通知与时钟", "💬", "时钟与通知展示，支持定时提醒。"),
            ("hardware", "hardware", "硬件监控", "📊", "CPU / GPU / 内存占用实时监控。"),
            ("note", "note", "记事备忘", "📝", "自定义文字胶囊：展示文字内容。")
        };

        public static string KindOf(IslandModule module) => module switch
        {
            IslandModule.Notification => "notification",
            IslandModule.Hardware => "hardware",
            IslandModule.Note => "note",
            _ => "media"
        };

        public static bool TryParseKind(string? kind, out IslandModule module)
        {
            switch (kind)
            {
                case "notification": module = IslandModule.Notification; return true;
                case "hardware": module = IslandModule.Hardware; return true;
                case "note": module = IslandModule.Note; return true;
                case "media": module = IslandModule.Media; return true;
                default: module = IslandModule.Media; return false;
            }
        }

        /// <summary>补齐默认插件文件夹与模板文件（缺失才写）。输出目录只读时静默失败，不影响运行。</summary>
        public static void EnsureDefaults()
        {
            try
            {
                Directory.CreateDirectory(PluginsDirectory);
                foreach (var d in Defaults)
                {
                    string dir = Path.Combine(PluginsDirectory, d.Id);
                    string file = Path.Combine(dir, "plugin.json");
                    if (File.Exists(file)) continue;
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(file, JsonSerializer.Serialize(new PluginManifest
                    {
                        Id = d.Id, Kind = d.Kind, Name = d.Name, Icon = d.Icon, Description = d.Desc
                    }, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch { }
        }

        /// <summary>扫描插件目录，返回全部合法插件清单（跳过 _ / . 前缀目录与非法清单）。
        /// 无论磁盘状态如何，四个内置默认插件（media/notification/hardware/note）始终会被补齐进结果，
        /// 保证“可用预设”库永不因插件目录缺失而变空。</summary>
        public static List<PluginManifest> Scan()
        {
            var result = new List<PluginManifest>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (Directory.Exists(PluginsDirectory))
                {
                    foreach (string dir in Directory.GetDirectories(PluginsDirectory))
                    {
                        string folderName = Path.GetFileName(dir);
                        if (folderName.StartsWith("_", StringComparison.Ordinal) ||
                            folderName.StartsWith(".", StringComparison.Ordinal)) continue;

                        string file = Path.Combine(dir, "plugin.json");
                        if (!File.Exists(file)) continue;
                        try
                        {
                            var m = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(file));
                            if (m == null || string.IsNullOrWhiteSpace(m.Id) || string.IsNullOrWhiteSpace(m.Name) ||
                                !TryParseKind(m.Kind, out _)) continue;
                            m.Id = m.Id.Trim();
                            m.Name = m.Name.Trim();
                            if (seenIds.Add(m.Id)) result.Add(m);
                        }
                        catch { /* 单个坏清单不影响其它插件 */ }
                    }
                }
            }
            catch { }

            // 兜底：四个内置默认插件永远在场（磁盘文件缺失/损坏/目录不可读时也保证可添加）
            foreach (var d in Defaults)
            {
                if (seenIds.Contains(d.Id)) continue;
                seenIds.Add(d.Id);
                result.Add(new PluginManifest
                {
                    Id = d.Id,
                    Kind = d.Kind,
                    Name = d.Name,
                    Icon = d.Icon,
                    Description = d.Desc
                });
            }
            return result;
        }

        /// <summary>某 kind 的内置默认 id（media/notification/hardware/note）。</summary>
        public static string DefaultId(IslandModule module) => KindOf(module);

        /// <summary>磁盘插件不可用时的兜底文案。</summary>
        public static (string Name, string Icon) FallbackMeta(IslandModule module)
        {
            foreach (var d in Defaults)
                if (d.Kind == KindOf(module)) return (d.Name, d.Icon);
            return (module.ToString(), "");
        }
    }
}
