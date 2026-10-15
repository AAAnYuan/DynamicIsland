using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            if (!IsWebUrl(manifestUrl)) return null;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIslandWin");
            string json = await http.GetStringAsync(manifestUrl).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!IsWebUrl(TryStr(root, "url"))) return null;
            return new UpdateInfo
            {
                Version = TryStr(root, "version"),
                Note = TryStr(root, "note"),
                Url = TryStr(root, "url")
            };
        }

        internal static bool IsWebUrl(string? value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

        /// <summary>按数字版本及预发布标识比较；无效清单不能触发更新提示。</summary>
        public static bool IsNewer(string currentVersion, string? remoteVersion)
        {
            if (string.IsNullOrWhiteSpace(remoteVersion)) return false;
            var a = ParseNums(currentVersion, out var currentPre);
            var b = ParseNums(remoteVersion, out var remotePre);
            if (a == null || b == null) return false;
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0;
                int y = i < b.Length ? b[i] : 0;
                if (x != y) return y > x;
            }
            if (currentPre == remotePre) return false;
            if (currentPre.Length == 0) return false;
            if (remotePre.Length == 0) return true;
            var left = currentPre.Split('.');
            var right = remotePre.Split('.');
            for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
            {
                bool ln = ulong.TryParse(left[i], out ulong lv);
                bool rn = ulong.TryParse(right[i], out ulong rv);
                int comparison = ln && rn ? rv.CompareTo(lv)
                    : ln != rn ? (rn ? -1 : 1) : string.CompareOrdinal(right[i], left[i]);
                if (comparison != 0) return comparison > 0;
            }
            return right.Length > left.Length;
        }

        private static int[]? ParseNums(string version, out string prerelease)
        {
            prerelease = "";
            var match = Regex.Match(version.Trim(), @"^v?(\d+(?:\.\d+){1,3})(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant);
            if (!match.Success) return null;
            prerelease = match.Groups[2].Value;
            var parts = match.Groups[1].Value.Split('.');
            var nums = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out nums[i])) return null;
            }
            return nums;
        }

        private static string TryStr(JsonElement root, string name) =>
            root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : "";
    }
}
