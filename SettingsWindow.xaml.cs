using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DynamicIslandWin
{
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "DynamicIslandWin";
        private bool _isInitializing = true;
        private bool _modalDialogOpen = false; // 模态系统对话框（文件选择等）弹出期间禁止“失焦即关”，避免窗口在对话框生命周期中被销毁而崩溃

        // 滑块丝滑：高频 ValueChanged 先暂存，24ms 合并提交一次，避免拖动时反复重建/保存造成的僵硬卡顿
        private readonly DispatcherTimer _sliderCommit = new() { Interval = TimeSpan.FromMilliseconds(24) };
        private double? _pendScale;
        private double? _pendWidth;
        private double? _pendWaveCount;
        private double? _pendWaveHeight;

        public SettingsWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _sliderCommit.Tick += (s, e) => { _sliderCommit.Stop(); ApplyPendingSliderValues(); };

            // 1. 初始化尺寸与自适应开关
            SliderCapsuleScale.Value = _mainWindow.CapsuleScale;
            TxtCapsuleScaleVal.Text = $"{(int)(_mainWindow.CapsuleScale * 100)} %";

            SliderCapsuleWidth.Value = _mainWindow.BaseWidth;
            TxtCapsuleWidthVal.Text = $"{(int)_mainWindow.BaseWidth} px";

            SliderWaveCount.Value = _mainWindow.WaveBarCount;
            TxtWaveCountVal.Text = $"{_mainWindow.WaveBarCount} 根";

            SliderWaveMaxHeight.Value = _mainWindow.WaveMaxHeight;
            TxtWaveHeightVal.Text = $"{(int)_mainWindow.WaveMaxHeight} px";

            // 各胶囊专属自定义程序路径
            TxtMediaCustomApp.Text = _mainWindow.MediaCustomAppPath;
            TxtNotifCustomApp.Text = _mainWindow.NotifCustomAppPath;
            TxtHwCustomApp.Text = _mainWindow.HwCustomAppPath;
            TxtNoteCustomApp.Text = _mainWindow.NoteCustomAppPath;

            ChkAutoWidth.IsChecked = _mainWindow.IsAutoWidthEnabled;
            SyncIdleStyleRadio();
            ChkStartup.IsChecked = IsStartupEnabled();
            ChkAutoCheckUpdate.IsChecked = _mainWindow.IsStartupCheckUpdatesEnabled;
            TxtUpdateManifestUrl.Text = _mainWindow.UpdateManifestUrl;

            // 2. 备忘录文字初始化（卡片仅在“已添加”时显示，状态由 RefreshCapsuleState 统一管理）
            TxtNoteTitle.Text = _mainWindow.NoteTitle;
            TxtNoteContent.Text = _mainWindow.NoteText;

            // ♥ 喜欢快捷键设置
            ChkLikeShortcut.IsChecked = _mainWindow.LikeShortcutEnabled;
            TxtLikeKey.Text = _mainWindow.LikeShortcutKey;

            SyncMainRadioSelection();

            // 3. 专属绑定各胶囊动作下拉菜单
            PopulateMediaActionCombo(CmbMediaLeftAction, _mainWindow.MediaLeftAction);
            PopulateMediaActionCombo(CmbMediaRightAction, _mainWindow.MediaRightAction);

            PopulateNotifActionCombo(CmbNotifLeftAction, _mainWindow.NotifLeftAction);
            PopulateNotifActionCombo(CmbNotifRightAction, _mainWindow.NotifRightAction);

            PopulateHwActionCombo(CmbHwLeftAction, _mainWindow.HwLeftAction);
            PopulateHwActionCombo(CmbHwRightAction, _mainWindow.HwRightAction);

            PopulateNoteActionCombo(CmbNoteLeftAction, _mainWindow.NoteLeftAction);
            PopulateNoteActionCombo(CmbNoteRightAction, _mainWindow.NoteRightAction);

            _isInitializing = false;
            UpdateMainCapsuleRightLock(); // 主胶囊右键固定为打开设置
            RefreshCustomAppRows();       // 依据当前左右键动作，展开/收起各胶囊的程序选择行
            RefreshCapsuleState();        // 依据“已添加/未添加”显示卡片或预设库行
            RebuildRemindersList();       // 重建通知胶囊的定时提醒行
            RefreshSelectionIndicators(); // 主题/声波色/字体色 当前选中项高亮
            InitVersionLabel();
        }

        /// <summary>版本角标：直接从程序集版本读取（csproj Version=3.7.0 → 显示 v3.7）。</summary>
        private void InitVersionLabel()
        {
            try
            {
                var v = typeof(SettingsWindow).Assembly.GetName().Version;
                VersionLabel.Text = v != null ? $"DynamicIslandWin v{v.Major}.{v.Minor}" : "DynamicIslandWin";
            }
            catch { }
        }

        #region 1. 专属动作下拉菜单填充
        private void PopulateMediaActionCombo(ComboBox combo, MediaAction selectedAction)
        {
            combo.Items.Clear();
            combo.Items.Add(new ComboBoxItem { Content = "🪟 展开大卡片 / 收起", Tag = MediaAction.ToggleExpand });
            combo.Items.Add(new ComboBoxItem { Content = "▶/⏸ 播放 / 暂停", Tag = MediaAction.TogglePlayPause });
            combo.Items.Add(new ComboBoxItem { Content = "⏭ 切下一首", Tag = MediaAction.NextTrack });
            combo.Items.Add(new ComboBoxItem { Content = "⏮ 切上一首", Tag = MediaAction.PrevTrack });
            combo.Items.Add(new ComboBoxItem { Content = "🎧 打开当前音乐软件", Tag = MediaAction.OpenMusicApp });
            combo.Items.Add(new ComboBoxItem { Content = "🚀 打开自定义程序", Tag = MediaAction.OpenCustomApp });
            combo.Items.Add(new ComboBoxItem { Content = "⚙️ 灵动岛设置", Tag = MediaAction.OpenSettings });
            combo.Items.Add(new ComboBoxItem { Content = "🚫 无动作", Tag = MediaAction.None });

            SelectComboItemByTag(combo, selectedAction);
        }

        private void PopulateNotifActionCombo(ComboBox combo, NotifAction selectedAction)
        {
            combo.Items.Clear();
            combo.Items.Add(new ComboBoxItem { Content = "🪟 展开日程与通知", Tag = NotifAction.ToggleExpand });
            combo.Items.Add(new ComboBoxItem { Content = "⏰ 打开日期与时间设置", Tag = NotifAction.OpenWindowsTimeSettings });
            combo.Items.Add(new ComboBoxItem { Content = "🧹 清除通知 / 标记已读", Tag = NotifAction.ClearNotification });
            combo.Items.Add(new ComboBoxItem { Content = "🚀 打开自定义程序", Tag = NotifAction.OpenCustomApp });
            combo.Items.Add(new ComboBoxItem { Content = "⚙️ 灵动岛设置", Tag = NotifAction.OpenSettings });
            combo.Items.Add(new ComboBoxItem { Content = "🚫 无动作", Tag = NotifAction.None });

            SelectComboItemByTag(combo, selectedAction);
        }

        private void PopulateHwActionCombo(ComboBox combo, HwAction selectedAction)
        {
            combo.Items.Clear();
            combo.Items.Add(new ComboBoxItem { Content = "⚡ 打开任务管理器", Tag = HwAction.OpenTaskManager });
            combo.Items.Add(new ComboBoxItem { Content = "🪟 展开硬件仪表盘", Tag = HwAction.ToggleExpand });
            combo.Items.Add(new ComboBoxItem { Content = "📈 打开资源监视器", Tag = HwAction.OpenResourceMonitor });
            combo.Items.Add(new ComboBoxItem { Content = "💻 打开系统设置", Tag = HwAction.OpenWindowsSettings });
            combo.Items.Add(new ComboBoxItem { Content = "🚀 打开自定义程序", Tag = HwAction.OpenCustomApp });
            combo.Items.Add(new ComboBoxItem { Content = "⚙️ 灵动岛设置", Tag = HwAction.OpenSettings });
            combo.Items.Add(new ComboBoxItem { Content = "🚫 无动作", Tag = HwAction.None });

            SelectComboItemByTag(combo, selectedAction);
        }

        private void PopulateNoteActionCombo(ComboBox combo, NoteAction selectedAction)
        {
            combo.Items.Clear();
            combo.Items.Add(new ComboBoxItem { Content = "📝 展开全文 / 收起", Tag = NoteAction.ToggleExpand });
            combo.Items.Add(new ComboBoxItem { Content = "⚙️ 灵动岛设置", Tag = NoteAction.OpenSettings });
            combo.Items.Add(new ComboBoxItem { Content = "🚀 打开自定义程序", Tag = NoteAction.OpenCustomApp });
            combo.Items.Add(new ComboBoxItem { Content = "🚫 无动作", Tag = NoteAction.None });

            SelectComboItemByTag(combo, selectedAction);
        }

        private static void SelectComboItemByTag(ComboBox combo, object selectedTag)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item && item.Tag.Equals(selectedTag))
                {
                    combo.SelectedIndex = i;
                    break;
                }
            }
        }
        #endregion

        #region 2. 各胶囊专属自定义程序选择与动作回调
        /// <summary>把 Button/TextBox 的 Tag（Media/Notification/Hardware/Note）解析为模块。</summary>
        private static IslandModule ModuleFromTag(string? tag) => tag switch
        {
            "Notification" => IslandModule.Notification,
            "Hardware" => IslandModule.Hardware,
            "Note" => IslandModule.Note,
            _ => IslandModule.Media
        };

        private TextBox GetCustomAppTextBox(IslandModule mod) => mod switch
        {
            IslandModule.Notification => TxtNotifCustomApp,
            IslandModule.Hardware => TxtHwCustomApp,
            IslandModule.Note => TxtNoteCustomApp,
            _ => TxtMediaCustomApp
        };

        private void BtnBrowseApp_Click(object sender, RoutedEventArgs e)
        {
            if (_modalDialogOpen) return;
            _modalDialogOpen = true;
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "可执行程序 (*.exe)|*.exe|所有文件 (*.*)|*.*",
                    Title = "选择要绑定的自定义应用程序"
                };

                // 显式以本窗口为所有者；期间 Deactivated 不会关闭窗口
                if (dialog.ShowDialog(this) == true)
                {
                    IslandModule mod = ModuleFromTag((sender as FrameworkElement)?.Tag as string);
                    TextBox box = GetCustomAppTextBox(mod);
                    box.Text = dialog.FileName;   // TextChanged 会同步到 _mainWindow 并持久化
                    box.Focus();
                }
            }
            catch (Exception ex)
            {
                AppLog.Write(ex, "open-file-dialog");
            }
            finally
            {
                _modalDialogOpen = false;
            }
        }

        private void ClearCustomApp_Click(object sender, RoutedEventArgs e)
        {
            IslandModule mod = ModuleFromTag((sender as FrameworkElement)?.Tag as string);
            GetCustomAppTextBox(mod).Text = "";
        }

        private void TxtCustomAppPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _mainWindow == null || sender is not TextBox box) return;
            _mainWindow.SetCustomAppPath(ModuleFromTag(box.Tag as string), box.Text.Trim());
        }

        private void CmbMediaAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _mainWindow == null) return;
            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag is MediaAction action)
            {
                if (combo == CmbMediaLeftAction) _mainWindow.MediaLeftAction = action;
                else if (combo == CmbMediaRightAction) _mainWindow.MediaRightAction = action;
                _mainWindow.SaveConfiguration();
                RefreshCustomAppRows();
            }
        }

        private void CmbNotifAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _mainWindow == null) return;
            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag is NotifAction action)
            {
                if (combo == CmbNotifLeftAction) _mainWindow.NotifLeftAction = action;
                else if (combo == CmbNotifRightAction) _mainWindow.NotifRightAction = action;
                _mainWindow.SaveConfiguration();
                RefreshCustomAppRows();
            }
        }

        private void CmbHwAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _mainWindow == null) return;
            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag is HwAction action)
            {
                if (combo == CmbHwLeftAction) _mainWindow.HwLeftAction = action;
                else if (combo == CmbHwRightAction) _mainWindow.HwRightAction = action;
                _mainWindow.SaveConfiguration();
                RefreshCustomAppRows();
            }
        }

        private void CmbNoteAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _mainWindow == null) return;
            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && item.Tag is NoteAction action)
            {
                if (combo == CmbNoteLeftAction) _mainWindow.NoteLeftAction = action;
                else if (combo == CmbNoteRightAction) _mainWindow.NoteRightAction = action;
                _mainWindow.SaveConfiguration();
                RefreshCustomAppRows();
            }
        }

        private void TxtNoteContent_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _mainWindow == null) return;
            _mainWindow.SetNoteText(TxtNoteContent.Text);
        }

        private void TxtNoteTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _mainWindow == null) return;
            _mainWindow.SetNoteTitle(TxtNoteTitle.Text);
        }

        /// <summary>当某胶囊任一侧动作 = “打开自定义程序”时，展开该胶囊的程序选择行；
        /// 主胶囊右键固定为设置，因此主胶囊只统计左键；胶囊未添加或未选择则收起。</summary>
        private void RefreshCustomAppRows()
        {
            IslandModule main = _mainWindow.ConfiguredMainModule;
            bool customOn(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag switch
            {
                MediaAction.OpenCustomApp => true,
                NotifAction.OpenCustomApp => true,
                HwAction.OpenCustomApp => true,
                NoteAction.OpenCustomApp => true,
                _ => false
            };
            bool visibleRow(IslandModule mod, ComboBox left, ComboBox right) =>
                IsModuleAdded(mod) && (customOn(left) || (mod != main && customOn(right)));

            MediaCustomAppRow.Visibility = visibleRow(IslandModule.Media, CmbMediaLeftAction, CmbMediaRightAction)
                ? Visibility.Visible : Visibility.Collapsed;
            NotifCustomAppRow.Visibility = visibleRow(IslandModule.Notification, CmbNotifLeftAction, CmbNotifRightAction)
                ? Visibility.Visible : Visibility.Collapsed;
            HwCustomAppRow.Visibility = visibleRow(IslandModule.Hardware, CmbHwLeftAction, CmbHwRightAction)
                ? Visibility.Visible : Visibility.Collapsed;
            NoteCustomAppRow.Visibility = visibleRow(IslandModule.Note, CmbNoteLeftAction, CmbNoteRightAction)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion

        #region 3. 基础偏好 (声波条数、最大高度、缩放、自适应) —— 拖动实时但 24ms 合并提交，更丝滑
        private void QueueSliderCommit()
        {
            if (_isInitializing) return;
            if (!_sliderCommit.IsEnabled) _sliderCommit.Start();
        }

        private void ApplyPendingSliderValues()
        {
            if (_isInitializing || _mainWindow == null) return;
            try
            {
                if (_pendScale.HasValue)
                {
                    double scale = Math.Round(_pendScale.Value, 2);
                    _pendScale = null;
                    _mainWindow.SetCapsuleScale(scale);
                }
                if (_pendWidth.HasValue)
                {
                    int val = (int)_pendWidth.Value;
                    _pendWidth = null;
                    _mainWindow.SetCapsuleBaseWidth(val);
                }
                if (_pendWaveCount.HasValue || _pendWaveHeight.HasValue)
                {
                    int count = (int)(_pendWaveCount ?? _mainWindow.WaveBarCount);
                    double height = _pendWaveHeight ?? _mainWindow.WaveMaxHeight;
                    _pendWaveCount = null;
                    _pendWaveHeight = null;
                    _mainWindow.SetWaveConfig(count, height);
                }
            }
            catch { }
        }

        private void SliderWaveCount_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtWaveCountVal == null || _mainWindow == null) return;
            TxtWaveCountVal.Text = $"{(int)e.NewValue} 根";
            if (_isInitializing) return;
            _pendWaveCount = e.NewValue;
            QueueSliderCommit();
        }

        private void SliderWaveMaxHeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtWaveHeightVal == null || _mainWindow == null) return;
            TxtWaveHeightVal.Text = $"{(int)e.NewValue} px";
            if (_isInitializing) return;
            _pendWaveHeight = e.NewValue;
            QueueSliderCommit();
        }

        private void SliderCapsuleScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtCapsuleScaleVal == null || _mainWindow == null) return;
            double scale = Math.Round(e.NewValue, 2);
            TxtCapsuleScaleVal.Text = $"{(int)(scale * 100)} %";
            if (_isInitializing) return;
            _pendScale = scale;
            QueueSliderCommit();
        }

        private void SliderCapsuleWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtCapsuleWidthVal == null || _mainWindow == null) return;
            TxtCapsuleWidthVal.Text = $"{(int)e.NewValue} px";
            if (_isInitializing) return;
            _pendWidth = e.NewValue;
            QueueSliderCommit();
        }

        private void ChkAutoWidth_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.SetAutoWidthEnabled(ChkAutoWidth.IsChecked == true);
        }

        private void SyncIdleStyleRadio()
        {
            RadioIdleNone.IsChecked = _mainWindow.IdleStyle == IdleStyle.None;
            RadioIdleCircle.IsChecked = _mainWindow.IdleStyle == IdleStyle.Circle;
            RadioIdleNotch.IsChecked = _mainWindow.IdleStyle == IdleStyle.Notch;
        }

        private void OnIdleStyleRadioChanged(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null || sender is not RadioButton rb || rb.Tag is not string tag) return;
            _mainWindow.SetIdleStyle((IdleStyle)int.Parse(tag));
        }

        private void WaveColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                _mainWindow.SetWaveColor(color);
                RefreshSelectionIndicators();
            }
        }

        private void FontColor_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null || sender is not Button btn || btn.Tag is not string tag) return;
            _mainWindow.SetFontColor(tag.Equals("Auto", StringComparison.OrdinalIgnoreCase) ? "" : tag);
            RefreshSelectionIndicators();
        }

        private void ChkLikeShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null) return;
            _mainWindow.SetLikeShortcutEnabled(ChkLikeShortcut.IsChecked == true);
        }

        /// <summary>失焦提交自定义快捷键；非法格式回退为当前值。</summary>
        private void LikeField_LostFocus(object sender, RoutedEventArgs e) => CommitLikeFields();

        private void LikeField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitLikeFields();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void CommitLikeFields()
        {
            if (_mainWindow == null) return;
            if (!_mainWindow.SetLikeShortcutKey(TxtLikeKey.Text)) TxtLikeKey.Text = _mainWindow.LikeShortcutKey;
        }

        private void ChkStartup_Click(object sender, RoutedEventArgs e)
        {
            SetStartup(ChkStartup.IsChecked == true);
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return;
                if (enable)
                {
                    string exePath = Environment.ProcessPath ?? "";
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch { }
        }

        private void ResetPos_Click(object sender, RoutedEventArgs e) => _mainWindow.PositionToTopCenter();

        private void ChkAutoCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null) return;
            _mainWindow.IsStartupCheckUpdatesEnabled = ChkAutoCheckUpdate.IsChecked == true;
            _mainWindow.SaveConfiguration();
        }

        private void TxtUpdateManifestUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _mainWindow == null) return;
            _mainWindow.UpdateManifestUrl = TxtUpdateManifestUrl.Text.Trim();
            _mainWindow.SaveConfiguration();
        }
        #endregion

        #region 4. 主次架构与自定义胶囊（添加/移除）
        private static readonly IslandModule[] AllModules =
        {
            IslandModule.Media, IslandModule.Notification, IslandModule.Hardware, IslandModule.Note
        };

        /// <summary>模块是否可用：已添加（GetModuleEnabled）或正作为主胶囊在场。</summary>
        private bool IsModuleAdded(IslandModule mod) =>
            _mainWindow.GetModuleEnabled(mod) || _mainWindow.ConfiguredMainModule == mod;

        /// <summary>当前“已添加”（独立胶囊在场）的模块数，主胶囊强制计入。</summary>
        private int CountAddedModules()
        {
            int n = 0;
            foreach (IslandModule m in AllModules)
                if (IsModuleAdded(m)) n++;
            return n;
        }

        private Border GetModuleCard(IslandModule mod) => mod switch
        {
            IslandModule.Notification => CardNotif,
            IslandModule.Hardware => CardHw,
            IslandModule.Note => CardNote,
            _ => CardMedia
        };

        private Button GetRemoveButton(IslandModule mod) => mod switch
        {
            IslandModule.Notification => BtnRemoveNotif,
            IslandModule.Hardware => BtnRemoveHw,
            IslandModule.Note => BtnRemoveNote,
            _ => BtnRemoveMedia
        };

        private void AddPluginRow_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null || sender is not FrameworkElement fe || fe.Tag is not PluginManifest plugin) return;
            if (!PluginCatalog.TryParseKind(plugin.Kind, out IslandModule mod)) return;
            _mainWindow.ActivatePlugin(mod, plugin.Id); // 设为生效插件并确保已添加
            AfterCapsuleSetChanged();
        }

        private void RemoveCapsule_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null) return;
            IslandModule mod = ModuleFromTag((sender as FrameworkElement)?.Tag as string);
            if (!_mainWindow.GetModuleEnabled(mod)) return;

            if (CountAddedModules() <= 1)
            {
                try { MessageBox.Show(this, "至少保留一个胶囊，无法移除最后一个。", "DynamicIslandWin", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
                return;
            }

            if (_mainWindow.ConfiguredMainModule == mod)
            {
                // 主胶囊被移除时：主位自动移交给下一个已添加胶囊
                foreach (IslandModule other in AllModules)
                {
                    if (other == mod || !IsModuleAdded(other)) continue;
                    _mainWindow.ConfiguredMainModule = other;
                    break;
                }
            }

            _mainWindow.SetModuleEnabled(mod, false);
            _mainWindow.ResetActivePlugin(mod); // 该 kind 还原为内置默认插件
            AfterCapsuleSetChanged();
        }

        private void AfterCapsuleSetChanged()
        {
            SyncMainRadioSelection();
            _mainWindow.RebuildCapsuleLayout();
            UpdateMainCapsuleRightLock();
            RefreshCustomAppRows();
            RefreshCapsuleState();
        }

        /// <summary>同步卡片可见性/移除按钮，并把卡片标题与“可用预设”库刷新为插件目录内容。</summary>
        private void RefreshCapsuleState()
        {
            _mainWindow.RefreshPluginsFromDisk(); // 重扫插件目录（仅当有变更才落盘）
            foreach (IslandModule m in AllModules)
            {
                bool added = _mainWindow.GetModuleEnabled(m);
                bool canRemove = added && CountAddedModules() > 1;
                GetModuleCard(m).Visibility = added ? Visibility.Visible : Visibility.Collapsed;
                GetRemoveButton(m).Visibility = added ? Visibility.Visible : Visibility.Collapsed;
                GetRemoveButton(m).IsEnabled = canRemove;
                SetCardTitle(m);
            }
            RebuildPluginsLibrary();
        }

        private TextBlock GetCardTitle(IslandModule m) => m switch
        {
            IslandModule.Notification => TxtTitleNotif,
            IslandModule.Hardware => TxtTitleHw,
            IslandModule.Note => TxtTitleNote,
            _ => TxtTitleMedia
        };

        /// <summary>卡片标题取自该 kind 的“生效插件”清单（图标+名称）。</summary>
        private void SetCardTitle(IslandModule m)
        {
            PluginManifest p = _mainWindow.ActivePlugin(m);
            string icon = string.IsNullOrWhiteSpace(p.Icon) ? "" : p.Icon + "  ";
            GetCardTitle(m).Text = $"{icon}{p.Name}";
        }

        /// <summary>从 plugins 目录动态生成“可用预设”行：正在生效的插件隐藏，其余可点击添加/切换。</summary>
        private void RebuildPluginsLibrary()
        {
            if (PluginsLibraryList == null || _mainWindow == null) return;
            PluginsLibraryList.Children.Clear();
            foreach (PluginManifest p in _mainWindow.InstalledPlugins)
            {
                if (!PluginCatalog.TryParseKind(p.Kind, out IslandModule mod)) continue;
                PluginManifest active = _mainWindow.ActivePlugin(mod);
                if (_mainWindow.GetModuleEnabled(mod) && string.Equals(active.Id, p.Id, StringComparison.OrdinalIgnoreCase)) continue;

                string icon = string.IsNullOrWhiteSpace(p.Icon) ? "" : p.Icon + "  ";
                var btn = new Button
                {
                    Tag = p,
                    Content = $"{icon}{p.Name}",
                    ToolTip = p.Description,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 2, 0, 2),
                    Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(12, 0, 12, 0),
                    Height = 26,
                    Cursor = Cursors.Hand,
                    FontSize = 11
                };
                var radiusStyle = new Style(typeof(Border));
                radiusStyle.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(6)));
                btn.Resources[typeof(Border)] = radiusStyle;
                btn.Click += AddPluginRow_Click;
                PluginsLibraryList.Children.Add(btn);
            }
        }

        private void OnMainCapsuleRadioChanged(object sender, RoutedEventArgs e)
        {
            if (RadioMainMedia.IsChecked == true) _mainWindow.ConfiguredMainModule = IslandModule.Media;
            else if (RadioMainNotif.IsChecked == true) _mainWindow.ConfiguredMainModule = IslandModule.Notification;
            else if (RadioMainHw.IsChecked == true) _mainWindow.ConfiguredMainModule = IslandModule.Hardware;
            else if (RadioMainNote.IsChecked == true) _mainWindow.ConfiguredMainModule = IslandModule.Note;

            SyncMainRadioSelection();
            _mainWindow.RebuildCapsuleLayout();
            UpdateMainCapsuleRightLock();
            RefreshCustomAppRows();
            RefreshCapsuleState();
        }

        /// <summary>该胶囊被设为主胶囊时，右键固定为打开设置：隐藏可配置右键下拉并给出提示；降回副胶囊后恢复。</summary>
        private void UpdateMainCapsuleRightLock()
        {
            SetRightLock(IslandModule.Media, CmbMediaRightAction, LblMediaRight);
            SetRightLock(IslandModule.Notification, CmbNotifRightAction, LblNotifRight);
            SetRightLock(IslandModule.Hardware, CmbHwRightAction, LblHwRight);
            SetRightLock(IslandModule.Note, CmbNoteRightAction, LblNoteRight);
        }

        private void SetRightLock(IslandModule mod, ComboBox rightCombo, TextBlock label)
        {
            bool isMain = _mainWindow.ConfiguredMainModule == mod;
            rightCombo.Visibility = isMain ? Visibility.Collapsed : Visibility.Visible;
            label.Text = isMain ? "右键: ⚙️ 灵动岛设置 (主胶囊固定)" : "右键: ";
        }
        #endregion

        #region 5. 定时提醒（通知胶囊）
        private bool _suppressReminderEvents;

        private void AddReminder_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null) return;
            if (_mainWindow.Reminders.Count >= 10) return;
            DateTime next = DateTime.Now.AddMinutes(1);
            _mainWindow.Reminders.Add(new ReminderConfig { Enabled = true, Hour = next.Hour, Minute = next.Minute, Message = "" });
            _mainWindow.SaveConfiguration();
            RebuildRemindersList();
        }

        /// <summary>依据配置重建所有定时提醒行（每条：开关 / 时 / 分 / 内容 / 删除）。</summary>
        private void RebuildRemindersList()
        {
            if (RemindersList == null || _mainWindow == null) return;
            RemindersList.Children.Clear();
            _suppressReminderEvents = true;
            try
            {
                Brush white = Brushes.White;
                Brush softBorder = new SolidColorBrush(Color.FromArgb(53, 255, 255, 255));
                Brush softBg = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));

                foreach (ReminderConfig r in _mainWindow.Reminders)
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                    var chk = new CheckBox
                    {
                        Style = (Style)FindResource("ModernDarkCheckBox"),
                        IsChecked = r.Enabled,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    chk.Click += (s, e) =>
                    {
                        r.Enabled = chk.IsChecked == true;
                        row.Opacity = r.Enabled ? 1.0 : 0.45;
                        _mainWindow.SaveConfiguration();
                    };

                    var cboHour = MakeTimeCombo(r.Hour, 24, v => { r.Hour = v; _mainWindow.SaveConfiguration(); });
                    var sep = new TextBlock
                    {
                        Text = ":",
                        Foreground = new SolidColorBrush(Color.FromArgb(136, 255, 255, 255)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(2, 0, 2, 0)
                    };
                    var cboMin = MakeTimeCombo(r.Minute, 60, v => { r.Minute = v; _mainWindow.SaveConfiguration(); });

                    var txt = new TextBox
                    {
                        Width = 148,
                        Height = 24,
                        Text = r.Message,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Background = softBg,
                        Foreground = white,
                        BorderBrush = softBorder,
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(6, 0, 0, 0),
                        ToolTip = "提醒内容（可留空，默认显示时间）"
                    };
                    txt.TextChanged += (s, e) =>
                    {
                        if (_suppressReminderEvents) return;
                        r.Message = txt.Text;
                        _mainWindow.SaveConfiguration();
                    };

                    var del = new Button
                    {
                        Content = "✕",
                        Width = 24,
                        Height = 24,
                        Margin = new Thickness(6, 0, 0, 0),
                        Background = new SolidColorBrush(Color.FromArgb(34, 255, 69, 58)),
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 159, 159)),
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand,
                        ToolTip = "删除该提醒"
                    };
                    del.Click += (s, e) =>
                    {
                        _mainWindow.Reminders.Remove(r);
                        _mainWindow.SaveConfiguration();
                        RebuildRemindersList();
                    };

                    row.Children.Add(chk);
                    row.Children.Add(cboHour);
                    row.Children.Add(sep);
                    row.Children.Add(cboMin);
                    row.Children.Add(txt);
                    row.Children.Add(del);
                    row.Opacity = r.Enabled ? 1.0 : 0.45;

                    var border = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
                        CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(0, 2, 0, 2),
                        Padding = new Thickness(6, 2, 6, 2),
                        Child = row
                    };
                    RemindersList.Children.Add(border);
                }
            }
            finally
            {
                _suppressReminderEvents = false;
            }
        }

        private ComboBox MakeTimeCombo(int selectedValue, int count, Action<int> onPick)
        {
            var combo = new ComboBox
            {
                Style = (Style)FindResource("ModernActionCombo"),
                Width = 62,
                Height = 26
            };
            for (int v = 0; v < count; v++)
                combo.Items.Add(new ComboBoxItem { Content = v.ToString("00"), Tag = v });
            foreach (ComboBoxItem item in combo.Items)
            {
                if ((int)item.Tag == selectedValue)
                {
                    combo.SelectedItem = item;
                    break;
                }
            }
            combo.SelectionChanged += (s, e) =>
            {
                if (_suppressReminderEvents) return;
                if (combo.SelectedItem is ComboBoxItem item && item.Tag is int v) onPick(v);
            };
            return combo;
        }
        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(320);

            var scaleAnimX = new DoubleAnimation(0.88, 1.0, duration) { EasingFunction = ease };
            var scaleAnimY = new DoubleAnimation(0.88, 1.0, duration) { EasingFunction = ease };
            var transAnimY = new DoubleAnimation(-15, 0, duration) { EasingFunction = ease };
            var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220));

            SettingsScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimX);
            SettingsScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimY);
            SettingsTranslate.BeginAnimation(TranslateTransform.YProperty, transAnimY);
            SettingsRootBorder.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    if (FindParent<Button>(dep) != null || FindParent<Slider>(dep) != null || 
                        FindParent<CheckBox>(dep) != null || FindParent<RadioButton>(dep) != null ||
                        FindParent<ComboBox>(dep) != null || FindParent<TextBox>(dep) != null)
                    {
                        return;
                    }
                }
                try { this.DragMove(); } catch { }
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? cur = child;
            while (cur != null) { if (cur is T m) return m; cur = VisualTreeHelper.GetParent(cur); }
            return null;
        }

        private void SyncMainRadioSelection()
        {
            // 主模块必须属于“已添加”集合；异常（如旧配置）时自动移交到任一已添加模块
            bool hasMain = _mainWindow.GetModuleEnabled(_mainWindow.ConfiguredMainModule);
            if (!hasMain)
            {
                foreach (IslandModule m in AllModules)
                {
                    if (!_mainWindow.GetModuleEnabled(m)) continue;
                    _mainWindow.ConfiguredMainModule = m;
                    hasMain = true;
                    break;
                }
            }
            if (!hasMain)
            {
                // 理论上不会发生：兜底强制加入媒体胶囊
                _mainWindow.ConfiguredMainModule = IslandModule.Media;
                _mainWindow.SetModuleEnabled(IslandModule.Media, true);
            }

            RadioMainMedia.IsEnabled = _mainWindow.GetModuleEnabled(IslandModule.Media);
            RadioMainNotif.IsEnabled = _mainWindow.GetModuleEnabled(IslandModule.Notification);
            RadioMainHw.IsEnabled = _mainWindow.GetModuleEnabled(IslandModule.Hardware);
            RadioMainNote.IsEnabled = _mainWindow.GetModuleEnabled(IslandModule.Note);

            RadioMainMedia.IsChecked = _mainWindow.ConfiguredMainModule == IslandModule.Media;
            RadioMainNotif.IsChecked = _mainWindow.ConfiguredMainModule == IslandModule.Notification;
            RadioMainHw.IsChecked = _mainWindow.ConfiguredMainModule == IslandModule.Hardware;
            RadioMainNote.IsChecked = _mainWindow.ConfiguredMainModule == IslandModule.Note;
        }

        #region 导航切换
        private void NavThemes_Click(object sender, RoutedEventArgs e) => SetNavState(NavThemes, PanelThemes);
        private void NavBasic_Click(object sender, RoutedEventArgs e) => SetNavState(NavBasic, PanelBasic);
        private void NavArchitecture_Click(object sender, RoutedEventArgs e) => SetNavState(NavArchitecture, PanelArchitecture);

        private void SetNavState(Button activeBtn, UIElement activePanel)
        {
            NavThemes.Background = Brushes.Transparent;
            NavThemes.Foreground = new SolidColorBrush(Color.FromArgb(153, 255, 255, 255));
            NavBasic.Background = Brushes.Transparent;
            NavBasic.Foreground = new SolidColorBrush(Color.FromArgb(153, 255, 255, 255));
            NavArchitecture.Background = Brushes.Transparent;
            NavArchitecture.Foreground = new SolidColorBrush(Color.FromArgb(153, 255, 255, 255));

            PanelThemes.Visibility = Visibility.Collapsed;
            PanelBasic.Visibility = Visibility.Collapsed;
            PanelArchitecture.Visibility = Visibility.Collapsed;

            activeBtn.Background = new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
            activeBtn.Foreground = Brushes.White;
            activePanel.Visibility = Visibility.Visible;
        }
        #endregion

        #region 质感材质
        private void ThemeSelect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string themeKey)
            {
                _mainWindow.ApplyTheme(themeKey);
                RefreshSelectionIndicators();
            }
        }

        /// <summary>当前已选项加深高亮：主题卡 / 声波色 / 字体色 / 主胶囊单选等。</summary>
        private void RefreshSelectionIndicators()
        {
            if (_mainWindow == null) return;
            Brush accent = new SolidColorBrush(Color.FromRgb(0, 229, 255));
            Brush ringIdle = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));

            // 1) 主题卡：当前主题深色高亮 + 标题文字青色
            foreach (object o in PanelThemes.Children)
            {
                if (o is not Button b || b.Tag is not string tk) continue;
                bool selected = tk.Equals(_mainWindow.CurrentThemeKey, StringComparison.OrdinalIgnoreCase);
                b.Background = selected
                    ? new SolidColorBrush(Color.FromArgb(66, 0, 170, 255))   // 深一档的青色
                    : new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                if (FindContentTextBlock(b) is { } tb)
                {
                    tb.Foreground = selected ? accent : Brushes.White;
                }
            }

            // 2) 声波拾色球
            string waveNorm = NormalizeHex(_mainWindow.CurrentWaveColorHex);
            foreach (object o in WaveColorsRow.Children)
            {
                if (o is Button b && b.Tag is string tag)
                {
                    bool sel = NormalizeHex(tag) == waveNorm;
                    b.BorderBrush = sel ? accent : ringIdle;
                    b.BorderThickness = new Thickness(sel ? 2 : 1);
                }
            }

            // 3) 字体颜色（Auto 按钮或色球）
            string font = _mainWindow.FontColorHex ?? "";
            foreach (object o in FontColorsRow.Children)
            {
                if (o is not Button b || b.Tag is not string tag) continue;
                bool sel = tag.Equals("Auto", StringComparison.OrdinalIgnoreCase)
                    ? string.IsNullOrWhiteSpace(font)
                    : NormalizeHex(tag) == NormalizeHex(font);
                b.BorderBrush = sel ? accent : ringIdle;
                b.BorderThickness = new Thickness(sel ? 2 : 1);
                if (tag.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                {
                    b.Background = sel
                        ? new SolidColorBrush(Color.FromArgb(58, 0, 170, 255))
                        : new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                }
            }
        }

        private static string NormalizeHex(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return "";
            string t = hex.Trim().TrimStart('#');
            if (t.Length >= 6) t = t.Substring(t.Length - 6);
            return t.ToUpperInvariant();
        }

        private static TextBlock? FindContentTextBlock(Button b)
        {
            if (b.Content is TextBlock tb) return tb;
            if (b.Content is Panel p)
            {
                foreach (object child in p.Children)
                {
                    if (child is TextBlock t) return t;
                    if (child is Panel inner)
                    {
                        foreach (object c in inner.Children)
                        {
                            if (c is TextBlock t2) return t2;
                        }
                    }
                }
            }
            return null;
        }
        #endregion

        private void ExitApp_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void Close_Click(object sender, RoutedEventArgs e) => SafeClose();
        private void Window_Deactivated(object? sender, EventArgs e)
        {
            // 模态对话框（文件选择等）打开期间不自动关闭；关闭过程防重入
            if (_modalDialogOpen || _isClosingWindow) return;
            SafeClose();
        }

        private bool _isClosingWindow;
        private void SafeClose()
        {
            if (_isClosingWindow || !IsVisible) return;
            _isClosingWindow = true;
            try { Close(); }
            catch { _isClosingWindow = false; }
            Closed += (s, e) => _isClosingWindow = true;
        }
    }
}