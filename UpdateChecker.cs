using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DynamicIslandWin
{
    /// <summary>更新清单条目（对应远端 version.json 结构：{ version, note, url }）。</summary>
    public sealed class UpdateInfo
    {
        public string Version { get; set; } = "";
        public string Note { get; set; } = "";
        public string Url { get; set; } = "";
    }

    /// <summary>检查更新的纯逻辑客户端：拉取清单、比较版本。网络失败一律静默（由调用方记录）。</summary>
    internal static class UpdateChecker
    {
        public static async Task<UpdateInfo?> FetchAsync(string manifestUrl)
        {
            if (string.IsNullOrWhiteSpace(manifestUrl)) return null;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIslandWin");
            string json = await http.GetStringAsync(manifestUrl).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new UpdateInfo
            {
                Version = TryStr(root, "version"),
                Note = TryStr(root, "note"),
                Url = TryStr(root, "url")
            };
        }

        /// <summary>远端版本是否比当前新（支持 a.b.c / a.b / 带后缀的容错比较）。</summary>
        public static bool IsNewer(string currentVersion, string? remoteVersion)
        {
            if (string.IsNullOrWhiteSpace(remoteVersion)) return false;
            var a = ParseNums(currentVersion);
            var b = ParseNums(remoteVersion);
            if (a == null || b == null)
            {
                return string.CompareOrdinal(remoteVersion.Trim(), currentVersion.Trim()) > 0;
            }
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0;
                int y = i < b.Length ? b[i] : 0;
                if (x != y) return y > x;
            }
            return false;
        }

        private static int[]? ParseNums(string version)
        {
            var text = version.Trim();
            if (text.Length == 0) return null;
            var parts = text.Split('.');
            var nums = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var s = parts[i];
                var dash = s.IndexOf('-');
                if (dash >= 0) s = s.Substring(0, dash); // 忽略预发布后缀用于数值比较
                if (!int.TryParse(s, out nums[i])) return null;
            }
            return nums;
        }

        private static string TryStr(JsonElement root, string name) =>
            root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : "";
    }
}
