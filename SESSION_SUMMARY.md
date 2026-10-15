# DynamicIslandWin — 本次开发会话总结

> 项目：WPF (.NET 8, x64) Windows 灵动岛克隆
> 路径：`D:\app\islandtop\DynamicIslandWin`
> 起始版本：**v3.19.0** → 结束版本：**v3.32.0**

---

## 一、版本演进

| 版本 | 内容 |
|------|------|
| 3.20.0 | 滚轮切换主胶囊（展开态）尺寸/形状跳动修复：滚轮合并防抖、按模块圆角基准、`e.Handled` |
| 3.21.0 | 闲置样式「无 / 小圆点 / 刘海吸附」初版（下拉） |
| 3.22.0 | 备注胶囊增强：标题+内容分字段、闲置显示标题、标题宽度自适应、展开单点编辑 |
| 3.23.0 | 刘海吸附“顶宽平底圆肚”水滴轮廓 |
| 3.24.0 | 托盘/浅盘剪影（顶宽平贴边、底窄平、底角圆润） |
| 3.25.0 | 底角改**贝塞尔圆弧**、修左右不对称 |
| 3.26.0 | 圆角加大 + 吸附/解除吸附**窗口平滑缓动** |
| 3.27.0 | 刘海吸附标注「· Beta」 |
| 3.28.0 | 备注胶囊右键改为可配置（`NoteRightAction`） |
| 3.29.0 | 备注左右键新增「打开自定义程序」（`NoteAction.OpenCustomApp` + `NoteCustomAppPath`） |
| 3.30.0 | 备注展开卡片显示修复（编辑态残留、高度裁切） |
| 3.31.0 | 客户端「检查更新」逻辑（启动静默、`version.json`、仅提示+打开下载页） |
| 3.32.0 | 内置默认更新清单地址；修整备注编辑态与高度 |

---

## 二、本次完成的功能

### 1. 滚轮切换主胶囊修复
- 展开态滚轮快速/慢速切换导致的「尺寸/形状跳动」。
- 改法：滚轮**合并防抖**（90ms 停顿后一次切换）；切模块时刷新几何参考；`e.Handled`。

### 2. 闲置样式（可选）
- 设置「基本偏好」顶部：**无 / 灵动小圆点 / 刘海吸附（Beta）** 三选一。
- `IdleStyle` 枚举 + 配置迁移（旧 `IsIdleCircleEnabled` 兼容）。
- 刘海吸附：托盘形剪影，**贴屏幕顶边、贝塞尔圆角、窗口缓动**；悬停/通知解除吸附恢复原位。

### 3. 备注（备忘录）胶囊
- **标题 + 内容** 分字段（`NoteTitle` / `NoteText`）。
- 闲置/紧凑态只显示标题，宽度自适应。
- 展开态**单点进入内联编辑**（标题 Enter 跳内容、内容 Enter 保存）。
- 左右键动作：展开/收起、打开设置、**打开自定义程序**、无动作（右键可配置）。
- 修复展开卡片内容不显示（编辑态残留顶掉正文 + 高度不足裁切）。

### 4. 检查更新（客户端）
- `UpdateChecker.cs`：启动 2.5s 后静默拉取 `version.json`（`{version, note, url}`），8s 超时，失败静默。
- 版本较新且未提示过（`LastNotifiedUpdateVersion`）→ 弹「发现新版本…是否打开下载页？」。
- 内置默认地址：`https://github.com/AAAnYuan/DynamicIsland/releases/latest/download/version.json`（设置里可改）。

---

## 三、GitHub 与分发

- 仓库：`https://github.com/AAAnYuan/DynamicIsland`（public，main 分支）。
- 已推送代码（3 次提交：初始 + `publish-github.ps1` + 默认更新地址）。
- `.gitignore`：排除 `bin/obj/dist/config.json/*.log/_repro/`。
- `publish-github.ps1`：生成 `dist\version.json`；完整模式建 GitHub Release 并上传 zip + version.json（令牌走 `-Token`/`$env:GH_TOKEN`，不落盘）。
- Release `v3.31.0` 已创建（公开），但 **资产（zip/version.json）尚未上传**（本机到 `uploads.github.com` 被代理拦截）——**待用户自行拖拽上传**。

### 待办
1. 用户在 GitHub Releases 上传 zip + version.json（对外发布版本自定）。
2. 之后每次出新版本并本地打包后，重复「拖拽/或 `publish-github.ps1`」发布。
3. 老版本客户端（≤3.31.0）启动即会收到更新提示。

---

## 四、当前产物

- 源码/最新版：v3.32.0（`dotnet build` 0 警告 0 错误）。
- 可分发包：`dist\DynamicIslandWin-win-x64.zip`（约 6.7 MB，framework-dependent，含 `plugins\`）。
- 更新清单：`dist\version.json`（version=3.32.0 + 下载直链）。
- 历史备份：`dist\archive\`（v3.21.0 … v3.31.0）。

---

## 五、关键文件

- `MainWindow.xaml(.cs)` — 主逻辑、闲置状态机、刘海几何、备注编辑、检查更新。
- `SettingsWindow.xaml(.cs)` — 设置界面（闲置样式、备注标题/内容、右键动作、检查更新）。
- `UpdateChecker.cs` — 检查更新客户端。
- `publish-github.ps1` — GitHub Releases 发布脚本。
- `PROJECT_HANDOFF.md` — 项目交接文档。
- `.gitignore` — 忽略规则。

## 2026-10-14 审查修复落地（v3.33.0）
- 已应用媒体异步刷新、配置持久化、通知去重、插件解析、更新校验及退出清理修复。
- 原项目构建通过：0 警告、0 错误；修复副本的 24 项回归检查通过。
- 已保留 v3.32.0 分发包备份，生成 v3.33.0 分发包并启动 dist 中的程序。
- 已清理 bin/obj 和本次工作区临时副本；源码、用户配置、插件、历史备份保留。
- 尚未发布 GitHub；dist/version.json 是待发布清单。真实媒体、多屏动画仍需交互验收。

