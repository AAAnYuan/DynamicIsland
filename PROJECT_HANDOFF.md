# DynamicIslandWin — 项目交接文档

> 用途：跨会话延续开发时，只需在新会话中说「读取并继续 DynamicIslandWin」，
> 即以此文档为上下文入口。每次有重大进展，请同步更新本文档与版本号。

- 路径：`D:\app\islandtop\DynamicIslandWin`
- 类型：WPF (.NET 8) Windows 灵动岛克隆，x64
- 当前版本：**3.31.0**（见 `DynamicIslandWin.csproj` `<Version>`）
- 目标框架：`net8.0-windows10.0.22621.0`，`PlatformTarget=x64`，`Nullable=enable`，
  `ImplicitUsings=enable`，`UseWPF=true`
- 开发工具：VS Code / `dotnet build`

---

## ⚠️ 铁律（用户强制要求）
1. **每次功能更新必须升版本**：只改 `DynamicIslandWin.csproj` 的 `<Version>`，程序集/文件版本
   由 `0` 自动跟随生成（当前 `<AssemblyVersion>`/`<FileVersion>` 为 `3.19.0.0`）。
   设置窗口角落的版本标签也据此自动更新。**绝不可跳过。**
2. 编译须 0 警告 0 错误；改动后用 `dotnet build` 验证，必要时 `--settings` 冒烟。
3. 构建前若报「文件被占用」，先杀掉 `DynamicIslandWin` 进程再构建。

---

## 功能清单（已实现）
- **主胶囊**：顶中置，无边框、透明分层窗口、Topmost。中心居中通过透明
  `_padLeftElement`/`_padRightElement` + `UpdateCenterPadding()`（取整到整像素）。
- **悬停/切换**：状态机 `_hoverIntentTimer`(55ms) / `_hoverLeaveTimer`(55ms) /
  `_switchConfirmTimer`(70ms)，配合 `ScheduleHoverIntent`/`ScheduleHoverLeave`/
  `CommitHoverExpansion`/`CommitPendingSwitch`。切换时「先立即收起旧胶囊 + 70ms 确认再展开新」。
- **动画**：`SpringPhysics`（半隐式欧拉）+ **混合尾部**（远时用弹簧，接近目标后转指数衰减收尾）。
  参数 `TailDistance=46`、`TailSpeed=540`、`TailRate=11`。前段保持弹性、收尾干净利落。
- **主题**：只保留 `AeroLight` / `AeroDark`（默认 `AeroDark`）。
  未知/旧键一律归一为 `AeroDark`。每主题捕获 `_shadowBaseBlur/_shadowBaseOpacity/_shadowBaseDepth`。
- **子胶囊阴影**：`ApplySubShadow` 按宽度插值，避免展开时的黑色「雾」。
- **媒体胶囊**：`Windows.Media.Control`(GSMTC)。封面(封套裁剪) + 上一首/播放/下一首 +
  **♥ 点赞**。`GetPlaybackInfo()`（可空 `PlaybackRate`）、`SourceAppUserModelId`、
  `GetTimelineProperties()`、`TryChangePlaybackPositionAsync(long ticks)`。
  *进度条与时间轴引擎已整体移除（尝试失败）。*
- **系统通知胶囊**：`Windows.UI.Notifications.Management.UserNotificationListener`，
  `GetNotificationsAsync(NotificationKinds.Toast)`、`NotificationBinding.GetTextElements()`
  → `IReadOnlyList<AdaptiveNotificationText>`、`AppInfo.DisplayInfo.DisplayName`。
  事件订阅抛 `COMException 0x80070490` → 改为 **2s 轮询** `PollSystemNotifications`。
  仅展示**新**通知（初始化时标记已有为已见）；受系统「通知访问权限」限制。
- **硬件胶囊**：PDH GPU 采样 + `GetSystemTimes`/`GlobalMemoryStatusEx`。
- **备注胶囊** / 提醒构建器（`Reminders`）。
- **点赞 = 鼠标宏**：`TryParseLikeCombo` 解析组合键 → `keybd_event`，无 `RegisterHotKey`。
  全局热键注册已移除。点击 ♥ 切换 `_heartLiked`（保持红色）并对前台播放器发送宏；
  前台窗口由 `GetForegroundWindow`/`SetForegroundWindow`/`GetWindowThreadProcessId` 追踪。
  配置键：`LikeShortcutEnabled`（默认 false）、`LikeShortcutKey`（默认 "F5"）。
  *旧键 `LikeTriggerKey` 已废弃，被忽略。*
- **插件架构**：`plugins\<id>\plugin.json`（id/kind: media|notification|hardware|note/name/icon/description）。
  `PluginCatalog`（`EnsureDefaults`/`Scan`/`TryParseKind`/`KindOf`）内置 4 个始终存在；
  引擎仍为**固定槽位**（每种 kind 一个岛），自定义插件可在每 kind 下切换激活插件
  （`ActivePluginByKind`/`ActivatePlugin`/`ResetActivePlugin`/`RefreshPluginsFromDisk`）。
  扫描跳过 `_`/`.` 开头目录与非法 manifest。
- **设置窗口**：失焦自动关，由 `_modalDialogOpen` 守卫 + `SafeClose()` 处理竞态；
  滑块 24ms 合并（`_sliderCommit`/`ApplyPendingSliderValues`）。
- **闲置样式（v3.21.0）**：`IdleStyle` 配置（`None=无变化` / `Circle=灵动小圆点`(现有) / `Notch=刘海吸附`(新)）。
  - 旧配置无 `IdleStyle` 键时，用 `IsIdleCircleEnabled` 迁移推断（true→Circle，false→None）。
  - **Notch（刘海吸附）**：闲置时主胶囊收缩为**固定药丸形**（宽 150×高 44、全圆角、隐藏内容与各副胶囊），并把整个透明窗口上移使药丸
    **贴合桌面顶部边缘**（顶部留 4px 极细间隙；窗口内主胶囊顶部 Margin=16，故窗口 Top 上移到 `4-16=-12`）。
    悬停/有内容时解除吸附、恢复到进入闲置前的窗口位置并展开。`Window_LocationChanged` 在吸附时不落盘，避免把吸附位写成用户位置。
  - 设置窗口「⚙️ 基本偏好」标签内新增「闲置样式」三个**单选菜单项**（无 / 小圆点 / 刘海吸附），置于**基本偏好最顶部**，
    替换旧的「空闲小圆点」勾选框；打开设置窗口**默认显示「基本偏好」标签**，便于直接找到。
- **备忘录胶囊增强（v3.22.0）**：新增独立的**标题(`NoteTitle`)+内容(`NoteText`)**字段；闲置/紧凑态**显示标题**，宽度随标题**自适应**；
  展开态显示「标题+内容」，**单点标题/正文进入内联编辑**（标题 Enter 跳到内容框、内容 Enter 保存，点其它处不保存回到显示态）；
  设置窗口新增「标题」输入框。左键:展开态=编辑，紧凑态=常规动作。
- **刘海吸附水滴感（v3.22.0–v3.26.0，迭代中）**：`BuildNotchDropletGeometry` 画「托盘/浅盘」剪影（顶宽平贴顶间距0、侧壁外展、底窄平、底角**贝塞尔圆弧**，
  左右镜像对称；圆角 rc=min(12,h*0.5)、底宽/顶宽 0.76）。**吸附/解除吸附用窗口缓动**（`_dockAnimTimer` 每帧 30% 逼近目标 Top），避免瞬间跳顶。
  *若仍不满意，按其图调 `bottomScale`/`rc`/`NotchIdleHeight` 即可。* 设置里该选项已标注「· Beta」（v3.27.0）。
- **闲置判定按主模块区分（v3.22.0）**：媒体=无播放即闲置；时间/通知=除收到通知都闲置；硬件=**取消常驻判定，始终可闲置**；备注=常态即紧凑标题（不进全局小圆点/刘海）。
- **备忘录左右键打开自定义程序（v3.29.0）**：`NoteAction.OpenCustomApp` + `NoteCustomAppPath`，设置里可选并填路径/浏览。
- **检查更新（v3.31.0，客户端逻辑）**：启动静默拉取 `UpdateManifestUrl` 的 `version.json`（`{version,note,url}`，`UpdateChecker.cs`），
  版本较新且未提示过（`LastNotifiedUpdateVersion`）→ 弹“发现新版本…是否打开下载页？”（仅提示+打开浏览器，不做自动替换）。
  设置「基本偏好」底部：开关 `IsStartupCheckUpdatesEnabled` + 清单地址输入。更新源地址待用户提供后再填写并对外发布。
- **GitHub 发布脚本（publish-github.ps1，UTF-8 BOM 以免 PS5.1 中文乱码）**：自动生成 `dist\version.json`；完整模式建 GitHub Release 并上传
  zip + version.json（令牌取 `-Token` 或 `$env:GH_TOKEN`，仓库取 `-Owner/-Repo` 或 `$env:GH_REPO`，绝不写盘）。应用内清单地址应填
  `https://github.com/<owner>/<repo>/releases/latest/download/version.json`（latest 直链指向最新版 asset）。
  注意：仓库尚未创建/推送（等用户用 VS Code 发布后再执行脚本）。
- **版本标签 + `--settings` 诊断参数**、单实例 mutex。
- **滚轮切换主胶囊（展开态）修复（v3.20.0）**：原实现每个滚轮格都在尺寸弹簧动画中途重复改目标（各模块展开宽高不同：
  媒体 440×148 / 时钟 300×136 / 硬件 280×140 / 备注 320×170；弹簧欠阻尼会过冲），且圆角计算沿用上一模块紧凑宽度/固定 460，
  导致展开态下切模块出现「尺寸/形状跳动变形」。改为：①滚轮**合并防抖**（连续滚动累计方向，停顿 90ms 后一次切到目标模块）；
  ②切模块时先刷新 `_currentCalculatedCompactWidth` 为当前模块紧凑宽度，`UpdateMainIslandGeometry` 改用**该模块展开宽度**作圆角基准，
  使各模块展开态收敛到同一矩形圆角；③补 `e.Handled`；④`MorphToCircle` 进圆点时**立即隐藏主内容与各副胶囊**，
  杜绝「回缩小圆点后紧凑单行内容残影/重影」（物理循环可能在收敛首帧即停、依赖第二帧的 Collapse 未必执行）；⑤`CommitWheelSwitch`
  校验 `MainIsland.IsMouseOver`，手离开则丢弃待切换，避免回缩/收起过程中切内容导致闪现或变形。保留各模块展开尺寸。

---

## 关键文件
- `MainWindow.xaml.cs`（约 2500 行）— 主逻辑。
- `MainWindow.xaml` — 岛布局与主题样式。
- `SettingsWindow.xaml(.cs)` — 设置界面。
- `App.xaml.cs` — `AppLog.Info`/`Write`、单实例、`--settings`。
- `PluginCatalog.cs` — `PluginManifest`/`PluginCatalog`。
- `plugins\` — `media/notification/hardware/note` + `_TEMPLATE`。
- `publish-release.ps1` — 发布脚本。

---

## 配置键（`config.json` 运行时生成）
默认值来源在 `MainWindow.xaml.cs`（约 86–190 行）：
- `WindowLeft/WindowTop/BaseWidth/CapsuleScale/IsAutoWidthEnabled/IdleStyle`（3.21.0 起闲置样式：None/Circle/Notch；`IsIdleCircleEnabled` 仅旧版迁移兼容）
- `CurrentThemeKey`（默认 `"AeroDark"`）
- `WaveColorHex`（默认 `#00E5FF`）/`WaveBarCount`(5)/`WaveMaxHeight`(24)
- `CustomAppPath` / `MediaCustomAppPath` / `NotifCustomAppPath` / `HwCustomAppPath` / `NoteCustomAppPath`
- `IsStartupCheckUpdatesEnabled` / `UpdateManifestUrl` / `LastNotifiedUpdateVersion`
- `ConfiguredMainModule`(0)/`ActiveMainModule`(默认 `IslandModule.Media`)
- `IsMediaEnabled/IsNotifEnabled/IsHwEnabled/IsNoteEnabled`
- `NoteText`/`NoteTitle`（v3.22.0 起新增）/`Reminders`
- `MediaLeftAction(0)/MediaRightAction(5)/NotifLeftAction(0)/NotifRightAction(4)/HwLeftAction(0)/HwRightAction(5)/NoteLeftAction(0)/NoteRightAction(4)`
- `FontColorHex`（默认 ""）
- `ActivePluginByKind`（`Dictionary<string,string>`）
- `LikeShortcutEnabled`(默认 false)/`LikeShortcutKey`(默认 "F5")

---

## 构建 / 发布
- 构建：`dotnet build`
- 发布：`dotnet publish -c Release -r win-x64 --self-contained false` 到 `dist\DynamicIslandWin` + zip。
- 自包含需要 NuGet 下载（当前网络不可达，故仅 framework-dependent）。
- **自动发布规则（用户要求，务必遵守）**：每次功能/修复更新后**自动打包**（framework-dependent 到 `dist\DynamicIslandWin` + zip），**无需再向用户确认/汇报**；**打包前先把当前 `dist\DynamicIslandWin-win-x64.zip` 备份为** `dist\archive\DynamicIslandWin-v{版本号}-win-x64.zip`，**保留上一个版本副本**，便于回滚。
- `publish-release.ps1` 支持 `-SingleFile`。默认是自包含（需联网），当前交付走 framework-dependent（离线）。
- 发布需把 `plugins\` 文件夹随 exe 一起分发；`config.json`/`error.log` 写 exe 所在目录（建议可写位置）。
- 当前交付：`dist\DynamicIslandWin-win-x64.zip`（约 6.4 MB，**v3.31.0**，framework-dependent；含 `plugins\` 随包分发）。
- 历史版本备份：`dist\archive\`（自动发布保留上一版：v3.21.0 … v3.25.0）。

---

## 当前待办 / 未决问题
1. **朋友的媒体胶囊不出声（诊断问题，未实现修复）**：用户自己运行正常，朋友那边音乐胶囊
   显示不出当前播放。已给出诊断性解释（见下）。**已提议**在媒体胶囊上加一个
   「未识别到播放器（请确认播放器支持系统媒体卡片）」提示，并在 `error.log` 里输出
   SMTC 访问状态（`RequestAsync` 结果 / 会话数），供定位。**尚未实现；若用户确认要做，
   请实现并升版本。**

   诊断要点（朋友侧可能原因）：
   - 播放器未注册到系统媒体传输（SMTC）/不可行媒体会话。
   - 播放状态为暂停/停止（`PlaybackRate` 为 0 或 `GetPlaybackInfo()` 为空）。
   - 同时存在多个媒体会话，取的 session 不是当前那个。
   - 系统版本低于 Windows 10 1803（旧系统没 SMTC）。
   - 浏览器/网页播放时未把媒体接入系统媒体卡片（需站点/浏览器支持）。
   - 系统「媒体」后台设置未开启。

2. 任何新功能 / 修复一律遵循版本升级铁律。

---

## 想在新会话继续时，怎么开口
直接说：「读取并继续 DynamicIslandWin，参考 PROJECT_HANDOFF.md」。
如果我在这个会话里做了非代码的内容，也会顺手更新本文档。
