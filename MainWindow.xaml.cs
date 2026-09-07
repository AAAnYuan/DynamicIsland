using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;

namespace DynamicIslandWin
{
    public enum IslandModule { Media, Notification, Hardware, Note }
    public enum CapsuleId { None, Main, SubNotif, SubHw, SubMedia, SubNote }

    /// <summary>闲置时主胶囊的形态。Circle=现有灵动小圆点；Notch=像 iPhone 刘海一样吸附桌面顶部（药丸形贴边）。</summary>
    public enum IdleStyle { None = 0, Circle = 1, Notch = 2 }

    // 1. 媒体胶囊专属动作
    public enum MediaAction
    {
        ToggleExpand,       // 🪟 展开大卡片 / 收起
        TogglePlayPause,    // ▶/⏸ 播放 / 暂停
        NextTrack,          // ⏭ 切下一首
        PrevTrack,          // ⏮ 切上一首
        OpenCustomApp,      // 🚀 打开自定义程序/应用
        OpenSettings,       // ⚙️ 灵动岛设置
        None,               // 🚫 无动作 (仅拖动)
        OpenMusicApp        // 🎧 打开当前播放来源对应的音乐软件（值追加在末尾，保持旧配置编号稳定）
    }

    // 2. 通知与时钟胶囊专属动作
    public enum NotifAction
    {
        ToggleExpand,            // 🪟 展开日程与通知
        OpenWindowsTimeSettings, // ⏰ 打开 Windows 日期与时间设置
        ClearNotification,       // 🧹 清除当前通知 / 标记已读
        OpenCustomApp,           // 🚀 打开自定义程序/应用
        OpenSettings,            // ⚙️ 灵动岛设置
        None                     // 🚫 无动作
    }

    // 3. 硬件监控胶囊专属动作
    public enum HwAction
    {
        OpenTaskManager,     // ⚡ 打开 Windows 任务管理器 (taskmgr)
        ToggleExpand,        // 🪟 展开硬件仪表盘
        OpenResourceMonitor, // 📈 打开资源监视器 (resmon)
        OpenWindowsSettings, // 💻 打开 Windows 系统设置
        OpenCustomApp,       // 🚀 打开自定义程序/应用
        OpenSettings,        // ⚙️ 灵动岛设置
        None                 // 🚫 无动作
    }

    // 4. 记事备忘胶囊专属动作（右键固定为打开设置）
    public enum NoteAction
    {
        ToggleExpand,   // 📝 展开全文 / 收起
        OpenSettings,   // ⚙️ 灵动岛设置
        None,           // 🚫 无动作
        OpenCustomApp   // 🚀 打开自定义程序/应用
    }

    /// <summary>一条定时提醒：每天在 Hour:Minute 触发一次（当分钟窗口内），触发后记入 LastFiredDate 防止同日重复。</summary>
    public class ReminderConfig
    {
        public bool Enabled { get; set; } = true;
        public int Hour { get; set; } = 9;
        public int Minute { get; set; } = 0;
        public string Message { get; set; } = "";
        public string LastFiredDate { get; set; } = "";
    }

    public class AppConfig
    {
        public double WindowLeft { get; set; } = -1;        public double WindowTop { get; set; } = -1;
        public double BaseWidth { get; set; } = 280;
        public double CapsuleScale { get; set; } = 1.0;
        public bool IsAutoWidthEnabled { get; set; } = true;
        public bool IsIdleCircleEnabled { get; set; } = true;
        /// <summary>闲置样式（None/Circle/Notch），新版本权威设置；IsIdleCircleEnabled 仅作旧版迁移推断。</summary>
        public IdleStyle IdleStyle { get; set; } = IdleStyle.Circle;
        public string CurrentThemeKey { get; set; } = "AeroDark";
        public string WaveColorHex { get; set; } = "#00E5FF";
        /// <summary>字体颜色覆盖（空 = 跟随主题自动）。</summary>
        public string FontColorHex { get; set; } = "";

        public int WaveBarCount { get; set; } = 5;
        public double WaveMaxHeight { get; set; } = 24.0;

        /// <summary>v1 时代的全局自定义程序路径：仅用于首次升级迁移（并入媒体胶囊），不再写入。</summary>
        public string CustomAppPath { get; set; } = "";
        public string MediaCustomAppPath { get; set; } = "";
        public string NotifCustomAppPath { get; set; } = "";
        public string HwCustomAppPath { get; set; } = "";
        public string NoteCustomAppPath { get; set; } = "";

        public IslandModule ConfiguredMainModule { get; set; } = IslandModule.Media;
        public bool IsMediaEnabled { get; set; } = true;
        public bool IsNotifEnabled { get; set; } = true;
        public bool IsHwEnabled { get; set; } = true;
        public bool IsNoteEnabled { get; set; } = false;
        public string NoteText { get; set; } = "";
        /// <summary>备忘录标题（闲置/紧凑态显示；展开态与内容并列）。</summary>
        public string NoteTitle { get; set; } = "";
        public List<ReminderConfig> Reminders { get; set; } = new();

        public MediaAction MediaLeftAction { get; set; } = MediaAction.ToggleExpand;
        public MediaAction MediaRightAction { get; set; } = MediaAction.OpenSettings;

        public NotifAction NotifLeftAction { get; set; } = NotifAction.ToggleExpand;
        public NotifAction NotifRightAction { get; set; } = NotifAction.OpenSettings;

        public HwAction HwLeftAction { get; set; } = HwAction.OpenTaskManager;
        public HwAction HwRightAction { get; set; } = HwAction.OpenSettings;

        public NoteAction NoteLeftAction { get; set; } = NoteAction.ToggleExpand;
        /// <summary>备忘胶囊右键动作（副胶囊时生效；主胶囊右键仍固定为设置）。</summary>
        public NoteAction NoteRightAction { get; set; } = NoteAction.OpenSettings;
        /// <summary>每个 kind 当前“生效插件”id（kind → plugin id），未配置则使用内置默认插件。</summary>
        public Dictionary<string, string> ActivePluginByKind { get; set; } = new();
        /// <summary>喜欢快捷键（向播放器发送其“喜欢”绑定键）。</summary>
        public bool LikeShortcutEnabled { get; set; } = false;
        public string LikeShortcutKey { get; set; } = "F5";
        /// <summary>启动静默检查更新。</summary>
        public bool IsStartupCheckUpdatesEnabled { get; set; } = true;
        /// <summary>更新清单地址（远端 version.json）。</summary>
        public string UpdateManifestUrl { get; set; } = "";
        /// <summary>已提示过的新版本（避免每次启动都打扰）。</summary>
        public string LastNotifiedUpdateVersion { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        private GlobalSystemMediaTransportControlsSessionManager? _mediaManager;
        private GlobalSystemMediaTransportControlsSession? _activeSession;
        private GlobalSystemMediaTransportControlsSession? _subscribedSession;

        private readonly DispatcherTimer _systemStatusTimer = new();
        private readonly DispatcherTimer _notifResetTimer = new();
        private readonly DispatcherTimer _hoverIntentTimer = new();
        private readonly DispatcherTimer _hoverLeaveTimer = new();
        private readonly DispatcherTimer _switchConfirmTimer = new(); // 已展开胶囊↔邻胶囊切换的极短确认
        private readonly DispatcherTimer _saveDebounceTimer = new(); // 配置持久化防抖
        private readonly DispatcherTimer _wheelSwitchTimer = new();   // 滚轮切主模块的合并防抖（避免快速滚动反复改弹簧目标导致尺寸跳动）
        private int _wheelPendingSteps = 0;                           // 滚轮累计步数（方向），停顿后一次切到目标模块
        private readonly GpuLoadSampler _gpuSampler = new();          // 真实 GPU 负载采样（PDH）

        // 主胶囊居中锚定的透明伸缩占位（首尾各一）
        private readonly Border _padLeftElement = new Border { Width = 0, Height = 1, Opacity = 0, IsHitTestVisible = false };
        private readonly Border _padRightElement = new Border { Width = 0, Height = 1, Opacity = 0, IsHitTestVisible = false };

        // 副胶囊阴影的“基准值”（来自当前主题），每帧按宽度插值，避免小胶囊出现黑色“雾”
        private double _shadowBaseBlur = 20;
        private double _shadowBaseOpacity = 0.55;
        private double _shadowBaseDepth = 3;

        private bool _isCircleIdle = false;
        private bool _isNotificationActive = false;

        // “刘海吸附”闲置形态：扁“托盘/浅盘”形（顶宽平、底窄平、底角圆），顶部完全贴屏幕顶边
        private const double NotchIdleWidth = 150;
        private const double NotchIdleHeight = 26;
        private const double NotchIdleTopGap = 0;
        private const double IslandTopMargin = 16;
        private bool _isIdleWindowDocked = false;
        private double _idleRestoreLeft = 0;
        private double _idleRestoreTop = 0;
        private readonly DispatcherTimer _dockAnimTimer = new(); // 吸附/解除吸附：窗口纵向平滑缓动
        private double _dockAnimToTop = 0;

        private CapsuleId _pendingHoverCapsule = CapsuleId.None;
        private CapsuleId _pendingSwitchTarget = CapsuleId.None;
        private CapsuleId _activeExpandedCapsule = CapsuleId.None;
        private bool _isMainExpanded = false;

        private readonly Stopwatch _physicsStopwatch = new();
        private bool _isPhysicsRunning = false;
        private bool _physicsSettledFrame = false; // 上一帧是否已全部收敛（用于静止期冻结尺寸写入）

        // 模块配置
        public IslandModule ConfiguredMainModule { get; set; } = IslandModule.Media;
        public IslandModule ActiveMainModule { get; set; } = IslandModule.Media;
        public List<IslandModule> MainScrollPool { get; } = new();

        public bool IsMediaEnabled { get; set; } = true;
        public bool IsNotifEnabled { get; set; } = true;
        public bool IsHwEnabled { get; set; } = true;
        public bool IsNoteEnabled { get; set; } = false;
        public string NoteText { get; set; } = "";
        /// <summary>备忘录标题。</summary>
        public string NoteTitle { get; set; } = "";

        // 明确补全的动作字段 (解决 CS0103 虚空引用)
        public MediaAction MediaLeftAction { get; set; } = MediaAction.ToggleExpand;
        public MediaAction MediaRightAction { get; set; } = MediaAction.OpenSettings;

        public NotifAction NotifLeftAction { get; set; } = NotifAction.ToggleExpand;
        public NotifAction NotifRightAction { get; set; } = NotifAction.OpenSettings;

        public HwAction HwLeftAction { get; set; } = HwAction.OpenTaskManager;
        public HwAction HwRightAction { get; set; } = HwAction.OpenSettings;

        public NoteAction NoteLeftAction { get; set; } = NoteAction.ToggleExpand;
        /// <summary>备忘胶囊右键动作（副胶囊时生效）。</summary>
        public NoteAction NoteRightAction { get; set; } = NoteAction.OpenSettings;
        public List<ReminderConfig> Reminders { get; set; } = new();
        /// <summary>每个 kind 当前“生效插件”id（kind → plugin id），未配置则使用内置默认插件。</summary>
        public Dictionary<string, string> ActivePluginByKind { get; set; } = new();
        public List<PluginManifest> InstalledPlugins { get; private set; } = new();
        /// <summary>喜欢快捷键：启用开关 + 点按 ♥ 后发送给播放器的键组合（鼠标宏式）。</summary>
        public bool LikeShortcutEnabled { get; set; } = false;
        public string LikeShortcutKey { get; set; } = "F5";
        /// <summary>启动静默检查更新。</summary>
        public bool IsStartupCheckUpdatesEnabled { get; set; } = true;
        /// <summary>更新清单地址（远端 version.json）。</summary>
        public string UpdateManifestUrl { get; set; } = "";
        /// <summary>已提示过的新版本。</summary>
        public string LastNotifiedUpdateVersion { get; set; } = "";

        /// <summary>查询某模块是否“已添加”（作为独立胶囊启用）。</summary>
        public bool GetModuleEnabled(IslandModule mod) => mod switch
        {
            IslandModule.Notification => IsNotifEnabled,
            IslandModule.Hardware => IsHwEnabled,
            IslandModule.Note => IsNoteEnabled,
            _ => IsMediaEnabled
        };

        /// <summary>设置某模块“已添加/已移除”状态并持久化。</summary>
        public void SetModuleEnabled(IslandModule mod, bool enabled)
        {
            switch (mod)
            {
                case IslandModule.Notification: IsNotifEnabled = enabled; break;
                case IslandModule.Hardware: IsHwEnabled = enabled; break;
                case IslandModule.Note: IsNoteEnabled = enabled; break;
                default: IsMediaEnabled = enabled; break;
            }
            SaveConfiguration();
        }

        #region 胶囊插件目录
        /// <summary>补齐默认插件文件并重新扫描；同时校准每个 kind 的“生效插件”。</summary>
        public void RefreshPluginsFromDisk()
        {
            PluginCatalog.EnsureDefaults();
            InstalledPlugins = PluginCatalog.Scan();
            var kinds = new[] { IslandModule.Media, IslandModule.Notification, IslandModule.Hardware, IslandModule.Note };
            bool changed = false;
            foreach (var mod in kinds)
            {
                string kind = PluginCatalog.KindOf(mod);
                bool valid = ActivePluginByKind.TryGetValue(kind, out string? id) && id != null && FindPlugin(id) != null;
                if (!valid)
                {
                    ActivePluginByKind[kind] = PluginCatalog.DefaultId(mod);
                    changed = true;
                }
            }

            // 自愈：胶囊不可能全部缺失（至少保留一个已添加）
            if (!IsMediaEnabled && !IsNotifEnabled && !IsHwEnabled && !IsNoteEnabled)
            {
                IsMediaEnabled = true;
                if (ConfiguredMainModule != IslandModule.Media &&
                    ConfiguredMainModule != IslandModule.Notification &&
                    ConfiguredMainModule != IslandModule.Hardware &&
                    ConfiguredMainModule != IslandModule.Note)
                {
                    ConfiguredMainModule = IslandModule.Media;
                }
                changed = true;
            }
            if (changed) SaveConfiguration();
        }

        private PluginManifest? FindPlugin(string id)
        {
            if (InstalledPlugins == null) return null;
            return InstalledPlugins.Find(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>某胶囊当前生效插件（未配置/文件缺失时回落内置默认）。</summary>
        public PluginManifest ActivePlugin(IslandModule mod)
        {
            string kind = PluginCatalog.KindOf(mod);
            if (ActivePluginByKind != null && ActivePluginByKind.TryGetValue(kind, out string? id) && id != null)
            {
                PluginManifest? p = FindPlugin(id);
                if (p != null) return p;
            }
            PluginManifest? d = FindPlugin(PluginCatalog.DefaultId(mod));
            if (d != null) return d;
            var (name, icon) = PluginCatalog.FallbackMeta(mod);
            return new PluginManifest { Id = PluginCatalog.DefaultId(mod), Kind = kind, Name = name, Icon = icon };
        }

        /// <summary>把某插件设为对应 kind 的生效插件，并确保该胶囊已添加。</summary>
        public void ActivatePlugin(IslandModule mod, string pluginId)
        {
            if (FindPlugin(pluginId) == null) return;
            ActivePluginByKind[PluginCatalog.KindOf(mod)] = pluginId;
            SetModuleEnabled(mod, true);
            SaveConfiguration();
        }

        /// <summary>胶囊移除后把该 kind 还原为内置默认插件。</summary>
        public void ResetActivePlugin(IslandModule mod)
        {
            ActivePluginByKind[PluginCatalog.KindOf(mod)] = PluginCatalog.DefaultId(mod);
        }
        #endregion

        /// <summary>写入备忘录文字并即时刷新主/副胶囊预览。</summary>
        public void SetNoteText(string text)
        {
            NoteText = text ?? "";
            UpdateNoteContentViews();
            SaveConfiguration();
        }

        /// <summary>写入备忘录标题并即时刷新主/副胶囊预览。</summary>
        public void SetNoteTitle(string title)
        {
            NoteTitle = title ?? "";
            UpdateNoteContentViews();
            SaveConfiguration();
        }

        /// <summary>展开态备注：单击标题/正文进入内联编辑。</summary>
        private void NoteExpanded_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            bool isSub = ReferenceEquals(sender, SubNote_Display);
            if (isSub)
            {
                EditNoteTitleSub.Text = NoteTitle;
                EditNoteContentSub.Text = NoteText;
                SubNote_Display.Visibility = Visibility.Collapsed;
                SubNote_Edit.Visibility = Visibility.Visible;
                EditNoteTitleSub.Focus();
                EditNoteTitleSub.SelectAll();
            }
            else
            {
                EditNoteTitleMain.Text = NoteTitle;
                EditNoteContentMain.Text = NoteText;
                MainNote_Display.Visibility = Visibility.Collapsed;
                MainNote_Edit.Visibility = Visibility.Visible;
                EditNoteTitleMain.Focus();
                EditNoteTitleMain.SelectAll();
            }
        }

        /// <summary>标题框 Enter：跳到内容框。</summary>
        private void NoteEditTitle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            bool isSub = ReferenceEquals(sender, EditNoteTitleSub);
            if (isSub) { EditNoteContentSub.Focus(); EditNoteContentSub.SelectAll(); }
            else { EditNoteContentMain.Focus(); EditNoteContentMain.SelectAll(); }
        }

        /// <summary>内容框 Enter：保存并回到显示态。</summary>
        private void NoteEditContent_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            CommitNoteEdit(ReferenceEquals(sender, EditNoteContentSub));
        }

        /// <summary>提交备注（标题+内容）并回到显示态。</summary>
        private void CommitNoteEdit(bool isSub)
        {
            if (isSub)
            {
                SetNoteTitle(EditNoteTitleSub.Text);
                SetNoteText(EditNoteContentSub.Text);
            }
            else
            {
                SetNoteTitle(EditNoteTitleMain.Text);
                SetNoteText(EditNoteContentMain.Text);
            }
            ResetNoteEditState();
        }

        /// <summary>备注：统一回到“显示态”（隐藏标题/内容编辑框，显示只读内容）。</summary>
        private void ResetNoteEditState()
        {
            if (MainNote_Edit != null)
            {
                MainNote_Edit.Visibility = Visibility.Collapsed;
                if (MainNote_Display != null) MainNote_Display.Visibility = Visibility.Visible;
            }
            if (SubNote_Edit != null)
            {
                SubNote_Edit.Visibility = Visibility.Collapsed;
                if (SubNote_Display != null) SubNote_Display.Visibility = Visibility.Visible;
            }
        }

        /// <summary>把备忘录同步到主胶囊与备忘录胶囊的内容视图（标题/内容/图标；闲置紧凑态显示标题）。</summary>
        public void UpdateNoteContentViews()
        {
            if (TxtNoteCompactMain == null) return; // XAML 未加载完成时跳过
            string title = string.IsNullOrWhiteSpace(NoteTitle) ? "" : NoteTitle.Trim();
            string full = string.IsNullOrWhiteSpace(NoteText) ? "" : NoteText.Trim();
            // 闲置/紧凑显示标题；无标题则回退到内容首行
            string compact = !string.IsNullOrEmpty(title) ? title : FirstLineOf(full, 20);

            TxtNoteCompactMain.Text = string.IsNullOrEmpty(compact) ? "暂无标题" : compact;
            TxtNoteExpandedTitle.Text = string.IsNullOrEmpty(title) ? "（未命名）" : title;
            TxtNoteExpandedMain.Text = string.IsNullOrEmpty(full) ? "（暂无内容，单击标题或正文可编辑）" : full;

            TxtSubNoteCompact.Text = string.IsNullOrEmpty(compact) ? "暂无标题" : compact;
            TxtSubNoteExpandedTitle.Text = string.IsNullOrEmpty(title) ? "（未命名）" : title;
            TxtSubNoteExpanded.Text = string.IsNullOrEmpty(full) ? "（暂无内容，单击标题或正文可编辑）" : full;

            // 生效插件的图标/名称
            PluginManifest p = ActivePlugin(IslandModule.Note);
            string icon = string.IsNullOrWhiteSpace(p.Icon) ? "📝" : p.Icon;
            string header = $"{icon}  {p.Name}";
            TxtMainNoteIcon.Text = icon;
            TxtSubNoteIcon.Text = icon;
            TxtMainNoteHeader.Text = header;
            TxtSubNoteHeader.Text = header;

            // 布局：紧凑/展开宽度随文字自适应
            RecalculateMainCompactWidth();
        }

        private static string FirstLineOf(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return "暂无内容";
            string line = text;
            int nl = line.IndexOfAny(new[] { '\r', '\n' });
            if (nl >= 0) line = line.Substring(0, nl);
            return line.Length <= maxChars ? line : line.Substring(0, maxChars) + "…";
        }

        // 各胶囊专属的自定义程序路径（旧版单一 CustomAppPath 启动时迁移进媒体胶囊）
        public string MediaCustomAppPath { get; set; } = "";
        public string NotifCustomAppPath { get; set; } = "";
        public string HwCustomAppPath { get; set; } = "";
        public string NoteCustomAppPath { get; set; } = "";

        public string GetCustomAppPath(IslandModule mod) => mod switch
        {
            IslandModule.Notification => NotifCustomAppPath,
            IslandModule.Hardware => HwCustomAppPath,
            IslandModule.Note => NoteCustomAppPath,
            _ => MediaCustomAppPath
        };

        public void SetCustomAppPath(IslandModule mod, string path)
        {
            switch (mod)
            {
                case IslandModule.Notification: NotifCustomAppPath = path; break;
                case IslandModule.Hardware: HwCustomAppPath = path; break;
                case IslandModule.Note: NoteCustomAppPath = path; break;
                default: MediaCustomAppPath = path; break;
            }
            SaveConfiguration();
        }

        // 声波条数与高度自定义
        public int WaveBarCount { get; set; } = 5;
        public double WaveMaxHeight { get; set; } = 24.0;

        public double BaseWidth { get; set; } = 280;
        public double CapsuleScale { get; set; } = 1.0;
        public bool IsAutoWidthEnabled { get; set; } = true;
        public bool IsIdleCircleEnabled { get; set; } = true;
        /// <summary>闲置样式（None/Circle/Notch）。</summary>
        public IdleStyle IdleStyle { get; set; } = IdleStyle.Circle;
        public string CurrentThemeKey { get; set; } = "AeroDark";
        public string CurrentWaveColorHex { get; set; } = "#00E5FF";
        /// <summary>字体颜色覆盖（空 = 跟随主题自动）。</summary>
        public string FontColorHex { get; set; } = "";
        private double _currentCalculatedCompactWidth = 280;

        // 四大胶囊独立物理弹簧
        private readonly SpringPhysics _mainWidthSpring = new(280, 260, 18);
        private readonly SpringPhysics _mainHeightSpring = new(44, 210, 14);

        private readonly SpringPhysics _subNotifWidthSpring = new(84, 240, 16);
        private readonly SpringPhysics _subNotifHeightSpring = new(44, 210, 14);

        private readonly SpringPhysics _subHwWidthSpring = new(128, 240, 16);
        private readonly SpringPhysics _subHwHeightSpring = new(44, 210, 14);

        private readonly SpringPhysics _subMediaWidthSpring = new(134, 240, 16);
        private readonly SpringPhysics _subMediaHeightSpring = new(44, 210, 14);

        private readonly SpringPhysics _subNoteWidthSpring = new(132, 240, 16);
        private readonly SpringPhysics _subNoteHeightSpring = new(44, 210, 14);

        private string _currentSongTitle = "";
        private string _currentSongArtist = "";
        private double _wavePhaseTime = 0;

        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public MainWindow()
        {
            InitializeComponent();

            // 窗口标题带版本（读取程序集版本，csproj 单一来源）
            try
            {
                var ver = typeof(MainWindow).Assembly.GetName().Version;
                if (ver != null) Title = $"DynamicIslandWin v{ver.Major}.{ver.Minor}";
            }
            catch { }

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);

            _saveDebounceTimer.Interval = TimeSpan.FromMilliseconds(600);
            _saveDebounceTimer.Tick += (s, args) => { _saveDebounceTimer.Stop(); FlushConfigurationNow(); };
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfiguration();
            RefreshPluginsFromDisk(); // 补齐插件目录默认文件并扫描
            RebuildCapsuleLayout();
            RebuildWaveBars();
            await InitializeMediaSessionAsync();

            _hoverIntentTimer.Interval = TimeSpan.FromMilliseconds(55);
            _hoverIntentTimer.Tick += (s, args) =>
            {
                _hoverIntentTimer.Stop();
                CommitHoverExpansion();
            };

            _hoverLeaveTimer.Interval = TimeSpan.FromMilliseconds(55);
            _hoverLeaveTimer.Tick += (s, args) =>
            {
                _hoverLeaveTimer.Stop();
                CheckAndCollapseInactiveCapsules();
            };

            _switchConfirmTimer.Interval = TimeSpan.FromMilliseconds(70);
            _switchConfirmTimer.Tick += (s, args) =>
            {
                _switchConfirmTimer.Stop();
                CommitPendingSwitch();
            };

            _wheelSwitchTimer.Interval = TimeSpan.FromMilliseconds(90);
            _wheelSwitchTimer.Tick += (s, args) =>
            {
                _wheelSwitchTimer.Stop();
                CommitWheelSwitch();
            };

            _dockAnimTimer.Interval = TimeSpan.FromMilliseconds(16);
            _dockAnimTimer.Tick += (s, args) =>
            {
                double cur = this.Top;
                double next = cur + (_dockAnimToTop - cur) * 0.30;
                if (Math.Abs(_dockAnimToTop - next) < 0.4)
                {
                    next = _dockAnimToTop;
                    _dockAnimTimer.Stop();
                }
                this.Top = next; // 吸附期间 LocationChanged 已跳过保存；解除时正常落盘
            };

            _notifResetTimer.Interval = TimeSpan.FromSeconds(3.5);
            _notifResetTimer.Tick += (s, args) =>
            {
                _notifResetTimer.Stop();
                _isNotificationActive = false;
                CollapseSubNotif();
            };

            _systemStatusTimer.Interval = TimeSpan.FromMilliseconds(400);
            _systemStatusTimer.Tick += async (s, args) =>
            {
                UpdateClockTime();
                UpdateHardwareMetrics();
                EvaluateIdleCircleState();
                EvaluateReminders();
                UpdateExternalForegroundTracker();
                PollSystemNotifications();
                await CheckTrackChangeFallbackAsync();
            };
            _systemStatusTimer.Start();

            StartPhysicsLoop();
            RefreshHeartButtons(); // 依据配置显示/隐藏 ♥ 并贴靠
            _ = InitializeSystemNotificationListenerAsync(); // 监听系统通知（首次需在系统设置里允许“通知访问”）
            _ = CheckForUpdateAsync(); // 启动静默检查更新（无新版则毫无打扰）
        }

        #region 1. 记忆与持久化配置
        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null)
                    {
                        BaseWidth = config.BaseWidth;
                        CapsuleScale = Math.Clamp(config.CapsuleScale, 0.75, 1.45);
                        IsAutoWidthEnabled = config.IsAutoWidthEnabled;
                        IsIdleCircleEnabled = config.IsIdleCircleEnabled;
                        // 闲置样式：新键 IdleStyle 优先；旧配置无此键时用 IsIdleCircleEnabled 推断（true→Circle，false→None）。
                        IdleStyle = json.Contains("\"IdleStyle\":", StringComparison.Ordinal)
                            ? (Enum.IsDefined(typeof(IdleStyle), config.IdleStyle) ? config.IdleStyle : IdleStyle.Circle)
                            : (config.IsIdleCircleEnabled ? IdleStyle.Circle : IdleStyle.None);
                        IsIdleCircleEnabled = IdleStyle != IdleStyle.None; // 同步旧字段，便于兼容读取
                        ConfiguredMainModule = config.ConfiguredMainModule;
                        IsMediaEnabled = config.IsMediaEnabled;
                        IsNotifEnabled = config.IsNotifEnabled;
                        IsHwEnabled = config.IsHwEnabled;
                        IsNoteEnabled = config.IsNoteEnabled;
                        NoteText = config.NoteText ?? "";
            NoteTitle = config.NoteTitle ?? "";
                        NoteLeftAction = config.NoteLeftAction;
                        NoteRightAction = config.NoteRightAction;
                        Reminders = config.Reminders ?? new List<ReminderConfig>();
                        ActivePluginByKind = config.ActivePluginByKind ?? new Dictionary<string, string>();
                        LikeShortcutEnabled = config.LikeShortcutEnabled;
                        LikeShortcutKey = string.IsNullOrWhiteSpace(config.LikeShortcutKey) ? "F5" : config.LikeShortcutKey;
                        IsStartupCheckUpdatesEnabled = config.IsStartupCheckUpdatesEnabled;
                        UpdateManifestUrl = config.UpdateManifestUrl ?? "";
                        LastNotifiedUpdateVersion = config.LastNotifiedUpdateVersion ?? "";
                        // 防旧配置残留非法主模块值（如已删除的枚举）
                        if (ConfiguredMainModule != IslandModule.Media && ConfiguredMainModule != IslandModule.Notification &&
                            ConfiguredMainModule != IslandModule.Hardware && ConfiguredMainModule != IslandModule.Note)
                        {
                            ConfiguredMainModule = IslandModule.Media;
                        }
                        CurrentThemeKey = config.CurrentThemeKey;
                        CurrentWaveColorHex = config.WaveColorHex;
                        FontColorHex = config.FontColorHex ?? "";

                        WaveBarCount = Math.Clamp(config.WaveBarCount, 3, 9);
                        WaveMaxHeight = Math.Clamp(config.WaveMaxHeight, 12.0, 32.0);
                        MediaCustomAppPath = config.MediaCustomAppPath ?? "";
                        NotifCustomAppPath = config.NotifCustomAppPath ?? "";
                        HwCustomAppPath = config.HwCustomAppPath ?? "";
                        NoteCustomAppPath = config.NoteCustomAppPath ?? "";
                        // 旧版全局路径首次迁移到媒体胶囊（仅当三个新路径都为空时）
                        if (string.IsNullOrEmpty(MediaCustomAppPath) && string.IsNullOrEmpty(NotifCustomAppPath) &&
                            string.IsNullOrEmpty(HwCustomAppPath) && !string.IsNullOrEmpty(config.CustomAppPath))
                        {
                            MediaCustomAppPath = config.CustomAppPath;
                        }

                        MediaLeftAction = config.MediaLeftAction;
                        MediaRightAction = config.MediaRightAction;
                        NotifLeftAction = config.NotifLeftAction;
                        NotifRightAction = config.NotifRightAction;
                        HwLeftAction = config.HwLeftAction;
                        HwRightAction = config.HwRightAction;

                        ApplyTheme(CurrentThemeKey);
                        SetWaveColor((Color)ColorConverter.ConvertFromString(CurrentWaveColorHex));
                        SetCapsuleScale(CapsuleScale);

                        if (config.WindowLeft >= 0 && config.WindowTop >= 0 &&
                            config.WindowLeft < SystemParameters.VirtualScreenWidth - 150 &&
                            config.WindowTop < SystemParameters.VirtualScreenHeight - 100)
                        {
                            this.Left = config.WindowLeft;
                            this.Top = config.WindowTop;
                            return;
                        }
                    }
                }
            }
            catch { }

            UpdateNoteContentViews(); // 无论配置来源，都在 XAML 加载完成后同步一次备忘录预览
            PositionToTopCenter();
        }

        /// <summary>请求持久化：600ms 防抖后统一落盘，拖动/滑块等高频调用不再反复写文件。</summary>
        public void SaveConfiguration()
        {
            if (_saveDebounceTimer.IsEnabled) _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        /// <summary>立即落盘（关闭/退出时调用）。</summary>
        internal void FlushConfigurationNow()
        {
            _saveDebounceTimer.Stop();
            try
            {
                var config = new AppConfig
                {
                    WindowLeft = this.Left,
                    WindowTop = this.Top,
                    BaseWidth = this.BaseWidth,
                    CapsuleScale = this.CapsuleScale,
                    IsAutoWidthEnabled = this.IsAutoWidthEnabled,
                    IsIdleCircleEnabled = this.IsIdleCircleEnabled,
                    IdleStyle = this.IdleStyle,
                    CurrentThemeKey = this.CurrentThemeKey,
                    WaveColorHex = this.CurrentWaveColorHex,
                    FontColorHex = this.FontColorHex,
                    WaveBarCount = this.WaveBarCount,
                    WaveMaxHeight = this.WaveMaxHeight,
                    MediaCustomAppPath = this.MediaCustomAppPath,
                    NotifCustomAppPath = this.NotifCustomAppPath,
                    HwCustomAppPath = this.HwCustomAppPath,
                    NoteCustomAppPath = this.NoteCustomAppPath,
                    ConfiguredMainModule = this.ConfiguredMainModule,
                    IsMediaEnabled = this.IsMediaEnabled,
                    IsNotifEnabled = this.IsNotifEnabled,
                    IsHwEnabled = this.IsHwEnabled,
                    IsNoteEnabled = this.IsNoteEnabled,
                    NoteText = this.NoteText,
                    NoteTitle = this.NoteTitle,
                    NoteLeftAction = this.NoteLeftAction,
                    NoteRightAction = this.NoteRightAction,
                    Reminders = this.Reminders,
                    ActivePluginByKind = this.ActivePluginByKind,
                    LikeShortcutEnabled = this.LikeShortcutEnabled,
                    LikeShortcutKey = this.LikeShortcutKey,
                    IsStartupCheckUpdatesEnabled = this.IsStartupCheckUpdatesEnabled,
                    UpdateManifestUrl = this.UpdateManifestUrl,
                    LastNotifiedUpdateVersion = this.LastNotifiedUpdateVersion,
                    MediaLeftAction = this.MediaLeftAction,
                    MediaRightAction = this.MediaRightAction,
                    NotifLeftAction = this.NotifLeftAction,
                    NotifRightAction = this.NotifRightAction,
                    HwLeftAction = this.HwLeftAction,
                    HwRightAction = this.HwRightAction
                };

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch { }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => FlushConfigurationNow();
        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            // 刘海吸附时窗口被临时上移，不落盘，避免把“吸附位”写成用户位置；离开闲置恢复位置时再正常保存。
            if (_isIdleWindowDocked) return;
            SaveConfiguration(); // 拖动时防抖落盘，不再逐像素写盘
        }
        #endregion

        #region 2. 自定义程序唤起与动作分发
        /// <summary>启动指定胶囊的自定义程序路径；路径为空或被系统拒绝时安全忽略并记录。</summary>
        public void ExecuteCustomApp(string? customPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(customPath))
                {
                    Process.Start(new ProcessStartInfo(customPath) { UseShellExecute = true });
                }
            }
            catch (Exception ex) { AppLog.Write(ex, "launch-custom-app"); }
        }

        /// <summary>打开当前播放来源对应的音乐软件（按 GSMTC 会话的 SourceAppUserModelId 唤起）。</summary>
        private void LaunchMediaSourceApp()
        {
            string? aumid = null;
            try { aumid = _activeSession?.SourceAppUserModelId; } catch { }
            if (string.IsNullOrWhiteSpace(aumid))
            {
                AppLog.Write(new InvalidOperationException("no active media session source"), "launch-media-app");
                return;
            }

            // 打包应用/已注册开始菜单应用：走 shell:AppsFolder；桌面 exe 直启作为兜底
            Exception? lastError = null;
            try
            {
                Process.Start(new ProcessStartInfo("shell:AppsFolder\\" + aumid) { UseShellExecute = true });
                return;
            }
            catch (Exception ex) { lastError = ex; }

            if (aumid.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(aumid) { UseShellExecute = true });
                    return;
                }
                catch (Exception ex) { lastError = ex; }
            }
            AppLog.Write(lastError ?? new InvalidOperationException("cannot launch " + aumid), "launch-media-app");
        }

        #region 喜欢（鼠标宏式：点按 ♥ 后把配置按键序列发送给前台播放器）
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private IntPtr _lastForeignHwnd;

        public void SetLikeShortcutEnabled(bool enabled)
        {
            LikeShortcutEnabled = enabled;
            RefreshHeartButtons();
            SaveConfiguration();
        }

        /// <summary>自定义发送键（如 F5 / Ctrl+F5 / Ctrl+J）；格式非法则拒绝并返回 false。</summary>
        public bool SetLikeShortcutKey(string key)
        {
            if (!TryParseLikeCombo(key, out _, out _)) return false;
            LikeShortcutKey = key.Trim();
            SaveConfiguration();
            return true;
        }

        /// <summary>解析 “Ctrl+Shift+F5” 形式的键组合：返回修饰键 VK 列表与主键 VK。</summary>
        private static bool TryParseLikeCombo(string? text, out List<byte> modVks, out byte key)
        {
            modVks = new List<byte>();
            key = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string main = "";
            foreach (string raw in text.Split('+'))
            {
                string p = raw.Trim().ToUpperInvariant();
                if (p == "CTRL") { modVks.Add(0x11); continue; }
                if (p == "ALT") { modVks.Add(0x12); continue; }
                if (p == "SHIFT") { modVks.Add(0x10); continue; }
                if (p.Length == 0) continue;
                if (main.Length != 0) return false; // 多个主键视为非法
                main = p;
            }
            if (main.Length == 0) return false;
            if (main[0] == 'F' && int.TryParse(main.Substring(1), out int fn) && fn >= 1 && fn <= 24)
            {
                key = (byte)(0x70 + fn - 1);
                return true;
            }
            if (main.Length == 1 && ((main[0] >= 'A' && main[0] <= 'Z') || (main[0] >= '0' && main[0] <= '9')))
            {
                key = (byte)main[0];
                return true;
            }
            return false;
        }

        /// <summary>注册全局触发键 Ctrl+Alt+Shift+L 并挂接消息钩子（喜欢功能启用时有效）。</summary>
        /// <summary>♥ 触发：把播放器窗口带回前台（用户最后一次使用的外部窗口）后模拟按键。</summary>
        private async Task TriggerLikeShortcutAsync()
        {
            try
            {
                IntPtr target = _lastForeignHwnd;
                IntPtr self = new WindowInteropHelper(this).Handle;
                if (target != IntPtr.Zero && target != self)
                {
                    SetForegroundWindow(target);
                    await Task.Delay(120);
                }
            }
            catch { }
            SendConfiguredLikeKey();
        }

        /// <summary>心跳里记录“最近一次非本程序前台窗口”，供 ♥ 按钮定位播放器。</summary>
        private void UpdateExternalForegroundTracker()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return;
                IntPtr self = new WindowInteropHelper(this).Handle;
                if (fg == self) return;
                GetWindowThreadProcessId(fg, out uint pid);
                if (pid != 0 && pid != Environment.ProcessId) _lastForeignHwnd = fg;
            }
            catch { }
        }

        private void SendConfiguredLikeKey()
        {
            try
            {
                if (!TryParseLikeCombo(LikeShortcutKey, out var modVks, out byte vk)) return;
                foreach (byte m in modVks) keybd_event(m, 0, 0, UIntPtr.Zero);
                keybd_event(vk, 0, 0, UIntPtr.Zero);
                keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                for (int i = modVks.Count - 1; i >= 0; i--) keybd_event(modVks[i], 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex) { AppLog.Write(ex, "like-key"); }
        }

        private bool _heartLiked; // 本地喜欢态：点一次执行并变红保持，再点一次发键并复原（多数播放器同键可取消）

        private void RefreshHeartButtons()
        {
            if (BtnLikeHeart == null) return;
            Visibility v = LikeShortcutEnabled ? Visibility.Visible : Visibility.Collapsed;
            BtnLikeHeart.Visibility = v;
            if (BtnLikeSubHeart != null) BtnLikeSubHeart.Visibility = v;
            UpdateHeartVisuals();
        }

        /// <summary>苹果风心形：喜欢=粉色填充实心；未喜欢=透明+浅描边。两处卡片同步显示本地态。</summary>
        private void UpdateHeartVisuals()
        {
            if (TxtLikeHeart == null) return;
            Brush fill = _heartLiked ? new SolidColorBrush(Color.FromRgb(255, 45, 85)) : Brushes.Transparent;
            Brush stroke = _heartLiked ? new SolidColorBrush(Color.FromRgb(255, 45, 85)) : new SolidColorBrush(Color.FromArgb(176, 255, 255, 255));
            TxtLikeHeart.Fill = fill;
            TxtLikeHeart.Stroke = stroke;
            if (TxtLikeSubHeart != null)
            {
                TxtLikeSubHeart.Fill = fill;
                TxtLikeSubHeart.Stroke = stroke;
            }
        }

        private async void BtnLike_Click(object sender, RoutedEventArgs e)
        {
            _heartLiked = !_heartLiked;      // 先翻转本地态：红心保持，不自动变回
            UpdateHeartVisuals();
            await TriggerLikeShortcutAsync(); // 鼠标宏：把配置按键发给前台播放器（再次点击可发键取消喜欢）
        }

        #region 系统通知监听（UserNotificationListener → 通知胶囊展示）
        private Windows.UI.Notifications.Management.UserNotificationListener? _notifListener;
        private readonly HashSet<uint> _seenNotifIds = new();
        private bool _notifListenerBusy;

        public async Task InitializeSystemNotificationListenerAsync()
        {
            try
            {
                var listener = Windows.UI.Notifications.Management.UserNotificationListener.Current;
                if (listener == null) return;
                var status = await listener.RequestAccessAsync();
                if (status != Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus.Allowed)
                {
                    AppLog.Info("system notification access: " + status);
                    return;
                }
                _notifListener = listener;
                AppLog.Info("system notification listener ready");

                // 初次：把已有通知全部记为“已见”，只展示之后的新通知
                try
                {
                    var existing = await listener.GetNotificationsAsync(Windows.UI.Notifications.NotificationKinds.Toast);
                    foreach (var n in existing) _seenNotifIds.Add(n.Id);
                }
                catch { }
            }
            catch (Exception ex) { AppLog.Write(ex, "notif-listener-init"); }
        }

        /// <summary>心跳轮询（约 2s 一次）：桌面应用环境下的 UserNotificationListener 事件订阅不可靠，改轮询同样可达。</summary>
        private DateTime _lastNotifPollUtc = DateTime.MinValue;
        private void PollSystemNotifications()
        {
            if (_notifListener == null) return;
            if ((DateTime.UtcNow - _lastNotifPollUtc).TotalSeconds < 2) return;
            _lastNotifPollUtc = DateTime.UtcNow;
            _ = RefreshAndShowLatestNotificationAsync();
        }

        private async Task RefreshAndShowLatestNotificationAsync()
        {
            if (_notifListener == null || _notifListenerBusy) return;
            _notifListenerBusy = true;
            try
            {
                var items = await _notifListener.GetNotificationsAsync(Windows.UI.Notifications.NotificationKinds.Toast);
                Windows.UI.Notifications.UserNotification? candidate = null;
                foreach (var n in items)
                {
                    if (_seenNotifIds.Contains(n.Id)) continue;
                    if (candidate == null || n.Id > candidate.Id) candidate = n;
                }
                if (candidate != null)
                {
                    _seenNotifIds.Add(candidate.Id);
                    if (_seenNotifIds.Count > 600)
                    {
                        _seenNotifIds.Clear();
                        foreach (var n in items) _seenNotifIds.Add(n.Id);
                    }
                    ShowUserNotification(candidate);
                }
            }
            catch (Exception ex) { AppLog.Write(ex, "notif-refresh"); }
            finally { _notifListenerBusy = false; }
        }

        private void ShowUserNotification(Windows.UI.Notifications.UserNotification n)
        {
            try
            {
                string app = "系统通知";
                try { app = n.AppInfo?.DisplayInfo?.DisplayName ?? app; } catch { }

                string? title = null;
                string? body = null;
                try
                {
                    var bindings = n.Notification?.Visual?.Bindings;
                    if (bindings != null)
                    {
                        foreach (var b in bindings)
                        {
                            var texts = b.GetTextElements();
                            if (texts == null) continue;
                            foreach (var te in texts)
                            {
                                string? s = te?.Text;
                                if (string.IsNullOrWhiteSpace(s)) continue;
                                if (title == null) title = s;
                                else if (body == null) body = s;
                            }
                        }
                    }
                }
                catch { }

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body)) return;
                string message = string.IsNullOrWhiteSpace(title) ? body! : (string.IsNullOrWhiteSpace(body) ? title : title + "：" + body);
                TriggerNotification(app, message);
            }
            catch { }
        }
        #endregion

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        #endregion

        public void ExecuteMediaAction(MediaAction action, CapsuleId capsule)
        {
            try
            {
                switch (action)
                {
                    case MediaAction.ToggleExpand: ToggleCapsuleExpand(capsule); break;
                    case MediaAction.TogglePlayPause: BtnPlayPause_Click(this, new RoutedEventArgs()); break;
                    case MediaAction.NextTrack: BtnNext_Click(this, new RoutedEventArgs()); break;
                    case MediaAction.PrevTrack: BtnPrev_Click(this, new RoutedEventArgs()); break;
                    case MediaAction.OpenMusicApp: LaunchMediaSourceApp(); break;
                    case MediaAction.OpenCustomApp: ExecuteCustomApp(MediaCustomAppPath); break;
                    case MediaAction.OpenSettings: OpenSettingsWindow(); break;
                    case MediaAction.None: default: break;
                }
            }
            catch { }
        }

        public void ExecuteNotifAction(NotifAction action, CapsuleId capsule)
        {
            try
            {
                switch (action)
                {
                    case NotifAction.ToggleExpand: ToggleCapsuleExpand(capsule); break;
                    case NotifAction.OpenWindowsTimeSettings: Process.Start(new ProcessStartInfo("ms-settings:dateandtime") { UseShellExecute = true }); break;
                    case NotifAction.ClearNotification:
                        _isNotificationActive = false;
                        _notifResetTimer.Stop();
                        CollapseSubNotif();
                        TxtSubNotifBanner.Text = "暂无未读消息";
                        TxtMainNotifBanner.Text = "暂无新通知，系统运行正常";
                        break;
                    case NotifAction.OpenCustomApp: ExecuteCustomApp(NotifCustomAppPath); break;
                    case NotifAction.OpenSettings: OpenSettingsWindow(); break;
                    case NotifAction.None: default: break;
                }
            }
            catch { }
        }

        public void ExecuteHwAction(HwAction action, CapsuleId capsule)
        {
            try
            {
                switch (action)
                {
                    case HwAction.OpenTaskManager: Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); break;
                    case HwAction.ToggleExpand: ToggleCapsuleExpand(capsule); break;
                    case HwAction.OpenResourceMonitor: Process.Start(new ProcessStartInfo("resmon.exe") { UseShellExecute = true }); break;
                    case HwAction.OpenWindowsSettings: Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true }); break;
                    case HwAction.OpenCustomApp: ExecuteCustomApp(HwCustomAppPath); break;
                    case HwAction.OpenSettings: OpenSettingsWindow(); break;
                    case HwAction.None: default: break;
                }
            }
            catch { }
        }

        public void ExecuteNoteAction(NoteAction action, CapsuleId capsule)
        {
            try
            {
                switch (action)
                {
                    case NoteAction.ToggleExpand: ToggleCapsuleExpand(capsule); break;
                    case NoteAction.OpenSettings: OpenSettingsWindow(); break;
                    case NoteAction.OpenCustomApp: ExecuteCustomApp(NoteCustomAppPath); break;
                    case NoteAction.None: default: break;
                }
            }
            catch { }
        }

        private void ToggleCapsuleExpand(CapsuleId capsule)
        {
            if (_activeExpandedCapsule == capsule) CollapseAll();
            else { _pendingHoverCapsule = capsule; CommitHoverExpansion(); }
        }

        private SettingsWindow? _settingsWindow;
        public void OpenSettingsWindow()
        {
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                try { _settingsWindow.Activate(); } catch { }
                return;
            }

            _settingsWindow = new SettingsWindow(this)
            {
                Left = this.Left + (this.Width - 650) / 2,
                Top = this.Top + 70
            };
            _settingsWindow.Closed += (s, args) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        /// <summary>启动静默检查更新：拉取远端清单，若比当前新且未提示过 → 弹“发现新版本”并提供下载页。</summary>
        private async Task CheckForUpdateAsync()
        {
            try
            {
                if (!IsStartupCheckUpdatesEnabled || string.IsNullOrWhiteSpace(UpdateManifestUrl)) return;
                await Task.Delay(2500).ConfigureAwait(true); // 等主窗口稳定再提示，避免打断启动
                var info = await UpdateChecker.FetchAsync(UpdateManifestUrl).ConfigureAwait(true);
                if (info == null || string.IsNullOrWhiteSpace(info.Version)) return;
                string current = CurrentVersionString();
                if (!UpdateChecker.IsNewer(current, info.Version)) return;
                if (info.Version == LastNotifiedUpdateVersion) return; // 同一版只提示一次
                LastNotifiedUpdateVersion = info.Version;
                SaveConfiguration();

                string msg = $"发现新版本 v{info.Version}（当前 v{current}）。";
                if (!string.IsNullOrWhiteSpace(info.Note)) msg += $"\n\n更新说明：{info.Note}";
                msg += "\n\n是否打开下载页？";
                var res = MessageBox.Show(this, msg, "DynamicIslandWin 有更新",
                    MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (res == MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(info.Url))
                {
                    try { Process.Start(new ProcessStartInfo(info.Url) { UseShellExecute = true }); } catch { }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write(ex, "update-check"); // 静默：无网络/清单不存在都不打扰
            }
        }

        private static string CurrentVersionString()
        {
            try
            {
                var ver = typeof(MainWindow).Assembly.GetName().Version;
                if (ver != null) return $"{ver.Major}.{ver.Minor}.{ver.Build}";
            }
            catch { }
            return "0.0.0";
        }

        private void MainIsland_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 备注展开态：左键专用于“单点编辑”，不触发常规左键动作（收起/其它）
            if (ActiveMainModule == IslandModule.Note && MainNote_Expanded.Visibility == Visibility.Visible) { e.Handled = true; return; }
            switch (ActiveMainModule)
            {
                case IslandModule.Media: ExecuteMediaAction(MediaLeftAction, CapsuleId.Main); break;
                case IslandModule.Notification: ExecuteNotifAction(NotifLeftAction, CapsuleId.Main); break;
                case IslandModule.Hardware: ExecuteHwAction(HwLeftAction, CapsuleId.Main); break;
                case IslandModule.Note: ExecuteNoteAction(NoteLeftAction, CapsuleId.Main); break;
            }
        }

        private void MainIsland_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenSettingsWindow(); // 主胶囊右键固定为打开设置（无论当前承载哪个模块）
            e.Handled = true;
        }

        private void SubNotifIsland_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ExecuteNotifAction(NotifLeftAction, CapsuleId.SubNotif);
        private void SubNotifIsland_MouseRightButtonUp(object sender, MouseButtonEventArgs e) { ExecuteNotifAction(NotifRightAction, CapsuleId.SubNotif); e.Handled = true; }
        private void SubHwIsland_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ExecuteHwAction(HwLeftAction, CapsuleId.SubHw);
        private void SubHwIsland_MouseRightButtonUp(object sender, MouseButtonEventArgs e) { ExecuteHwAction(HwRightAction, CapsuleId.SubHw); e.Handled = true; }
        private void SubMediaIsland_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ExecuteMediaAction(MediaLeftAction, CapsuleId.SubMedia);
        private void SubMediaIsland_MouseRightButtonUp(object sender, MouseButtonEventArgs e) { ExecuteMediaAction(MediaRightAction, CapsuleId.SubMedia); e.Handled = true; }
        private void SubNoteIsland_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 备注展开态：左键专用于“单点编辑”
            if (SubNote_ExpandedView.Visibility == Visibility.Visible) { e.Handled = true; return; }
            ExecuteNoteAction(NoteLeftAction, CapsuleId.SubNote);
        }
        private void SubNoteIsland_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 备忘副胶囊右键走可配置动作（默认打开设置）；主胶囊右键仍固定为设置
            ExecuteNoteAction(NoteRightAction, CapsuleId.SubNote);
            e.Handled = true;
        }
        #endregion

        #region 3. 全局大小缩放与声波柱动态生成
        public void SetCapsuleScale(double scale)
        {
            CapsuleScale = Math.Clamp(scale, 0.75, 1.45);
            if (CapsulesGlobalScale != null)
            {
                CapsulesGlobalScale.ScaleX = CapsuleScale;
                CapsulesGlobalScale.ScaleY = CapsuleScale;
            }
            SaveConfiguration();
        }

        public void SetWaveConfig(int count, double maxHeight)
        {
            WaveBarCount = Math.Clamp(count, 3, 9);
            WaveMaxHeight = Math.Clamp(maxHeight, 12.0, 32.0);
            RebuildWaveBars();
            SaveConfiguration();
        }

        public void RebuildWaveBars()
        {
            if (WaveContainer == null) return;
            WaveContainer.Children.Clear();

            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(CurrentWaveColorHex));

            for (int i = 0; i < WaveBarCount; i++)
            {
                var bar = new Border
                {
                    Width = 2.5,
                    Height = 6,
                    CornerRadius = new CornerRadius(1.2),
                    Background = brush,
                    Margin = new Thickness(1, 0, 1, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                WaveContainer.Children.Add(bar);
            }
        }
        #endregion

        #region 4. 文字物理像素测算
        private double MeasureExactTextWidth(string text, double fontSize, FontWeight weight)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI, Microsoft YaHei UI"), FontStyles.Normal, weight, FontStretches.Normal),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            return formattedText.WidthIncludingTrailingWhitespace;
        }

        /// <summary>按当前主模块与最新曲名计算“理想紧凑宽度”（无状态守卫，随时可取）。</summary>
        private double ComputeMainCompactWidth()
        {
            if (!IsAutoWidthEnabled) return BaseWidth;

            if (ActiveMainModule == IslandModule.Media)
            {
                string fullText = string.IsNullOrEmpty(_currentSongArtist) ? _currentSongTitle : $"{_currentSongTitle}  ·  {_currentSongArtist}";
                double textPixelWidth = MeasureExactTextWidth(fullText, 12, FontWeights.SemiBold);
                double needed = textPixelWidth + 112;
                return Math.Clamp(needed, 160, 540);
            }
            if (ActiveMainModule == IslandModule.Notification) return 84;
            if (ActiveMainModule == IslandModule.Hardware) return 128;
            if (ActiveMainModule == IslandModule.Note)
            {
                // 备忘录紧凑/闲置显示标题，宽度随标题自适应
                string t = string.IsNullOrWhiteSpace(NoteTitle) ? "" : NoteTitle.Trim();
                if (string.IsNullOrEmpty(t)) t = FirstLineOf(NoteText, 20);
                double textPixelWidth = MeasureExactTextWidth(t, 12, FontWeights.SemiBold);
                double needed = textPixelWidth + 48; // 图标 + 间距 + 内边距
                return Math.Clamp(needed, 120, 540);
            }
            return BaseWidth;
        }

        public void RecalculateMainCompactWidth()
        {
            if (_activeExpandedCapsule == CapsuleId.Main || _isCircleIdle) return;

            _currentCalculatedCompactWidth = ComputeMainCompactWidth();
            _mainWidthSpring.Target = _currentCalculatedCompactWidth;
            StartPhysicsLoop();
        }

        public void SetAutoWidthEnabled(bool enabled)
        {
            IsAutoWidthEnabled = enabled;
            RecalculateMainCompactWidth();
            SaveConfiguration();
        }

        public void SetIdleStyle(IdleStyle style)
        {
            IdleStyle = style;
            IsIdleCircleEnabled = style != IdleStyle.None; // 同步旧字段，便于兼容读取
            if (style == IdleStyle.None && _isCircleIdle) LeaveIdle();
            SaveConfiguration();
        }

        // 旧版接口兼容：勾选“闲置小圆点”映射到 Circle/None。
        public void SetIdleCircleEnabled(bool enabled) => SetIdleStyle(enabled ? IdleStyle.Circle : IdleStyle.None);
        #endregion

        #region 5. 闲置形态收敛/舒展（小圆点 / 刘海吸附）
        private void EvaluateIdleCircleState()
        {
            if (IdleStyle == IdleStyle.None) return;

            bool isMusicPlaying = false;
            if (_activeSession != null)
            {
                try
                {
                    isMusicPlaying = _activeSession.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                }
                catch { }
            }

            bool isMouseHovering = MainIsland.IsMouseOver || SubNotifIsland.IsMouseOver || SubHwIsland.IsMouseOver || SubNoteIsland.IsMouseOver;
            // 各模块可否进入“全局闲置（小圆点/刘海）”：
            //  - 媒体：无播放即闲置；  - 时间/通知：除收到通知都闲置；  - 硬件：取消常驻 → 始终可闲置；
            //  - 备注：常态=紧凑显示标题（闲置态即标题，不再进全局闲置小圆点/刘海）
            bool moduleIdle = ActiveMainModule switch
            {
                IslandModule.Media => !isMusicPlaying,
                IslandModule.Notification => !_isNotificationActive,
                IslandModule.Hardware => true,
                IslandModule.Note => false,
                _ => !isMusicPlaying
            };
            bool shouldBeIdle = moduleIdle && !isMouseHovering && !_isMainExpanded && (_activeExpandedCapsule == CapsuleId.None);

            if (shouldBeIdle)
            {
                if (!_isCircleIdle) EnterIdle();
            }
            else
            {
                if (_isCircleIdle) LeaveIdle();
            }
        }

        private void EnterIdle()
        {
            _isCircleIdle = true;
            if (IdleStyle == IdleStyle.Notch)
            {
                // “刘海”药丸形：吸到桌面顶部边缘，宽度取固定药丸宽，高度取胶囊高（全圆角）。
                _mainWidthSpring.Target = NotchIdleWidth;
                _mainHeightSpring.Target = NotchIdleHeight;
                DockToTopEdge();
            }
            else
            {
                _mainWidthSpring.Target = 44;
                _mainHeightSpring.Target = 44;
            }
            _subNotifWidthSpring.Target = 0;
            _subHwWidthSpring.Target = 0;
            _subMediaWidthSpring.Target = 0;
            _subNoteWidthSpring.Target = 0;
            // 立即隐藏主内容与各副胶囊，避免回缩过程中“紧凑单行内容”残留成残影——
            // 物理循环可能在收敛首帧即停止（未播时），依赖第二连续帧的 Collapse 未必执行。
            if (MainIslandContentRoot != null) MainIslandContentRoot.Visibility = Visibility.Collapsed;
            SubNotifIsland.Visibility = Visibility.Collapsed;
            SubHwIsland.Visibility = Visibility.Collapsed;
            SubMediaIsland.Visibility = Visibility.Collapsed;
            SubNoteIsland.Visibility = Visibility.Collapsed;
            StartPhysicsLoop();
        }

        private void LeaveIdle()
        {
            _isCircleIdle = false;
            UndockFromTopEdge();

            if (MainIslandContentRoot != null) MainIslandContentRoot.Visibility = Visibility.Visible;
            if (IsNotifEnabled && ConfiguredMainModule != IslandModule.Notification) SubNotifIsland.Visibility = Visibility.Visible;
            if (IsHwEnabled && ConfiguredMainModule != IslandModule.Hardware) SubHwIsland.Visibility = Visibility.Visible;
            if (IsMediaEnabled && ConfiguredMainModule != IslandModule.Media) SubMediaIsland.Visibility = Visibility.Visible;
            if (IsNoteEnabled && ConfiguredMainModule != IslandModule.Note) SubNoteIsland.Visibility = Visibility.Visible;

            _mainHeightSpring.Target = 44;
            // 舒展瞬间按最新曲名重算宽度（修复空闲圆期间切歌后恢复宽度失效）
            _currentCalculatedCompactWidth = ComputeMainCompactWidth();
            _mainWidthSpring.Target = _currentCalculatedCompactWidth;

            if (SubNotifIsland.Visibility == Visibility.Visible) _subNotifWidthSpring.Target = 84;
            if (SubHwIsland.Visibility == Visibility.Visible) _subHwWidthSpring.Target = 128;
            if (SubMediaIsland.Visibility == Visibility.Visible) _subMediaWidthSpring.Target = 134;
            if (SubNoteIsland.Visibility == Visibility.Visible) _subNoteWidthSpring.Target = 132;

            StartPhysicsLoop();
        }

        /// <summary>刘海吸附：平滑上移窗口，使主胶囊贴合桌面顶部（避免瞬间跳顶的生硬过渡）。</summary>
        internal void DockToTopEdge()
        {
            if (_isIdleWindowDocked) return;
            _isIdleWindowDocked = true;
            _idleRestoreLeft = this.Left;
            _idleRestoreTop = this.Top;
            // 主胶囊在窗口内顶部 Margin=IslandTopMargin；窗口 Top 目标 = 贴顶间隙 - Margin
            _dockAnimToTop = NotchIdleTopGap - IslandTopMargin;
            if (!_dockAnimTimer.IsEnabled) _dockAnimTimer.Start();
        }

        /// <summary>解除刘海吸附：平滑回到进入闲置前窗口位置。</summary>
        internal void UndockFromTopEdge()
        {
            if (!_isIdleWindowDocked) return;
            _isIdleWindowDocked = false;
            this.Left = _idleRestoreLeft;
            _dockAnimToTop = _idleRestoreTop;
            if (!_dockAnimTimer.IsEnabled) _dockAnimTimer.Start();
        }
        #endregion

        #region 6. 物理弹簧主循环与流体声波
        private void StartPhysicsLoop()
        {
            if (!_isPhysicsRunning)
            {
                _isPhysicsRunning = true;
                _physicsSettledFrame = false;
                _physicsStopwatch.Restart();
                CompositionTarget.Rendering += OnPhysicsFrame;
            }
        }

        private void StopPhysicsLoop()
        {
            if (_isPhysicsRunning)
            {
                _isPhysicsRunning = false;
                _physicsStopwatch.Stop();
                CompositionTarget.Rendering -= OnPhysicsFrame;
            }
        }

        private void OnPhysicsFrame(object? sender, EventArgs e)
        {
            double dt = _physicsStopwatch.Elapsed.TotalSeconds;
            _physicsStopwatch.Restart();

            if (dt > 0.033) dt = 0.033;
            if (dt <= 0.0001) return;

            UpdateOrganicFluidWaveform(dt);

            bool r1 = _mainWidthSpring.Update(dt);
            bool r2 = _mainHeightSpring.Update(dt);
            bool r3 = _subNotifWidthSpring.Update(dt);
            bool r4 = _subNotifHeightSpring.Update(dt);
            bool r5 = _subHwWidthSpring.Update(dt);
            bool r6 = _subHwHeightSpring.Update(dt);
            bool r7 = _subMediaWidthSpring.Update(dt);
            bool r8 = _subMediaHeightSpring.Update(dt);
            bool r9 = _subNoteWidthSpring.Update(dt);
            bool r10 = _subNoteHeightSpring.Update(dt);

            bool settledNow = r1 && r2 && r3 && r4 && r5 && r6 && r7 && r8 && r9 && r10;
            if (settledNow && _physicsSettledFrame)
            {
                // 连续两帧收敛：冻结尺寸/占位写入（消除静止期亚像素微抖，波形仍独立运行）
                if (_isCircleIdle)
                {
                    if (SubNotifIsland.Width <= 1) SubNotifIsland.Visibility = Visibility.Collapsed;
                    if (SubHwIsland.Width <= 1) SubHwIsland.Visibility = Visibility.Collapsed;
                    if (SubMediaIsland.Width <= 1) SubMediaIsland.Visibility = Visibility.Collapsed;
                    if (SubNoteIsland.Width <= 1) SubNoteIsland.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                if (settledNow)
                {
                    // 首帧收敛：精确落到目标值并补齐占位，随后按播放状态挂起循环
                    MainIsland.Width = _mainWidthSpring.Target;
                    MainIsland.Height = _mainHeightSpring.Target;
                    SubNotifIsland.Width = _subNotifWidthSpring.Target;
                    SubNotifIsland.Height = _subNotifHeightSpring.Target;
                    SubHwIsland.Width = _subHwWidthSpring.Target;
                    SubHwIsland.Height = _subHwHeightSpring.Target;
                    SubMediaIsland.Width = _subMediaWidthSpring.Target;
                    SubMediaIsland.Height = _subMediaHeightSpring.Target;
                    SubNoteIsland.Width = _subNoteWidthSpring.Target;
                    SubNoteIsland.Height = _subNoteHeightSpring.Target;
                    UpdateCenterPadding();
                    _physicsSettledFrame = true;

                    bool isPlaying = false;
                    try { isPlaying = _activeSession?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; } catch { }
                    if (!isPlaying) StopPhysicsLoop();
                }
                else
                {
                    MainIsland.Width = Math.Max(44, _mainWidthSpring.Current);
                    MainIsland.Height = Math.Max(44, _mainHeightSpring.Current);
                    SubNotifIsland.Width = Math.Max(0, _subNotifWidthSpring.Current);
                    SubNotifIsland.Height = Math.Max(0, _subNotifHeightSpring.Current);
                    SubHwIsland.Width = Math.Max(0, _subHwWidthSpring.Current);
                    SubHwIsland.Height = Math.Max(0, _subHwHeightSpring.Current);
                    SubMediaIsland.Width = Math.Max(0, _subMediaWidthSpring.Current);
                    SubMediaIsland.Height = Math.Max(0, _subMediaHeightSpring.Current);
                    SubNoteIsland.Width = Math.Max(0, _subNoteWidthSpring.Current);
                    SubNoteIsland.Height = Math.Max(0, _subNoteHeightSpring.Current);
                    UpdateCenterPadding();
                    _physicsSettledFrame = false;
                }

                if (_isCircleIdle)
                {
                    SubNotifIsland.Opacity = Math.Clamp(SubNotifIsland.Width / 60.0, 0.0, 1.0);
                    SubHwIsland.Opacity = Math.Clamp(SubHwIsland.Width / 80.0, 0.0, 1.0);
                    SubMediaIsland.Opacity = Math.Clamp(SubMediaIsland.Width / 80.0, 0.0, 1.0);
                    SubNoteIsland.Opacity = Math.Clamp(SubNoteIsland.Width / 80.0, 0.0, 1.0);

                    if (SubNotifIsland.Width <= 1) SubNotifIsland.Visibility = Visibility.Collapsed;
                    if (SubHwIsland.Width <= 1) SubHwIsland.Visibility = Visibility.Collapsed;
                    if (SubMediaIsland.Width <= 1) SubMediaIsland.Visibility = Visibility.Collapsed;
                    if (SubNoteIsland.Width <= 1) SubNoteIsland.Visibility = Visibility.Collapsed;
                }
                else
                {
                    SubNotifIsland.Opacity = 1.0;
                    SubHwIsland.Opacity = 1.0;
                    SubMediaIsland.Opacity = 1.0;
                    SubNoteIsland.Opacity = 1.0;
                }
            }

            double t = Math.Clamp((MainIsland.Width - 44.0) / 120.0, 0.0, 1.0);
            if (MainIslandShadow != null)
            {
                if (CurrentThemeKey == "AeroLight" || CurrentThemeKey == "AeroDark")
                {
                    MainIslandShadow.BlurRadius = 3.0 + 8.0 * t;
                    MainIslandShadow.Opacity = 0.04 + 0.10 * t;
                    MainIslandShadow.ShadowDepth = 1.0 + 1.0 * t;
                }
                else
                {
                    MainIslandShadow.BlurRadius = 5.0 + 15.0 * t;
                    MainIslandShadow.Opacity = 0.12 + 0.43 * t;
                    MainIslandShadow.ShadowDepth = 1.0 + 2.5 * t;
                }
            }

            double crossFade = Math.Clamp((MainIsland.Width - 60.0) / 70.0, 0.0, 1.0);
            if (MainIslandContentRoot != null) MainIslandContentRoot.Opacity = crossFade;
            if (IdleCircleDot != null) IdleCircleDot.Opacity = (1.0 - crossFade);

            // 副胶囊阴影随自身宽度插值：小胶囊只留微光，放大过程不再出现黑色“雾”
            ApplySubShadow(SubNotifShadow, SubNotifIsland.Width);
            ApplySubShadow(SubHwShadow, SubHwIsland.Width);
            ApplySubShadow(SubMediaShadow, SubMediaIsland.Width);
            ApplySubShadow(SubNoteShadow, SubNoteIsland.Width);
        }

        /// <summary>按宽度把阴影在“微光(紧凑) ↔ 主题基准(展开)”间插值。</summary>
        private void ApplySubShadow(DropShadowEffect? shadow, double width)
        {
            if (shadow == null) return;
            double u = Math.Clamp((width - 44.0) / 230.0, 0.0, 1.0);
            shadow.BlurRadius = _shadowBaseBlur * (0.22 + 0.78 * u);
            shadow.Opacity = _shadowBaseOpacity * (0.26 + 0.74 * u);
            shadow.ShadowDepth = _shadowBaseDepth * (0.4 + 0.6 * u);
        }

        private void UpdateOrganicFluidWaveform(double dt)
        {
            bool isPlaying = false;
            try { isPlaying = _activeSession?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; } catch { }

            int count = WaveContainer.Children.Count;
            if (count == 0) return;

            if (isPlaying)
            {
                _wavePhaseTime += dt * 6.8;

                for (int i = 0; i < count; i++)
                {
                    if (WaveContainer.Children[i] is Border bar)
                    {
                        double phase = i * 0.75;
                        double factor = Math.Sin(_wavePhaseTime * (1.2 + (i % 3) * 0.3) + phase) * 0.4 +
                                        Math.Cos(_wavePhaseTime * (1.8 - (i % 2) * 0.4) + phase) * 0.3 + 0.5;

                        double h = 5.0 + (WaveMaxHeight - 5.0) * factor;
                        bar.Height = Math.Clamp(h, 4.0, WaveMaxHeight);
                        bar.Opacity = 1.0;
                    }
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    if (WaveContainer.Children[i] is Border bar)
                    {
                        bar.Height = 3.5;
                        bar.Opacity = 0.5;
                    }
                }
            }
        }
        #endregion

        #region 7. 几何曲率重算 (Squircle)
        private void MainIsland_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateMainIslandGeometry(e.NewSize.Width, e.NewSize.Height);
        private void UpdateMainIslandGeometry(double w, double h)
        {
            if (w <= 0 || h <= 0) return;
            // 刘海吸附闲态：用“水滴/黏着”胶囊形（轻微贴边），其余走常规圆角
            if (_isCircleIdle && IdleStyle == IdleStyle.Notch)
            {
                IslandShapePath.Data = BuildNotchDropletGeometry(w, h);
                return;
            }
            // 圆角用“当前模块的紧凑宽度 → 该模块展开宽度”作为基准，使每个模块展开时都收敛到同一种矩形圆角，
            // 避免展开态切换模块时因沿用上一模块的紧凑宽度（或固定 460）导致形状被拉成不同的大圆角“变胖”。
            double compact = _currentCalculatedCompactWidth;
            double expanded = GetModuleExpandedSize(ActiveMainModule).Width;
            double denom = expanded - compact;
            double progress = Math.Abs(denom) < 1e-3 ? 1.0 : Math.Clamp((w - compact) / denom, 0.0, 1.0);
            double r = (h / 2.0) * (1.0 - progress) + 24.0 * progress;
            IslandShapePath.Data = BuildSquircleGeometry(w, h, r);
        }

        /// <summary>刘海吸附“托盘/浅盘”剪影：顶边宽平贴屏幕顶，两侧壁从底部贝塞尔圆角向上外展，底部平直略窄（上宽下窄对称梯形）。</summary>
        private static PathGeometry BuildNotchDropletGeometry(double w, double h)
        {
            double bottomScale = 0.76;   // 底宽 = 顶宽 × 0.76（收窄略明显）
            double ins = w * (1.0 - bottomScale) / 2.0; // 单侧收进量
            double rc = Math.Min(12.0, h * 0.5);        // 底角圆角半径（加大、更明显）
            double L = Math.Sqrt(ins * ins + h * h);
            double cy = h - rc;

            // 底角圆心 x（圆心在 y=h-rc 且到侧壁距离=rc 的内侧解，左右镜像对称）
            double cxR = (w - ins) - rc * ((L - ins) / h);
            double cxL = ins + rc * ((L - ins) / h);

            // 圆角在侧壁上的切点 = 圆心到侧壁的垂足（右侧）
            double ux = ins / L, uy = -h / L;           // 右壁方向：底角→顶角
            var BR = new Point(w - ins, h);
            double t = (cxR - BR.X) * ux + (cy - BR.Y) * uy;
            var pr = new Point(BR.X + ux * t, BR.Y + uy * t);

            // 左侧壁圆角切点（镜像）
            double ulx = -ins / L, uly = -h / L;        // 左壁方向：底角→顶角
            var BL = new Point(ins, h);
            double tl = (cxL - BL.X) * ulx + (cy - BL.Y) * uly;
            var pl = new Point(BL.X + ulx * tl, BL.Y + uly * tl);

            var fig = new PathFigure { StartPoint = new Point(0, 0), IsClosed = true, IsFilled = true };
            fig.Segments.Add(new LineSegment(new Point(w, 0), true));      // 顶边宽平（贴屏幕顶）
            fig.Segments.Add(new LineSegment(pr, true));                   // 右侧壁
            AppendArcBezier(fig, pr, new Point(cxR, h), cxR, cy, rc);      // 右下圆角（贝塞尔）
            fig.Segments.Add(new LineSegment(new Point(cxL, h), true));    // 底部平直段
            AppendArcBezier(fig, new Point(cxL, h), pl, cxL, cy, rc);      // 左下圆角（贝塞尔，镜像对称）
            fig.Segments.Add(new LineSegment(new Point(0, 0), true));      // 左侧壁

            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            geo.Freeze();
            return geo;
        }

        /// <summary>把一段半径 rr、圆心 (cxx,cyy) 的圆角圆弧 p1→p2 用一条三次贝塞尔近似（始终取短弧，左右镜像自然对称）。</summary>
        private static void AppendArcBezier(PathFigure fig, Point p1, Point p2, double cxx, double cyy, double rr)
        {
            double a1 = Math.Atan2(p1.Y - cyy, p1.X - cxx);
            double a2 = Math.Atan2(p2.Y - cyy, p2.X - cxx);
            double sweep = a2 - a1;
            while (sweep > Math.PI) sweep -= 2 * Math.PI;
            while (sweep < -Math.PI) sweep += 2 * Math.PI;
            int sign = sweep >= 0 ? 1 : -1;
            double theta = Math.Abs(sweep);
            double k = (4.0 / 3.0) * Math.Tan(theta / 4.0);
            double dist = k * rr;
            // 逆时针单位切向量
            double tx1 = -(p1.Y - cyy) / rr, ty1 = (p1.X - cxx) / rr;
            double tx2 = -(p2.Y - cyy) / rr, ty2 = (p2.X - cxx) / rr;
            var cp1 = new Point(p1.X + sign * tx1 * dist, p1.Y + sign * ty1 * dist);
            var cp2 = new Point(p2.X - sign * tx2 * dist, p2.Y - sign * ty2 * dist);
            fig.Segments.Add(new BezierSegment(cp1, cp2, p2, true));
        }

        private void SubNotifIsland_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateSubNotifGeometry(e.NewSize.Width, e.NewSize.Height);
        private void UpdateSubNotifGeometry(double w, double h)
        {
            if (w <= 0 || h <= 0) return;
            SubNotifShapePath.Data = BuildSquircleGeometry(w, h, _activeExpandedCapsule == CapsuleId.SubNotif ? 24.0 : (h / 2.0));
        }

        private void SubHwIsland_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateSubHwGeometry(e.NewSize.Width, e.NewSize.Height);
        private void UpdateSubHwGeometry(double w, double h)
        {
            if (w <= 0 || h <= 0) return;
            SubHwShapePath.Data = BuildSquircleGeometry(w, h, _activeExpandedCapsule == CapsuleId.SubHw ? 24.0 : (h / 2.0));
        }

        private void SubMediaIsland_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateSubMediaGeometry(e.NewSize.Width, e.NewSize.Height);
        private void UpdateSubMediaGeometry(double w, double h)
        {
            if (w <= 0 || h <= 0) return;
            SubMediaShapePath.Data = BuildSquircleGeometry(w, h, _activeExpandedCapsule == CapsuleId.SubMedia ? 24.0 : (h / 2.0));
        }

        private void SubNoteIsland_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateSubNoteGeometry(e.NewSize.Width, e.NewSize.Height);
        private void UpdateSubNoteGeometry(double w, double h)
        {
            if (w <= 0 || h <= 0) return;
            SubNoteShapePath.Data = BuildSquircleGeometry(w, h, _activeExpandedCapsule == CapsuleId.SubNote ? 24.0 : (h / 2.0));
        }

        /// <summary>专辑封面使用与胶囊同源的三次贝塞尔 Squircle 裁剪，四角更圆润连续。</summary>
        private void CoverShape_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not Border b || b.ActualWidth <= 0 || b.ActualHeight <= 0) return;
            double r = Math.Min(b.ActualWidth, b.ActualHeight) * 0.38;
            b.Clip = BuildSquircleGeometry(b.ActualWidth, b.ActualHeight, r);
        }

        private static PathGeometry BuildSquircleGeometry(double w, double h, double r)
        {
            r = Math.Min(r, Math.Min(w, h) / 2.0);
            double c = 0.55228475;
            var fig = new PathFigure { StartPoint = new Point(r, 0), IsClosed = true, IsFilled = true };
            fig.Segments.Add(new LineSegment(new Point(w - r, 0), true));
            fig.Segments.Add(new BezierSegment(new Point(w - r + r * c, 0), new Point(w, r - r * c), new Point(w, r), true));
            fig.Segments.Add(new LineSegment(new Point(w, h - r), true));
            fig.Segments.Add(new BezierSegment(new Point(w, h - r + r * c), new Point(w - r + r * c, h), new Point(w - r, h), true));
            fig.Segments.Add(new LineSegment(new Point(r, h), true));
            fig.Segments.Add(new BezierSegment(new Point(r - r * c, h), new Point(0, h - r + r * c), new Point(0, h - r), true));
            fig.Segments.Add(new LineSegment(new Point(0, r), true));
            fig.Segments.Add(new BezierSegment(new Point(0, r - r * c), new Point(r - r * c, 0), new Point(r, 0), true));
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            geo.Freeze();
            return geo;
        }
        #endregion

        #region 8. 主胶囊居中对称排布
        public void RebuildCapsuleLayout()
        {
            SwitchMainActiveModule(ConfiguredMainModule);

            SubMediaIsland.Visibility = (IsMediaEnabled && ConfiguredMainModule != IslandModule.Media) ? Visibility.Visible : Visibility.Collapsed;
            SubNotifIsland.Visibility = (IsNotifEnabled && ConfiguredMainModule != IslandModule.Notification) ? Visibility.Visible : Visibility.Collapsed;
            SubHwIsland.Visibility = (IsHwEnabled && ConfiguredMainModule != IslandModule.Hardware) ? Visibility.Visible : Visibility.Collapsed;
            SubNoteIsland.Visibility = (IsNoteEnabled && ConfiguredMainModule != IslandModule.Note) ? Visibility.Visible : Visibility.Collapsed;

            MainScrollPool.Clear();
            MainScrollPool.Add(ConfiguredMainModule);

            if (SubMediaIsland.Visibility != Visibility.Visible && !MainScrollPool.Contains(IslandModule.Media))
                MainScrollPool.Add(IslandModule.Media);

            if (SubNotifIsland.Visibility != Visibility.Visible && !MainScrollPool.Contains(IslandModule.Notification))
                MainScrollPool.Add(IslandModule.Notification);

            if (SubHwIsland.Visibility != Visibility.Visible && !MainScrollPool.Contains(IslandModule.Hardware))
                MainScrollPool.Add(IslandModule.Hardware);

            if (SubNoteIsland.Visibility != Visibility.Visible && !MainScrollPool.Contains(IslandModule.Note))
                MainScrollPool.Add(IslandModule.Note);

            ArrangeCapsulesSymmetrically();
            RecalculateMainCompactWidth();
            UpdateNoteContentViews();
            SaveConfiguration();
        }

        private void ArrangeCapsulesSymmetrically()
        {
            var visibleSubs = new List<UIElement>();
            if (SubNotifIsland.Visibility == Visibility.Visible) visibleSubs.Add(SubNotifIsland);
            if (SubMediaIsland.Visibility == Visibility.Visible) visibleSubs.Add(SubMediaIsland);
            if (SubHwIsland.Visibility == Visibility.Visible) visibleSubs.Add(SubHwIsland);
            if (SubNoteIsland.Visibility == Visibility.Visible) visibleSubs.Add(SubNoteIsland);

            CapsulesContainer.Children.Clear();

            if (visibleSubs.Count == 0)
            {
                CapsulesContainer.Children.Add(MainIsland);
            }
            else if (visibleSubs.Count == 1)
            {
                CapsulesContainer.Children.Add(MainIsland);
                CapsulesContainer.Children.Add(visibleSubs[0]);
            }
            else if (visibleSubs.Count >= 2)
            {
                CapsulesContainer.Children.Add(visibleSubs[0]);
                CapsulesContainer.Children.Add(MainIsland);
                CapsulesContainer.Children.Add(visibleSubs[1]);

                for (int i = 2; i < visibleSubs.Count; i++)
                {
                    CapsulesContainer.Children.Add(visibleSubs[i]);
                }
            }

            // 主胶囊居中锚定：两端插入透明占位，由 UpdateCenterPadding 逐帧平衡左右宽度，
            // 添加/移除副胶囊或悬停展开时主胶囊始终钉在窗口正中，不再发生偏移
            if (CapsulesContainer.Children.Count > 0)
            {
                CapsulesContainer.Children.Insert(0, _padLeftElement);
                CapsulesContainer.Children.Add(_padRightElement);
            }
        }

        /// <summary>主胶囊居中锚定：按当前两侧副胶囊宽度差伸缩两端占位，使主胶囊中心恒等于整条布局中心。</summary>
        private void UpdateCenterPadding()
        {
            if (CapsulesContainer == null) return;
            double leftBlock = 0, rightBlock = 0;
            bool pastMain = false;
            foreach (object raw in CapsulesContainer.Children)
            {
                if (ReferenceEquals(raw, _padLeftElement) || ReferenceEquals(raw, _padRightElement)) continue;
                if (raw is not FrameworkElement el || el.Visibility != Visibility.Visible) continue;
                double itemWidth = el.ActualWidth > 0 ? el.ActualWidth + 10 : 0; // 含 5px 左右间距
                if (ReferenceEquals(raw, MainIsland)) pastMain = true;
                else if (pastMain) rightBlock += itemWidth;
                else leftBlock += itemWidth;
            }

            double diff = (rightBlock - leftBlock) / 2.0;
            // 取整到物理像素，避免亚像素占位引起整条文字/阴影微颤
            double leftTarget = diff > 0 ? Math.Round(diff) : 0;
            double rightTarget = diff > 0 ? 0 : Math.Round(-diff);
            if (Math.Abs(_padLeftElement.Width - leftTarget) > 0.01 || Math.Abs(_padRightElement.Width - rightTarget) > 0.01)
            {
                _padLeftElement.Width = leftTarget;
                _padRightElement.Width = rightTarget;
            }
        }
        #endregion

        #region 9. 悬停展开/收起
        private void ScheduleHoverIntent(CapsuleId capsule)
        {
            _hoverLeaveTimer.Stop();

            if (_isCircleIdle) LeaveIdle();

            if (_activeExpandedCapsule == capsule) return;

            // 从“已展开胶囊”移到另一颗：立刻止住并收起旧胶囊（消除“旧胶囊还在放大”），
            // 新胶囊只做 70ms 极短确认——真切换几乎立即展开，快速划过邻胶囊则不展开，避免“先展开再收起”的僵硬停顿
            if (_activeExpandedCapsule != CapsuleId.None)
            {
                _activeExpandedCapsule = CapsuleId.None;
                CollapseAllExcept(capsule);
                _pendingSwitchTarget = capsule;
                _switchConfirmTimer.Stop();
                _switchConfirmTimer.Start();
                return;
            }

            _pendingHoverCapsule = capsule;
            _switchConfirmTimer.Stop();        // 清除可能残留的切换确认
            _pendingSwitchTarget = CapsuleId.None;
            _hoverIntentTimer.Stop();
            _hoverIntentTimer.Start();
        }

        private void CommitPendingSwitch()
        {
            if (_pendingSwitchTarget == CapsuleId.None) return;
            CapsuleId target = _pendingSwitchTarget;
            _pendingSwitchTarget = CapsuleId.None;

            bool stillOver = target switch
            {
                CapsuleId.Main => MainIsland.IsMouseOver,
                CapsuleId.SubNotif => SubNotifIsland.IsMouseOver,
                CapsuleId.SubHw => SubHwIsland.IsMouseOver,
                CapsuleId.SubMedia => SubMediaIsland.IsMouseOver,
                CapsuleId.SubNote => SubNoteIsland.IsMouseOver,
                _ => false
            };
            if (!stillOver) return; // 已离开：保持全部收起，不误展开

            CollapseAllExcept(target);
            switch (target)
            {
                case CapsuleId.Main: ExpandMainIsland(); break;
                case CapsuleId.SubNotif: ExpandSubNotif(); break;
                case CapsuleId.SubHw: ExpandSubHw(); break;
                case CapsuleId.SubMedia: ExpandSubMedia(); break;
                case CapsuleId.SubNote: ExpandSubNote(); break;
            }
            _activeExpandedCapsule = target;
        }

        private void ScheduleHoverLeave(CapsuleId capsule)
        {
            if (_pendingSwitchTarget == capsule)
            {
                _switchConfirmTimer.Stop();
                _pendingSwitchTarget = CapsuleId.None;
            }
            if (_pendingHoverCapsule == capsule)
            {
                _hoverIntentTimer.Stop();
                _pendingHoverCapsule = CapsuleId.None;
            }
            _hoverLeaveTimer.Stop();
            _hoverLeaveTimer.Start();
        }

        private void CommitHoverExpansion()
        {
            CapsuleId target = _pendingHoverCapsule;
            _pendingHoverCapsule = CapsuleId.None;

            bool isActuallyHovered = target switch
            {
                CapsuleId.Main => MainIsland.IsMouseOver,
                CapsuleId.SubNotif => SubNotifIsland.IsMouseOver,
                CapsuleId.SubHw => SubHwIsland.IsMouseOver,
                CapsuleId.SubMedia => SubMediaIsland.IsMouseOver,
                CapsuleId.SubNote => SubNoteIsland.IsMouseOver,
                _ => false
            };

            if (!isActuallyHovered) return;

            CollapseAllExcept(target);

            switch (target)
            {
                case CapsuleId.Main: ExpandMainIsland(); break;
                case CapsuleId.SubNotif: ExpandSubNotif(); break;
                case CapsuleId.SubHw: ExpandSubHw(); break;
                case CapsuleId.SubMedia: ExpandSubMedia(); break;
                case CapsuleId.SubNote: ExpandSubNote(); break;
            }

            _activeExpandedCapsule = target;
        }

        private void CheckAndCollapseInactiveCapsules()
        {
            if (_activeExpandedCapsule == CapsuleId.None) return;

            bool isStillOver = _activeExpandedCapsule switch
            {
                CapsuleId.Main => MainIsland.IsMouseOver,
                CapsuleId.SubNotif => SubNotifIsland.IsMouseOver,
                CapsuleId.SubHw => SubHwIsland.IsMouseOver,
                CapsuleId.SubMedia => SubMediaIsland.IsMouseOver,
                CapsuleId.SubNote => SubNoteIsland.IsMouseOver,
                _ => false
            };

            if (!isStillOver) CollapseAll();
        }

        private void CollapseAllExcept(CapsuleId keep)
        {
            if (keep != CapsuleId.Main) CollapseMainIsland();
            if (keep != CapsuleId.SubNotif) CollapseSubNotif();
            if (keep != CapsuleId.SubHw) CollapseSubHw();
            if (keep != CapsuleId.SubMedia) CollapseSubMedia();
            if (keep != CapsuleId.SubNote) CollapseSubNote();
        }

        private void CollapseAll()
        {
            CollapseMainIsland();
            CollapseSubNotif();
            CollapseSubHw();
            CollapseSubMedia();
            CollapseSubNote();
            _activeExpandedCapsule = CapsuleId.None;
        }

        private void MainIsland_MouseEnter(object sender, MouseEventArgs e) => ScheduleHoverIntent(CapsuleId.Main);
        private void MainIsland_MouseLeave(object sender, MouseEventArgs e) => ScheduleHoverLeave(CapsuleId.Main);

        /// <summary>各模块展开态的尺寸。与 UpdateMainIslandGeometry 的圆角基准共用，保证展开形状一致。</summary>
        private static (double Width, double Height) GetModuleExpandedSize(IslandModule mod) => mod switch
        {
            IslandModule.Media => (440, 148),
            IslandModule.Notification => (300, 136),
            IslandModule.Hardware => (280, 140),
            IslandModule.Note => (360, 196),
            _ => (440, 148)
        };

        private void ExpandMainIsland()
        {
            _isMainExpanded = true;
            var (expW, expH) = GetModuleExpandedSize(ActiveMainModule);
            _mainWidthSpring.Target = expW;
            _mainHeightSpring.Target = expH;
            switch (ActiveMainModule)
            {
                case IslandModule.Media:
                    MainMedia_Compact.Visibility = Visibility.Collapsed;
                    MainMedia_Expanded.Visibility = Visibility.Visible;
                    break;
                case IslandModule.Notification:
                    MainNotif_Compact.Visibility = Visibility.Collapsed;
                    MainNotif_Expanded.Visibility = Visibility.Visible;
                    break;
                case IslandModule.Hardware:
                    MainHw_Compact.Visibility = Visibility.Collapsed;
                    MainHw_Expanded.Visibility = Visibility.Visible;
                    break;
                case IslandModule.Note:
                    ResetNoteEditState(); // 回到显示态，避免编辑框残留顶掉正文
                    MainNote_Compact.Visibility = Visibility.Collapsed;
                    MainNote_Expanded.Visibility = Visibility.Visible;
                    UpdateNoteContentViews();
                    break;
            }
            StartPhysicsLoop();
        }

        private void CollapseMainIsland()
        {
            _isMainExpanded = false;
            // 收起瞬间按最新曲名/状态重算宽度（修复展开期间切歌后收起宽度失效/卡旧宽度的问题）
            _currentCalculatedCompactWidth = ComputeMainCompactWidth();
            _mainWidthSpring.Target = _currentCalculatedCompactWidth;
            _mainHeightSpring.Target = 44;
            StartPhysicsLoop();

            MainMedia_Expanded.Visibility = Visibility.Collapsed;
            MainMedia_Compact.Visibility = Visibility.Visible;
            MainNotif_Expanded.Visibility = Visibility.Collapsed;
            MainNotif_Compact.Visibility = Visibility.Visible;
            MainHw_Expanded.Visibility = Visibility.Collapsed;
            MainHw_Compact.Visibility = Visibility.Visible;
            MainNote_Expanded.Visibility = Visibility.Collapsed;
            MainNote_Compact.Visibility = Visibility.Visible;
        }

        private void SubNotifIsland_MouseEnter(object sender, MouseEventArgs e) => ScheduleHoverIntent(CapsuleId.SubNotif);
        private void SubNotifIsland_MouseLeave(object sender, MouseEventArgs e) => ScheduleHoverLeave(CapsuleId.SubNotif);
        private void ExpandSubNotif()
        {
            _subNotifWidthSpring.Target = 300;
            _subNotifHeightSpring.Target = 136;
            StartPhysicsLoop();
            SubNotif_CompactClock.Visibility = Visibility.Collapsed;
            SubNotif_ExpandedView.Visibility = Visibility.Visible;
        }
        private void CollapseSubNotif()
        {
            _subNotifWidthSpring.Target = 84;
            _subNotifHeightSpring.Target = 44;
            StartPhysicsLoop();
            SubNotif_ExpandedView.Visibility = Visibility.Collapsed;
            SubNotif_CompactClock.Visibility = Visibility.Visible;
        }

        private void SubHwIsland_MouseEnter(object sender, MouseEventArgs e) => ScheduleHoverIntent(CapsuleId.SubHw);
        private void SubHwIsland_MouseLeave(object sender, MouseEventArgs e) => ScheduleHoverLeave(CapsuleId.SubHw);
        private void ExpandSubHw()
        {
            _subHwWidthSpring.Target = 280;
            _subHwHeightSpring.Target = 140;
            StartPhysicsLoop();
            SubHw_CompactView.Visibility = Visibility.Collapsed;
            SubHw_ExpandedView.Visibility = Visibility.Visible;
        }
        private void CollapseSubHw()
        {
            _subHwWidthSpring.Target = 128;
            _subHwHeightSpring.Target = 44;
            StartPhysicsLoop();
            SubHw_ExpandedView.Visibility = Visibility.Collapsed;
            SubHw_CompactView.Visibility = Visibility.Visible;
        }

        private void SubMediaIsland_MouseEnter(object sender, MouseEventArgs e) => ScheduleHoverIntent(CapsuleId.SubMedia);
        private void SubMediaIsland_MouseLeave(object sender, MouseEventArgs e) => ScheduleHoverLeave(CapsuleId.SubMedia);
        private void ExpandSubMedia()
        {
            _subMediaWidthSpring.Target = 320;
            _subMediaHeightSpring.Target = 130;
            StartPhysicsLoop();
            SubMedia_CompactView.Visibility = Visibility.Collapsed;
            SubMedia_ExpandedView.Visibility = Visibility.Visible;
        }
        private void CollapseSubMedia()
        {
            _subMediaWidthSpring.Target = 134;
            _subMediaHeightSpring.Target = 44;
            StartPhysicsLoop();
            SubMedia_ExpandedView.Visibility = Visibility.Collapsed;
            SubMedia_CompactView.Visibility = Visibility.Visible;
        }

        private void SubNoteIsland_MouseEnter(object sender, MouseEventArgs e) => ScheduleHoverIntent(CapsuleId.SubNote);
        private void SubNoteIsland_MouseLeave(object sender, MouseEventArgs e) => ScheduleHoverLeave(CapsuleId.SubNote);
        private void ExpandSubNote()
        {
            ResetNoteEditState(); // 每次展开都回到“显示态”，避免残留的编辑框顶掉正文
            _subNoteWidthSpring.Target = 330;
            _subNoteHeightSpring.Target = 182;
            StartPhysicsLoop();
            SubNote_CompactView.Visibility = Visibility.Collapsed;
            SubNote_ExpandedView.Visibility = Visibility.Visible;
            UpdateNoteContentViews();
        }
        private void CollapseSubNote()
        {
            _subNoteWidthSpring.Target = 132;
            _subNoteHeightSpring.Target = 44;
            StartPhysicsLoop();
            SubNote_ExpandedView.Visibility = Visibility.Collapsed;
            SubNote_CompactView.Visibility = Visibility.Visible;
        }
        #endregion

        #region 10. 滚轮切出与通知联动
        public void SwitchMainActiveModule(IslandModule mod)
        {
            ActiveMainModule = mod;
            MainContent_Media.Visibility = mod == IslandModule.Media ? Visibility.Visible : Visibility.Collapsed;
            MainContent_Notif.Visibility = mod == IslandModule.Notification ? Visibility.Visible : Visibility.Collapsed;
            MainContent_Hw.Visibility = mod == IslandModule.Hardware ? Visibility.Visible : Visibility.Collapsed;
            MainContent_Note.Visibility = mod == IslandModule.Note ? Visibility.Visible : Visibility.Collapsed;

            if (_activeExpandedCapsule == CapsuleId.Main)
            {
                // 展开态切模块：RecalculateMainCompactWidth 会在展开时提前 return，导致几何参考停留在上一模块。
                // 这里直接刷新为当前模块的紧凑宽度，让 UpdateMainIslandGeometry 用正确基准计算圆角（展开形状一致）；
                // 尺寸弹簧由 ExpandMainIsland 过渡到目标模块的展开尺寸。
                _currentCalculatedCompactWidth = ComputeMainCompactWidth();
                ExpandMainIsland();
            }
            else
            {
                RecalculateMainCompactWidth();
            }
        }

        private void MainIsland_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            if (MainScrollPool.Count <= 1) return;

            // 合并快速滚轮：累计方向格数，滚轮停顿约 90ms 后一次性切到目标模块。
            // 避免每个滚轮格都在尺寸弹簧动画中途反复改目标，造成展开态切换时的“尺寸/形状跳动”。
            _wheelPendingSteps += (e.Delta < 0) ? 1 : -1;
            _wheelSwitchTimer.Stop();
            _wheelSwitchTimer.Start();
        }

        private void CommitWheelSwitch()
        {
            if (_wheelPendingSteps == 0) return;
            // 手已离开主胶囊：丢弃本次待切换，避免在回缩/收起过程中强行切内容造成闪现或变形。
            if (!MainIsland.IsMouseOver) { _wheelPendingSteps = 0; return; }
            int n = MainScrollPool.Count;
            int curIdx = MainScrollPool.IndexOf(ActiveMainModule);
            if (curIdx < 0) curIdx = 0;
            int steps = _wheelPendingSteps % n;
            _wheelPendingSteps = 0;
            int finalIdx = ((curIdx + steps) % n + n) % n;
            SwitchMainActiveModule(MainScrollPool[finalIdx]);
        }

        public void TriggerNotification(string senderName, string message)
        {
            Dispatcher.Invoke(() =>
            {
                _isNotificationActive = true;
                if (_isCircleIdle) LeaveIdle();

                string banner = $"{senderName}: {message}";
                TxtSubNotifBanner.Text = banner;
                TxtMainNotifBanner.Text = banner;

                if (SubNotifIsland.Visibility == Visibility.Visible)
                {
                    SubNotif_CompactClock.Visibility = Visibility.Collapsed;
                    SubNotif_ExpandedView.Visibility = Visibility.Visible;
                    _subNotifWidthSpring.Target = 300;
                    _subNotifHeightSpring.Target = 136;
                    _activeExpandedCapsule = CapsuleId.SubNotif;
                    StartPhysicsLoop();
                }
                else
                {
                    SwitchMainActiveModule(IslandModule.Notification);
                    ExpandMainIsland();
                }

                _notifResetTimer.Stop();
                _notifResetTimer.Start();
            });
        }
        #endregion

        #region 11. 全局调色板引擎
        /// <summary>字体主色：用户设置的颜色覆盖优先；否则按主题（浅色主题用深字，暗色用白字）。</summary>
        private Brush GetThemePrimaryBrush()
        {
            if (TryGetFontOverride(out Color c)) return new SolidColorBrush(c);
            return (CurrentThemeKey == "AeroLight") ? new SolidColorBrush(Color.FromRgb(24, 24, 28)) : Brushes.White;
        }

        /// <summary>字体次色（辅助文字）：用户覆盖色时取主色的半透明版本；否则按主题。</summary>
        private Brush GetThemeSecondaryBrush()
        {
            if (TryGetFontOverride(out Color c)) return new SolidColorBrush(Color.FromArgb(165, c.R, c.G, c.B));
            return (CurrentThemeKey == "AeroLight") ? new SolidColorBrush(Color.FromRgb(100, 100, 110)) : new SolidColorBrush(Color.FromArgb(145, 255, 255, 255));
        }

        private bool TryGetFontOverride(out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(FontColorHex)) return false;
            try
            {
                var parsed = ColorConverter.ConvertFromString(FontColorHex);
                if (parsed is Color c) { color = c; return true; }
            }
            catch { }
            return false;
        }

        /// <summary>设置字体颜色（空串 = 跟随主题），即时刷新岛上所有主题文字。</summary>
        public void SetFontColor(string? hex)
        {
            FontColorHex = string.IsNullOrWhiteSpace(hex) ? "" : hex.Trim();
            SetThemeTextColors(CurrentThemeKey == "AeroLight");
            RefreshAllCurrentTextColors();
            UpdateNoteContentViews();
            SaveConfiguration();
        }

        public void SetCapsuleBaseWidth(double width)
        {
            BaseWidth = width;
            RecalculateMainCompactWidth();
            SaveConfiguration();
        }

        public void SetWaveColor(Color color)
        {
            CurrentWaveColorHex = color.ToString();
            RebuildWaveBars();
            SaveConfiguration();
        }

        private void RootCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenSettingsWindow(); // 右键背景直接打开设置
        }

        public void PositionToTopCenter()
        {
            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
            this.Top = 14;
            SaveConfiguration();
        }

        public void ApplyTheme(string themeKey)
        {
            // 只保留两套“高透”材质：未知/旧主题一律回退到高透暗色
            if (themeKey != "AeroLight" && themeKey != "AeroDark") themeKey = "AeroDark";
            CurrentThemeKey = themeKey;
            var rim = IslandShapePath.Stroke as LinearGradientBrush;

            if (themeKey == "AeroLight")
            {
                SetAllIslandBackground("#D5EFEFF5");
                SetAllDropShadows(Color.FromRgb(30, 30, 40), opacity: 0.12, blur: 10, depth: 2);
                _shadowBaseBlur = 10; _shadowBaseOpacity = 0.12; _shadowBaseDepth = 2;

                if (rim != null && rim.GradientStops.Count > 0) rim.GradientStops[0].Color = Color.FromArgb(200, 255, 255, 255);
                SetThemeTextColors(isLight: true);
            }
            else // 高透暗色（唯一暗色高透，含旧主题回退）
            {
                SetAllIslandBackground("#D5121216");
                SetAllDropShadows(Color.FromRgb(0, 0, 0), opacity: 0.18, blur: 12, depth: 2);
                _shadowBaseBlur = 12; _shadowBaseOpacity = 0.18; _shadowBaseDepth = 2;

                if (rim != null && rim.GradientStops.Count > 1)
                {
                    rim.GradientStops[0].Color = Color.FromArgb(72, 255, 255, 255);  // 顶部高光压暗
                    rim.GradientStops[1].Color = Color.FromArgb(10, 255, 255, 255);  // 底部几乎不可见
                }
                SetThemeTextColors(isLight: false);
            }

            RefreshAllCurrentTextColors();
            SaveConfiguration();
        }

        private void SetAllIslandBackground(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            IslandShapePath.Fill = brush;
            SubNotifShapePath.Fill = brush;
            SubHwShapePath.Fill = brush;
            SubMediaShapePath.Fill = brush;
            SubNoteShapePath.Fill = brush;
        }

        private void SetAllDropShadows(Color shadowColor, double opacity, double blur, double depth)
        {
            if (MainIslandShadow != null) { MainIslandShadow.Color = shadowColor; MainIslandShadow.Opacity = opacity; MainIslandShadow.BlurRadius = blur; MainIslandShadow.ShadowDepth = depth; }
            if (SubNotifShadow != null) { SubNotifShadow.Color = shadowColor; SubNotifShadow.Opacity = opacity; SubNotifShadow.BlurRadius = blur; SubNotifShadow.ShadowDepth = depth; }
            if (SubHwShadow != null) { SubHwShadow.Color = shadowColor; SubHwShadow.Opacity = opacity; SubHwShadow.BlurRadius = blur; SubHwShadow.ShadowDepth = depth; }
            if (SubMediaShadow != null) { SubMediaShadow.Color = shadowColor; SubMediaShadow.Opacity = opacity; SubMediaShadow.BlurRadius = blur; SubMediaShadow.ShadowDepth = depth; }
            if (SubNoteShadow != null) { SubNoteShadow.Color = shadowColor; SubNoteShadow.Opacity = opacity; SubNoteShadow.BlurRadius = blur; SubNoteShadow.ShadowDepth = depth; }
        }

        private void SetThemeTextColors(bool isLight)
        {
            Brush mainTextBrush = GetThemePrimaryBrush();
            Brush subTextBrush = GetThemeSecondaryBrush();
            Brush iconBrush = isLight ? new SolidColorBrush(Color.FromRgb(60, 60, 70)) : Brushes.White;

            TxtSubClockTime.Foreground = mainTextBrush;
            TxtMainClockTime.Foreground = mainTextBrush;
            TxtSubMediaTitle.Foreground = mainTextBrush;
            ExpandedTitle.Foreground = mainTextBrush;
            ExpandedArtist.Foreground = subTextBrush;

            if (IconClockSymbol != null) IconClockSymbol.Foreground = subTextBrush;
            if (IconSubClockSymbol != null) IconSubClockSymbol.Foreground = subTextBrush;

            TxtMainNotifFullTime.Foreground = mainTextBrush;
            TxtMainNotifFullDate.Foreground = subTextBrush;
            TxtSubNotifFullTime.Foreground = isLight ? new SolidColorBrush(Color.FromRgb(0, 150, 200)) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF"));

            if (TxtMainCpuLabel != null) TxtMainCpuLabel.Foreground = mainTextBrush;
            if (TxtMainGpuLabel != null) TxtMainGpuLabel.Foreground = mainTextBrush;

            TxtSubCpuLabel.Foreground = mainTextBrush;
            TxtSubGpuLabel.Foreground = mainTextBrush;
            TxtSubRamLabel.Foreground = mainTextBrush;

            // 记事备忘文字随主题走
            if (TxtNoteCompactMain != null) TxtNoteCompactMain.Foreground = mainTextBrush;
            if (TxtNoteExpandedMain != null) TxtNoteExpandedMain.Foreground = mainTextBrush;
            if (TxtSubNoteCompact != null) TxtSubNoteCompact.Foreground = mainTextBrush;
            if (TxtSubNoteExpanded != null) TxtSubNoteExpanded.Foreground = mainTextBrush;

            if (IconPrevPath != null) { IconPrevPath.Fill = iconBrush; IconPrevPath.Stroke = iconBrush; }
            if (IconNextPath != null) { IconNextPath.Fill = iconBrush; IconNextPath.Stroke = iconBrush; }
            if (IconPause != null) IconPause.Fill = iconBrush;
            if (IconPlay != null) { IconPlay.Fill = iconBrush; IconPlay.Stroke = iconBrush; }
        }

        private void RefreshAllCurrentTextColors()
        {
            if (CompactTitle != null && !string.IsNullOrEmpty(_currentSongTitle))
            {
                CompactTitle.Inlines.Clear();
                CompactTitle.Inlines.Add(new Run(_currentSongTitle) { Foreground = GetThemePrimaryBrush(), FontWeight = FontWeights.SemiBold });
                if (!string.IsNullOrEmpty(_currentSongArtist))
                {
                    CompactTitle.Inlines.Add(new Run($"  ·  {_currentSongArtist}") { Foreground = GetThemeSecondaryBrush(), FontSize = 11 });
                }
            }
        }
        #endregion

        #region 12. 媒体事件与切歌精准监听
        private async Task InitializeMediaSessionAsync()
        {
            try
            {
                _mediaManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_mediaManager == null) return;

                _mediaManager.CurrentSessionChanged += async (s, e) => await RefreshActiveMediaAsync();
                _mediaManager.SessionsChanged += async (s, e) => await RefreshActiveMediaAsync();
                await RefreshActiveMediaAsync();
            }
            catch { }
        }

        private async Task RefreshActiveMediaAsync()
        {
            try
            {
                if (_mediaManager == null) return;
                var sessions = _mediaManager.GetSessions();
                GlobalSystemMediaTransportControlsSession? playing = null;
                foreach (var s in sessions)
                {
                    try
                    {
                        if (s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        {
                            playing = s;
                            break;
                        }
                    }
                    catch { }
                }

                _activeSession = playing ?? _mediaManager.GetCurrentSession();
                BindSessionEvents(_activeSession);

                if (_activeSession != null)
                {
                    await LoadMediaPropertiesAsync();
                    UpdatePlaybackIconState();
                }
            }
            catch { }
        }

        private void BindSessionEvents(GlobalSystemMediaTransportControlsSession? session)
        {
            if (_subscribedSession != null)
            {
                try
                {
                    _subscribedSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    _subscribedSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                }
                catch { }
            }

            _subscribedSession = session;

            if (_subscribedSession != null)
            {
                try
                {
                    _subscribedSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    _subscribedSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                }
                catch { }
            }
        }

        private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            await Dispatcher.InvokeAsync(async () => await LoadMediaPropertiesAsync());
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            Dispatcher.Invoke(() =>
            {
                UpdatePlaybackIconState();
                StartPhysicsLoop();
            });
        }

        private async Task CheckTrackChangeFallbackAsync()
        {
            if (_activeSession != null)
            {
                try
                {
                    var props = await _activeSession.TryGetMediaPropertiesAsync();
                    if (props != null && !string.IsNullOrWhiteSpace(props.Title))
                    {
                        if (props.Title != _currentSongTitle)
                        {
                            await LoadMediaPropertiesAsync();
                        }
                    }
                }
                catch { }
            }
        }

        private void UpdatePlaybackIconState()
        {
            try
            {
                bool isPlaying = _activeSession?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                if (isPlaying)
                {
                    IconPlay.Visibility = Visibility.Collapsed;
                    IconPause.Visibility = Visibility.Visible;
                }
                else
                {
                    IconPlay.Visibility = Visibility.Visible;
                    IconPause.Visibility = Visibility.Collapsed;
                }
            }
            catch { }
        }

        private async Task LoadMediaPropertiesAsync()
        {
            if (_activeSession == null) return;
            try
            {
                var props = await _activeSession.TryGetMediaPropertiesAsync();
                if (props == null) return;

                string title = string.IsNullOrWhiteSpace(props.Title) ? "未知曲目" : props.Title;
                string artist = string.IsNullOrWhiteSpace(props.Artist) ? "" : props.Artist;

                _currentSongTitle = title;
                _currentSongArtist = artist;

                BitmapImage? art = null;
                if (props.Thumbnail != null)
                {
                    try
                    {
                        using var sRef = await props.Thumbnail.OpenReadAsync();
                        using var netS = sRef.AsStreamForRead();
                        using var memS = new MemoryStream();
                        await netS.CopyToAsync(memS);
                        memS.Position = 0;
                        art = new BitmapImage();
                        art.BeginInit();
                        art.CacheOption = BitmapCacheOption.OnLoad;
                        art.StreamSource = memS;
                        art.EndInit();
                        art.Freeze();
                    }
                    catch { art = null; }
                }

                Dispatcher.Invoke(() =>
                {
                    CompactTitle.Inlines.Clear();
                    CompactTitle.Inlines.Add(new Run(title) { Foreground = GetThemePrimaryBrush(), FontWeight = FontWeights.SemiBold });
                    if (!string.IsNullOrEmpty(artist) && artist != "未知艺术家")
                    {
                        CompactTitle.Inlines.Add(new Run($"  ·  {artist}") { Foreground = GetThemeSecondaryBrush(), FontSize = 11 });
                    }

                    ExpandedTitle.Text = title;
                    ExpandedTitle.Foreground = GetThemePrimaryBrush();
                    ExpandedArtist.Text = string.IsNullOrEmpty(artist) ? "未知艺术家" : artist;
                    ExpandedArtist.Foreground = GetThemeSecondaryBrush();

                    TxtSubMediaTitle.Text = title;
                    TxtSubMediaTitle.Foreground = GetThemePrimaryBrush();
                    TxtSubMediaExpandedTitle.Text = title;
                    TxtSubMediaExpandedTitle.Foreground = GetThemePrimaryBrush();
                    TxtSubMediaExpandedArtist.Text = string.IsNullOrEmpty(artist) ? "未知艺术家" : artist;
                    TxtSubMediaExpandedArtist.Foreground = GetThemeSecondaryBrush();

                    if (art != null)
                    {
                        CompactCoverImage.Source = art;
                        ExpandedCoverImage.Source = art;
                        CompactFallbackIcon.Visibility = Visibility.Collapsed;
                        ExpandedFallbackIcon.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        CompactCoverImage.Source = null;
                        ExpandedCoverImage.Source = null;
                        CompactFallbackIcon.Visibility = Visibility.Visible;
                        ExpandedFallbackIcon.Visibility = Visibility.Visible;
                    }

                    UpdatePlaybackIconState();
                    RecalculateMainCompactWidth();
                });
            }
            catch { }
        }


        private async void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_activeSession == null) return;
                var info = _activeSession.GetPlaybackInfo();
                if (info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    await _activeSession.TryPauseAsync();
                }
                else
                {
                    await _activeSession.TryPlayAsync();
                }
                UpdatePlaybackIconState();
                StartPhysicsLoop();
            }
            catch { }
        }

        private async void BtnPrev_Click(object sender, RoutedEventArgs e) { try { if (_activeSession != null) await _activeSession.TrySkipPreviousAsync(); } catch { } }
        private async void BtnNext_Click(object sender, RoutedEventArgs e) { try { if (_activeSession != null) await _activeSession.TrySkipNextAsync(); } catch { } }

        private void UpdateClockTime()
        {
            string timeStr = DateTime.Now.ToString("HH:mm");
            string fullTimeStr = DateTime.Now.ToString("HH:mm:ss");
            string dateStr = DateTime.Now.ToString("yyyy年M月d日 dddd");

            TxtSubClockTime.Text = timeStr;
            TxtSubNotifFullTime.Text = fullTimeStr;
            TxtSubNotifFullDate.Text = dateStr;

            TxtMainClockTime.Text = timeStr;
            TxtMainNotifFullTime.Text = fullTimeStr;
            TxtMainNotifFullDate.Text = dateStr;
        }

        /// <summary>定时提醒检测：到达设定分钟即触发一次通知胶囊提醒，同一天不重复；通知胶囊未添加时不打扰。</summary>
        private void EvaluateReminders()
        {
            try
            {
                if (!IsNotifEnabled) return;
                if (Reminders == null || Reminders.Count == 0) return;
                DateTime now = DateTime.Now;
                string today = now.ToString("yyyy-MM-dd");
                bool changed = false;
                foreach (var r in Reminders)
                {
                    if (r == null || !r.Enabled || r.Hour < 0 || r.Hour > 23 || r.Minute < 0 || r.Minute > 59) continue;
                    if (now.Hour == r.Hour && now.Minute == r.Minute && r.LastFiredDate != today)
                    {
                        string message = string.IsNullOrWhiteSpace(r.Message)
                            ? $"已到定时提醒时间 ({r.Hour:00}:{r.Minute:00})"
                            : r.Message.Trim();
                        TriggerNotification("⏰ 定时提醒", message);
                        r.LastFiredDate = today;
                        changed = true;
                    }
                }
                if (changed) SaveConfiguration();
            }
            catch { }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out System.Runtime.InteropServices.ComTypes.FILETIME i, out System.Runtime.InteropServices.ComTypes.FILETIME k, out System.Runtime.InteropServices.ComTypes.FILETIME u);
        private ulong _prevI = 0, _prevK = 0, _prevU = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX { public uint dwLength; public uint dwMemoryLoad; public ulong ullTotalPhys; public ulong ullAvailPhys; public ulong ullTotalPageFile; public ulong ullAvailPageFile; public ulong ullTotalVirtual; public ulong ullAvailVirtual; public ulong ullAvailExtendedVirtual; }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private void UpdateHardwareMetrics()
        {
            try
            {
                int cpu = 0;
                if (GetSystemTimes(out var idle, out var kernel, out var user))
                {
                    ulong i = ((ulong)idle.dwHighDateTime << 32) | (uint)idle.dwLowDateTime;
                    ulong k = ((ulong)kernel.dwHighDateTime << 32) | (uint)kernel.dwLowDateTime;
                    ulong u = ((ulong)user.dwHighDateTime << 32) | (uint)user.dwLowDateTime;
                    if (_prevK > 0)
                    {
                        ulong tot = (k - _prevK) + (u - _prevU);
                        if (tot > 0) cpu = (int)(((tot - (i - _prevI)) * 100) / tot);
                    }
                    _prevI = i; _prevK = k; _prevU = u;
                }
                cpu = Math.Clamp(cpu, 0, 100);

                // GPU：PDH 真实采样（GPU Engine 负载汇总，封顶 100%）；不可用/未就绪时显示 “--”，绝不返回假数据
                if (!_gpuSampler.IsEnabled) _gpuSampler.TryEnable();
                int? gpuPct = _gpuSampler.TrySamplePercent();
                string gpuText = gpuPct.HasValue ? $"{gpuPct.Value}%" : "--";
                double gpuBar = gpuPct ?? 0;

                TxtSubCpuVal.Text = $"{cpu}%";
                TxtSubGpuVal.Text = gpuText;
                TxtSubHwCpuDetail.Text = $"{cpu}%";
                TxtSubHwGpuDetail.Text = gpuText;
                ProgressSubCpu.Value = cpu;
                ProgressSubGpu.Value = gpuBar;

                TxtMainCpuVal.Text = $"{cpu}%";
                TxtMainGpuVal.Text = gpuText;
                TxtMainHwCpuBarVal.Text = $"{cpu}%";
                TxtMainHwGpuBarVal.Text = gpuText;
                ProgressMainCpu.Value = cpu;
                ProgressMainGpu.Value = gpuBar;

                var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                if (GlobalMemoryStatusEx(ref mem))
                {
                    TxtSubHwRamDetail.Text = $"{mem.dwMemoryLoad}%";
                    ProgressSubRam.Value = mem.dwMemoryLoad;
                }
            }
            catch { }
        }

        private void RootCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    if (FindParent<Button>(dep) != null) return;
                }
                try
                {
                    this.DragMove();
                    SaveConfiguration();
                }
                catch { }
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? cur = child;
            while (cur != null) { if (cur is T m) return m; cur = VisualTreeHelper.GetParent(cur); }
            return null;
        }
        #endregion
    }

    public class SpringPhysics
    {
        public double Current { get; set; }
        public double Target { get; set; }
        public double Velocity { get; set; }
        public double Stiffness { get; set; }
        public double Damping { get; set; }
        public double Mass { get; set; } = 1.0;

        public SpringPhysics(double initialValue, double stiffness = 260, double damping = 18)
        {
            Current = initialValue;
            Target = initialValue;
            Stiffness = stiffness;
            Damping = damping;
        }

        public bool Update(double dt)
        {
            if (double.IsNaN(Current) || double.IsInfinity(Current)) Current = Target;
            double disp = Current - Target;
            double f = -Stiffness * disp - Damping * Velocity;
            Velocity += (f / Mass) * dt;
            Current += Velocity * dt;

            // 主轴弹簧段：距离目标较远时全速弹簧推进，保留丝滑的加速/轻微弹性感
            if (Math.Abs(disp) >= TailDistance || Math.Abs(Velocity) >= TailSpeed)
            {
                return Math.Abs(Current - Target) < 0.2 && Math.Abs(Velocity) < 0.8;
            }

            // 干净收尾段：接近目标后切换为指数阻尼逼近（无回弹、无拖尾、无抖动尾巴）
            double ease = 1.0 - Math.Exp(-TailRate * dt);
            Current += (Target - Current) * ease;
            Velocity *= Math.Exp(-TailRate * dt * 0.5);

            if (Math.Abs(Current - Target) < 0.2 && Math.Abs(Velocity) < 0.6)
            {
                Current = Target;
                Velocity = 0;
                return true;
            }
            return false;
        }

        // 混合式参数：主段=弹簧；|位移|进入 TailDistance 且速度放缓后，切换到指数衰减收尾
        private const double TailDistance = 46.0;
        private const double TailSpeed = 540.0;
        private const double TailRate = 11.0;
    }

    /// <summary>真实 GPU 负载采样：通过 PDH 读取 “GPU Engine(*)\Utilization Percentage”，
    /// 对所有图形引擎实例求和并封顶 100% 作为整体 GPU 负载。计数器不存在/不可用时 IsEnabled=false，UI 显示 “--”，绝不返回假数据。</summary>
    internal sealed class GpuLoadSampler
    {
        private const string CounterPath = "\\GPU Engine(*)\\Utilization Percentage";
        private const uint PdhFmtDouble = 0x00000200;
        private const int PdhMoreData = unchecked((int)0x800007D2);

        private IntPtr _query = IntPtr.Zero;
        private IntPtr _counter = IntPtr.Zero;
        private bool _primed;

        public bool IsEnabled { get; private set; }

        public void TryEnable()
        {
            if (IsEnabled || _query != IntPtr.Zero) return;
            try
            {
                if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0) return;
                if (PdhAddEnglishCounter(_query, CounterPath, IntPtr.Zero, out _counter) != 0)
                {
                    PdhCloseQuery(_query);
                    _query = IntPtr.Zero;
                    return;
                }
                IsEnabled = true;
            }
            catch { }
        }

        /// <summary>采样一次整体 GPU 负载百分比；首次调用用于预热（PDH 需要两次收集才能算出差值），返回 null。</summary>
        public int? TrySamplePercent()
        {
            if (!IsEnabled || _query == IntPtr.Zero || _counter == IntPtr.Zero) return null;
            try
            {
                if (PdhCollectQueryData(_query) != 0) return null;
                if (!_primed) { _primed = true; return null; }

                uint bufferSize = 0, itemCount = 0;
                int status = PdhGetFormattedCounterArray(_counter, PdhFmtDouble, ref bufferSize, ref itemCount, IntPtr.Zero);
                if (status != 0 && status != PdhMoreData) return null;
                if (itemCount == 0 || bufferSize == 0) return null;

                IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    status = PdhGetFormattedCounterArray(_counter, PdhFmtDouble, ref bufferSize, ref itemCount, buffer);
                    if (status != 0) return null;

                    int stride = Marshal.SizeOf<PdhFmtCounterValueItem>();
                    double sum = 0;
                    for (uint i = 0; i < itemCount; i++)
                    {
                        var item = Marshal.PtrToStructure<PdhFmtCounterValueItem>(IntPtr.Add(buffer, (int)i * stride));
                        if (item.FmtValue.CStatus != 0) continue;
                        double v = item.FmtValue.DoubleValue;
                        if (double.IsNaN(v) || double.IsInfinity(v) || v < 0) continue;
                        sum += v;
                    }
                    return (int)Math.Round(Math.Min(100.0, sum));
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch { return null; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PdhFmtCounterValue
        {
            public uint CStatus;
            public double DoubleValue;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PdhFmtCounterValueItem
        {
            public IntPtr szName;
            public PdhFmtCounterValue FmtValue;
        }

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern int PdhOpenQuery(string? szDataSource, IntPtr dwUserData, out IntPtr phQuery);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern int PdhAddEnglishCounter(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

        [DllImport("pdh.dll")]
        private static extern int PdhCollectQueryData(IntPtr hQuery);

        [DllImport("pdh.dll")]
        private static extern int PdhGetFormattedCounterArray(IntPtr hCounter, uint dwFormat, ref uint lpdwBufferSize, ref uint lpdwBufferCount, IntPtr lpItemBuffer);

        [DllImport("pdh.dll")]
        private static extern int PdhCloseQuery(IntPtr hQuery);
    }
}
