using Microsoft.Win32;
using System.Diagnostics;

// 备注：由于是单文件轻量应用，因此各种function函数没有作拆分，后续有更多需求再考虑拆分成多个类文件。

namespace DisplayModeSwitch
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        // ===== 状态机 =====
        // WaitDefault：设置了默认模式时，开机后"尚未发生任何切换"的第一段倒计时，到点自动应用默认模式
        // WaitExit   ：任意一次切换（无论是默认超时触发，还是用户手动1~6/Enter触发）完成后，进入的"无操作则自动退出"倒计时
        // Paused     ：倒计时暂停中（记录 _prevState 以便恢复）
        // Idle       ：未设置默认模式时的常态，没有任何倒计时
        private enum State { WaitDefault, WaitExit, Paused, Idle }
        private enum Lang { ZH, EN }

        private State _state = State.Idle;
        private State _prevState = State.Idle; // 暂停前的状态，用于恢复
        private Lang _lang = Lang.ZH;
        private bool _isDark = false;

        private int _defaultMode = 0;      // 0=不设置默认模式，1~4=四种输出模式
        private int _countdownSeconds = 10; // 用户自定义的倒计时时长（默认前等待 与 操作后退出等待 共用同一个值）
        private int _count;                 // 当前倒计时剩余秒数
        private int _currentMode = 1;       // 本次运行期间，5/6 循环切换所依据的"当前模式"，与实际系统状态无关，仅作循环游标

        // ===== 控件 =====
        private System.Windows.Forms.Timer _timer;
        private Panel _topPanel;
        private Label _lblTitle;
        private Label _lblCountdown;
        private Label _lblStatus;
        private Label _lblDefaultCaption;
        private RadioButton _radioNone, _radioMode1, _radioMode2, _radioMode3, _radioMode4;
        private NumericUpDown _numSeconds;
        private Label _lblSecondsSuffix;
        private System.Windows.Forms.Timer _secondsIdleTimer; // 秒数输入框：打字/按上下箭头后1秒空闲，判定输入完成
        private Label _lblHint56, _lblHint0, _lblHintEsc, _lblEnterHint;
        private TableLayoutPanel _optionsPanel;
        private Button _btn0, _btn1, _btn2, _btn3, _btn4, _btn5, _btn6, _btnEsc, _btnTheme, _btnLang;

        public MainForm()
        {
            LoadSettings(); // 1. 先读取上次保存的设置（默认模式/语言/主题/倒计时秒数）
            InitUI();       // 2. 初始化控件并绑定事件
            ApplyTheme();
            RebuildTexts();

            // 3. 根据读取到的 _defaultMode 勾选对应单选框（事件里赋的值与已读取的值一致，不会覆盖错误）
            switch (_defaultMode)
            {
                case 0: _radioNone.Checked = true; break;
                case 1: _radioMode1.Checked = true; break;
                case 2: _radioMode2.Checked = true; break;
                case 3: _radioMode3.Checked = true; break;
                case 4: _radioMode4.Checked = true; break;
            }

            _currentMode = _defaultMode != 0 ? _defaultMode : 1;

            // 4. 决定启动状态：设置了默认模式则进入等待默认倒计时，否则 Idle（无倒计时）
            if (_defaultMode != 0)
            {
                _state = State.WaitDefault;
                _count = _countdownSeconds;
                _timer.Start();
            }
            else
            {
                _state = State.Idle;
            }
            UpdateLabel();
        }

        // 简易双语取词
        private string L(string zh, string en) => _lang == Lang.ZH ? zh : en;

        private string ModeArg(int mode) => mode switch
        {
            1 => "/internal",
            2 => "/external",
            3 => "/clone",
            4 => "/extend",
            _ => "/extend"
        };

        private string ModeName(int mode) => mode switch
        {
            1 => L("仅电脑屏幕", "PC Screen Only"),
            2 => L("仅第二屏幕", "Second Screen Only"),
            3 => L("复制", "Duplicate"),
            4 => L("扩展", "Extend"),
            _ => L("不设置", "None")
        };

        private void InitUI()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            ClientSize = new Size(520, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            // ===== 顶部标题栏 =====
            _topPanel = new Panel { Dock = DockStyle.Top, Height = 44 };

            _lblTitle = new Label
            {
                Font = new Font("Microsoft YaHei", 14, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true
            };

            _btnTheme = new Button
            {
                Text = "\U0001F313",
                Dock = DockStyle.Right,
                Width = 44,
                FlatStyle = FlatStyle.Flat
            };
            _btnTheme.Click += (s, e) => { _isDark = !_isDark; ApplyTheme(); };

            _btnLang = new Button
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(44, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                FlatStyle = FlatStyle.Flat
            };
            _btnLang.Click += (s, e) => { _lang = _lang == Lang.ZH ? Lang.EN : Lang.ZH; RebuildTexts(); };

            _topPanel.Controls.Add(_lblTitle);
            _topPanel.Controls.Add(_btnLang);
            _topPanel.Controls.Add(_btnTheme);

            _lblCountdown = new Label
            {
                Font = new Font("Microsoft YaHei", 12),
                Dock = DockStyle.Top,
                Height = 45,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _lblStatus = new Label
            {
                Font = new Font("Microsoft YaHei", 10),
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ===== 选项区 =====
            _optionsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(24, 6, 10, 6)
            };
            _optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _lblDefaultCaption = new Label
            {
                Font = new Font("Microsoft YaHei", 9, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 2)
            };

            _radioNone = new RadioButton { AutoSize = true, Checked = true, Font = new Font("Microsoft YaHei", 10), Margin = new Padding(0, 2, 0, 2) };
            _radioMode1 = new RadioButton { AutoSize = true, Font = new Font("Microsoft YaHei", 10), Margin = new Padding(0, 2, 0, 2) };
            _radioMode2 = new RadioButton { AutoSize = true, Font = new Font("Microsoft YaHei", 10), Margin = new Padding(0, 2, 0, 2) };
            _radioMode3 = new RadioButton { AutoSize = true, Font = new Font("Microsoft YaHei", 10), Margin = new Padding(0, 2, 0, 2) };
            _radioMode4 = new RadioButton { AutoSize = true, Font = new Font("Microsoft YaHei", 10), Margin = new Padding(0, 2, 0, 4) };

            // 初始化循环里统一加：关闭 RadioButton 的 Tab 焦点，防止方向键误改默认模式
            _radioNone.TabStop = false;
            _radioMode1.TabStop = false;
            _radioMode2.TabStop = false;
            _radioMode3.TabStop = false;
            _radioMode4.TabStop = false;

            // 不依赖容器分组，手动强制互斥
            _radioNone.CheckedChanged += (s, e) => { if (_radioNone.Checked) { _defaultMode = 0; OnDefaultModeChanged(); } };
            _radioMode1.CheckedChanged += (s, e) => { if (_radioMode1.Checked) { _defaultMode = 1; OnDefaultModeChanged(); } };
            _radioMode2.CheckedChanged += (s, e) => { if (_radioMode2.Checked) { _defaultMode = 2; OnDefaultModeChanged(); } };
            _radioMode3.CheckedChanged += (s, e) => { if (_radioMode3.Checked) { _defaultMode = 3; OnDefaultModeChanged(); } };
            _radioMode4.CheckedChanged += (s, e) => { if (_radioMode4.Checked) { _defaultMode = 4; OnDefaultModeChanged(); } };

            _numSeconds = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 9999999,
                Value = Math.Max(1, Math.Min(9999999, _countdownSeconds)),
                Width = 100,
                Font = new Font("Microsoft YaHei", 10),
                TextAlign = HorizontalAlignment.Center,
                Margin = new Padding(0, 6, 2, 0)
            };
            _numSeconds.ValueChanged += (s, e) =>
            {
                _countdownSeconds = (int)_numSeconds.Value == 0 ? 10 : (int)_numSeconds.Value;
                if (_state == State.WaitDefault || _state == State.WaitExit || _state == State.Paused) // 新增 Paused
                {
                    _count = _countdownSeconds;
                }
                RebuildTexts();
                RestartSecondsIdleTimer();
            };
            _numSeconds.TextChanged += (s, e) => RestartSecondsIdleTimer();
            _numSeconds.MouseLeave += (s, e) => CommitSecondsInput();
            _numSeconds.BorderStyle = BorderStyle.None;
            _numSeconds.MouseEnter += (s, e) => _numSeconds.BorderStyle = BorderStyle.FixedSingle;

            _secondsIdleTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _secondsIdleTimer.Tick += (s, e) => CommitSecondsInput();

            _lblSecondsSuffix = new Label
            {
                AutoSize = true,
                Font = new Font("Microsoft YaHei", 10),
                Margin = new Padding(0, 10, 0, 0)
            };

            _lblHint56 = new Label { AutoSize = true, Font = new Font("Microsoft YaHei", 10), Margin = new Padding(20, 6, 0, 2) };
            _lblHint0 = new Label { AutoSize = true, Font = new Font("Microsoft YaHei", 10), Margin = new Padding(20, 2, 0, 2) };
            _lblHintEsc = new Label { AutoSize = true, Font = new Font("Microsoft YaHei", 10), Margin = new Padding(20, 2, 0, 8) };
            _lblEnterHint = new Label { AutoSize = true, Font = new Font("Microsoft YaHei", 9, FontStyle.Bold), Margin = new Padding(0, 4, 0, 2) };

            int row = 0;
            _optionsPanel.Controls.Add(_lblDefaultCaption, 0, row); _optionsPanel.SetColumnSpan(_lblDefaultCaption, 3); row++;
            _optionsPanel.Controls.Add(_radioNone, 0, row); _optionsPanel.SetColumnSpan(_radioNone, 3); row++;
            _optionsPanel.Controls.Add(_radioMode1, 0, row); _optionsPanel.SetColumnSpan(_radioMode1, 3); row++;
            _optionsPanel.Controls.Add(_radioMode2, 0, row); _optionsPanel.SetColumnSpan(_radioMode2, 3); row++;
            _optionsPanel.Controls.Add(_radioMode3, 0, row); _optionsPanel.SetColumnSpan(_radioMode3, 3); row++;
            _optionsPanel.Controls.Add(_radioMode4, 0, row); _optionsPanel.SetColumnSpan(_radioMode4, 3); row++;

            _optionsPanel.Controls.Add(_numSeconds, 0, row);
            _optionsPanel.Controls.Add(_lblSecondsSuffix, 1, row);
            row++;

            _optionsPanel.Controls.Add(_lblHint56, 0, row); _optionsPanel.SetColumnSpan(_lblHint56, 3); row++;
            _optionsPanel.Controls.Add(_lblHint0, 0, row); _optionsPanel.SetColumnSpan(_lblHint0, 3); row++;
            _optionsPanel.Controls.Add(_lblHintEsc, 0, row); _optionsPanel.SetColumnSpan(_lblHintEsc, 3); row++;
            _optionsPanel.Controls.Add(_lblEnterHint, 0, row); _optionsPanel.SetColumnSpan(_lblEnterHint, 3); row++;

            _optionsPanel.Margin = new Padding(24, 6, 24, 0);

            // ===== 底部按钮 =====
            var panel = new FlowLayoutPanel
            {
                Location = new Point(0, 410), // 給予一個初始底部的坐標
                Height = 60,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(24, 10, 24, 10),
                WrapContents = false, // 絕對不允許按鈕換行
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            _btn0 = MakeButton((s, e) => TogglePauseResume());
            _btn1 = MakeButton((s, e) => ExecuteAndAdvance(1));
            _btn2 = MakeButton((s, e) => ExecuteAndAdvance(2));
            _btn3 = MakeButton((s, e) => ExecuteAndAdvance(3));
            _btn4 = MakeButton((s, e) => ExecuteAndAdvance(4));
            _btn5 = MakeButton((s, e) => CycleNext());
            _btn6 = MakeButton((s, e) => CyclePrev());
            _btnEsc = MakeButton((s, e) => OnExit());

            panel.Controls.AddRange(new Control[] { _btn1, _btn2, _btn3, _btn4, _btn5, _btn6, _btn0, _btnEsc });

            Controls.Add(panel);
            Controls.Add(_optionsPanel);
            Controls.Add(_lblStatus);
            Controls.Add(_lblCountdown);
            Controls.Add(_topPanel);

            KeyDown += MainForm_KeyDown;

            _timer = new System.Windows.Forms.Timer { Interval = 1000 };
            _timer.Tick += Timer_Tick;
        }

        private Button MakeButton(EventHandler onClick)
        {
            var b = new Button
            {
                Height = 40,
                AutoSize = true,
                Padding = new Padding(6, 0, 6, 0),
                Margin = new Padding(4, 4, 4, 4)
            };
            b.Click += onClick;
            return b;
        }

        // ===== 秒数输入框的自动固化逻辑 =====
        private RadioButton CurrentDefaultRadio() => _defaultMode switch
        {
            1 => _radioMode1,
            2 => _radioMode2,
            3 => _radioMode3,
            4 => _radioMode4,
            _ => _radioNone
        };

        private void RestartSecondsIdleTimer()
        {
            _secondsIdleTimer.Stop();
            _secondsIdleTimer.Start();
        }

        private void CommitSecondsInput()
        {
            _secondsIdleTimer.Stop();
            if (_numSeconds.Focused)
            {
                CurrentDefaultRadio().Focus();
            }
        }

        // ===== 语言/文案 =====
        private void RebuildTexts()
        {
            Text = L("显示输出模式切换", "Display Mode Switch");
            _lblTitle.Text = L("显示输出模式切换 2026", "Display Mode Switch 2026");

            _lblDefaultCaption.Text = L("默认模式（超时或回车触发）：", "Default Mode (on timeout / Enter):");
            _radioNone.Text = L("不设置默认模式", "None");
            _radioMode1.Text = "1 - " + ModeName(1);
            _radioMode2.Text = "2 - " + ModeName(2);
            _radioMode3.Text = "3 - " + ModeName(3);
            _radioMode4.Text = "4 - " + ModeName(4);

            _lblSecondsSuffix.Text = L("秒（默认前等待 / 操作后退出等待，共用此值）", "s (wait-for-default & wait-to-exit, shared)");

            _lblHint56.Text = L("5 - 切换到下一模式    6 - 切换到上一模式", "5 - Next Mode    6 - Previous Mode");
            _lblHint0.Text = "0 - " + L("暂停 / 恢复倒计时", "Pause / Resume Countdown");
            _lblHintEsc.Text = "Esc - " + L("退出程序", "Exit Program");
            _lblEnterHint.Text = "\u23CE " + L("回车 = 执行默认模式（若已设置）", "Enter = Run Default Mode (if set)")
    + "\n" + L("↑/↓ 切换默认模式选中项　←/→ 切换按钮选中", "↑/↓ change default mode　←/→ move button selection");

            _btn1.Text = "1 " + ModeName(1);
            _btn2.Text = "2 " + ModeName(2);
            _btn3.Text = "3 " + ModeName(3);
            _btn4.Text = "4 " + ModeName(4);
            _btn5.Text = "5 " + L("下一个", "Next");
            _btn6.Text = "6 " + L("上一个", "Prev");
            _btnEsc.Text = "Esc " + L("退出", "Exit");

            _btnLang.Text = _lang == Lang.ZH ? "EN" : "\u4E2D";

            UpdateLabel();
            Application.DoEvents();
        }

        // ===== 主题 =====
        private void ApplyTheme()
        {
            Color bg = _isDark ? Color.FromArgb(30, 30, 30) : Color.White;
            Color fg = _isDark ? Color.White : Color.Black;
            Color btnBg = _isDark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240);
            Color borderColor = _isDark ? Color.FromArgb(100, 100, 100) : Color.FromArgb(180, 180, 180);

            BackColor = bg;
            foreach (Control c in AllControls(this))
            {
                if (c is Label || c is RadioButton)
                {
                    c.ForeColor = fg;
                    c.BackColor = bg;
                }
                else if (c is Button btn)
                {
                    btn.ForeColor = fg;
                    btn.BackColor = btnBg;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = borderColor;
                }
                else if (c is Panel)
                {
                    c.BackColor = bg;
                }
                else if (c is NumericUpDown nud)
                {
                    nud.ForeColor = fg;
                    nud.BackColor = _isDark ? Color.FromArgb(50, 50, 50) : Color.White;
                }
            }
        }

        private IEnumerable<Control> AllControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var sub in AllControls(c)) yield return sub;
            }
        }

        // ===== 状态展示 =====
        private void UpdateLabel()
        {
            _lblStatus.Text = L(
                $"当前循环模式：{ModeName(_currentMode)}　|　默认模式：{ModeName(_defaultMode)}",
                $"Current: {ModeName(_currentMode)}   |   Default: {ModeName(_defaultMode)}");

            switch (_state)
            {
                case State.WaitDefault:
                    _lblCountdown.Text = L($"{_count} 秒后自动进入默认模式：{ModeName(_defaultMode)}...", $"Applying default ({ModeName(_defaultMode)}) in {_count}s...");
                    break;
                case State.WaitExit:
                    _lblCountdown.Text = L($"{_count} 秒后自动退出程序...", $"Auto-exit in {_count}s...");
                    break;
                case State.Paused:
                    _lblCountdown.Text = L("已暂停，按 0 恢复倒计时...", "Paused — press 0 to resume...");
                    break;
                case State.Idle:
                    _lblCountdown.Text = L("未设置默认模式，程序不会自动退出", "No default mode — program will not auto-exit");
                    break;
            }

            _btn0.Text = (_state == State.Paused ? "0 " + L("恢复", "Resume") : "0 " + L("暂停", "Pause"));
            _btn0.Enabled = (_state == State.WaitDefault || _state == State.WaitExit || _state == State.Paused);
        }

        // ===== 定时器 =====
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_state == State.Paused) return;

            _count--;
            if (_count <= 0)
            {
                _timer.Stop();
                if (_state == State.WaitDefault)
                {
                    RunDisplaySwitch(_defaultMode);
                    _currentMode = _defaultMode;
                    AdvanceAfterOperation();
                }
                else if (_state == State.WaitExit)
                {
                    SaveSettings();
                    Environment.Exit(0);
                }
                return;
            }
            UpdateLabel();
        }

        // ===== 按键处理 =====
        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (_numSeconds != null && _numSeconds.Focused) return;

            switch (e.KeyCode)
            {
                case Keys.Escape: OnExit(); break;
                case Keys.D0:
                case Keys.NumPad0: TogglePauseResume(); break;
                case Keys.D1:
                case Keys.NumPad1: ExecuteAndAdvance(1); break;
                case Keys.D2:
                case Keys.NumPad2: ExecuteAndAdvance(2); break;
                case Keys.D3:
                case Keys.NumPad3: ExecuteAndAdvance(3); break;
                case Keys.D4:
                case Keys.NumPad4: ExecuteAndAdvance(4); break;
                case Keys.D5:
                case Keys.NumPad5: CycleNext(); break;
                case Keys.D6:
                case Keys.NumPad6: CyclePrev(); break;
                case Keys.Enter: RunDefaultMode(); break;
                case Keys.T: _isDark = !_isDark; ApplyTheme(); break;
                case Keys.L: _lang = _lang == Lang.ZH ? Lang.EN : Lang.ZH; RebuildTexts(); break;
                default: return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // 秒数输入框正在编辑时放行，让 NumericUpDown 自己处理上下箭头微调数值
            if ((keyData == Keys.Up || keyData == Keys.Down) && _numSeconds != null && _numSeconds.Focused)
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }

            // 上下：切换默认模式选中项（左右交给下方 base.ProcessCmdKey 处理，
            // 用于在底部按钮之间移动焦点/切换选中，效果对齐 VirtualScreenSwitch）
            if (keyData == Keys.Up || keyData == Keys.Down)
            {
                CycleDefaultMode(keyData == Keys.Down);

                if (_state != State.Paused) _prevState = _state;
                _state = State.Paused;
                _timer.Stop();
                UpdateLabel();
                return true;
            }

            // 回车：执行当前选中的默认模式
            if (keyData == Keys.Enter)
            {
                RunDefaultMode();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ===== 核心动作 =====

        private void RunDisplaySwitch(int mode)
        {
            try
            {
                var psi = new ProcessStartInfo("DisplaySwitch.exe", ModeArg(mode))
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
            }
            catch { /* 忽略异常，防止系统缺少该工具时崩溃 */ }
        }

        // 数字键1~4 / 按钮1~4：立即切换到指定模式
        private void ExecuteAndAdvance(int mode)
        {
            _currentMode = mode;
            RunDisplaySwitch(mode);
            AdvanceAfterOperation();
        }

        // 数字键5：本次运行内循环切到下一个模式（1→2→3→4→1）
        private void CycleNext()
        {
            _currentMode = _currentMode % 4 + 1;
            RunDisplaySwitch(_currentMode);
            AdvanceAfterOperation();
        }

        // 数字键6：本次运行内循环切到上一个模式（4→3→2→1→4）
        private void CyclePrev()
        {
            _currentMode = (_currentMode - 2 + 4) % 4 + 1;
            RunDisplaySwitch(_currentMode);
            AdvanceAfterOperation();
        }

        // 回车：仅在设置了默认模式时，立即执行默认模式
        private void RunDefaultMode()
        {
            if (_defaultMode == 0) return;
            ExecuteAndAdvance(_defaultMode);
        }

        // 任意一次"完成操作"（手动切换/循环/默认超时应用）之后的统一收尾：
        // 设置了默认模式 → 开始新一轮"无操作则自动退出"倒计时；未设置 → 保持 Idle，不退出
        private void AdvanceAfterOperation()
        {
            if (_defaultMode != 0)
            {
                _state = State.WaitExit;
                _count = _countdownSeconds;
                if (!_timer.Enabled) _timer.Start();
            }
            else
            {
                _state = State.Idle;
                _timer.Stop();
            }
            UpdateLabel();
        }

        // 默认模式单选框变化：不设置则彻底停止倒计时；设置了且当前是 Idle，则开启新一轮"等待默认"倒计时
        private void OnDefaultModeChanged()
        {
            if (_defaultMode == 0)
            {
                _timer.Stop();
                _state = State.Idle;
                _prevState = State.Idle; // 新增：避免残留旧状态造成混淆
            }
            else if (_state == State.Idle)
            {
                _state = State.WaitDefault;
                _count = _countdownSeconds;
                _timer.Start();
            }
            UpdateLabel();
        }

        // 在 0(不设置)~4(扩展) 之间循环切换默认模式选中项，forward=true 往下/右切，false 往上/左切
        private void CycleDefaultMode(bool forward)
        {
            int next = _defaultMode + (forward ? 1 : -1);
            if (next > 4) next = 0;
            if (next < 0) next = 4;

            switch (next)
            {
                case 0: _radioNone.Checked = true; break;
                case 1: _radioMode1.Checked = true; break;
                case 2: _radioMode2.Checked = true; break;
                case 3: _radioMode3.Checked = true; break;
                case 4: _radioMode4.Checked = true; break;
            }
            // Checked=true 会自动触发对应 RadioButton 的 CheckedChanged，
            // 里面已经会更新 _defaultMode 并调用 OnDefaultModeChanged()，这里不用再手动赋值
        }

        // 数字键0/按钮0：暂停或恢复倒计时（无倒计时在跑时无效果）
        private void TogglePauseResume()
        {
            if (_state == State.Paused)
            {
                if (_defaultMode == 0)
                {
                    _state = State.Idle;
                    _timer.Stop();
                }
                else
                {
                    _state = (_prevState == State.WaitExit) ? State.WaitExit : State.WaitDefault;
                    _count = _countdownSeconds; // 浏览期间可能改了默认模式，重新起跳更清晰
                    _timer.Start();
                }
                UpdateLabel();
            }
            else if (_state == State.WaitDefault || _state == State.WaitExit)
            {
                _prevState = _state;
                _state = State.Paused;
                _timer.Stop();
                UpdateLabel();
            }
        }

        private void OnExit()
        {
            SaveSettings();
            Environment.Exit(0);
        }

        // ===== 注册表读写 =====
        private const string RegPath = @"Software\DisplayModeSwitch\Settings";

        private void LoadSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegPath))
                {
                    if (key != null)
                    {
                        _defaultMode = (int)key.GetValue("DefaultMode", 0);
                        _lang = (Lang)(int)key.GetValue("Lang", (int)Lang.ZH);
                        _isDark = Convert.ToBoolean(key.GetValue("IsDark", 0));
                        _countdownSeconds = (int)key.GetValue("CountdownSeconds", 10);
                    }
                }
            }
            catch { /* 忽略异常，防读取失败 */ }
        }

        private void SaveSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegPath))
                {
                    if (key != null)
                    {
                        key.SetValue("DefaultMode", _defaultMode);
                        key.SetValue("Lang", (int)_lang);
                        key.SetValue("IsDark", _isDark ? 1 : 0);
                        key.SetValue("CountdownSeconds", _countdownSeconds);
                    }
                }
            }
            catch { /* 忽略异常，防无写入权限 */ }
        }
    }
}

// 备注：由于是单文件轻量应用，因此各种function函数没有作拆分，后续有更多需求再考虑拆分成多个类文件。