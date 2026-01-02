using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Diagnostics;

namespace InvisibleMouseMover
{
    public class Program : ApplicationContext
    {
        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern uint SetThreadExecutionState(uint esFlags);

        const uint MOUSEEVENTF_MOVE = 0x0001;
        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;
        const uint ES_DISPLAY_REQUIRED = 0x00000002;

        private NotifyIcon trayIcon;
        private ToolStripMenuItem startItem;
        private ToolStripMenuItem pauseItem;
        private ToolStripMenuItem startupItem;
        
        private bool isRunning = true;
        private bool isPaused = false;
        private int minSeconds = 30;
        private int maxSeconds = 90;
        private List<ToolStripMenuItem> frequencyItems = new List<ToolStripMenuItem>();

        private readonly string appName = "InvisibleMouseMover";

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Program());
        }

        public Program()
        {
            // 1. Setup Frequency Submenu
            var freqMenu = new ToolStripMenuItem("Frequency (Seconds)");
            freqMenu.DropDownItems.Add(CreateFreqOption("30 - 90", 30, 90, true));
            freqMenu.DropDownItems.Add(CreateFreqOption("60 - 120", 60, 120));
            freqMenu.DropDownItems.Add(CreateFreqOption("90 - 150", 90, 150));
            freqMenu.DropDownItems.Add(CreateFreqOption("30 - 120", 30, 120));

            // 2. Setup Startup Option
            startupItem = new ToolStripMenuItem("Run at Startup", null, ToggleStartup);
            startupItem.Checked = IsSetToRunAtStartup();

            // 3. Setup Main Menu Items
            startItem = new ToolStripMenuItem("Start", null, StartActivity);
            pauseItem = new ToolStripMenuItem("Pause", null, PauseActivity);
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit", null, Exit);

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add(startItem);
            contextMenu.Items.Add(pauseItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(freqMenu);
            contextMenu.Items.Add(startupItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            // 4. Setup Tray Icon
            trayIcon = new NotifyIcon()
            {
                ContextMenuStrip = contextMenu,
                Visible = true,
                Text = "Activity Manager"
            };

            UpdateState(active: true);
            _ = RunMouseLoop();
        }

        // --- Startup Registry Logic ---
        private bool IsSetToRunAtStartup()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
            {
                return key?.GetValue(appName) != null;
            }
        }

        private void ToggleStartup(object? sender, EventArgs e)
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key == null) return;

                if (startupItem.Checked)
                {
                    key.DeleteValue(appName, false);
                    startupItem.Checked = false;
                }
                else
                {
                    // Path to the current executable
                    string? path = Process.GetCurrentProcess().MainModule?.FileName;
                    if (path != null)
                    {
                        key.SetValue(appName, $"\"{path}\"");
                        startupItem.Checked = true;
                    }
                }
            }
        }

        // --- Rest of the Logic ---

        private ToolStripMenuItem CreateFreqOption(string label, int min, int max, bool isChecked = false)
        {
            var item = new ToolStripMenuItem(label);
            item.Checked = isChecked;
            item.Click += (s, e) => {
                minSeconds = min; maxSeconds = max;
                foreach (var i in frequencyItems) i.Checked = false;
                item.Checked = true;
            };
            frequencyItems.Add(item);
            return item;
        }

        private void UpdateState(bool active)
        {
            isPaused = !active;
            if (active)
            {
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
                trayIcon.Icon = CreateCircleIcon(Color.LimeGreen);
                trayIcon.Text = "Activity Manager (Running)";
                startItem.Enabled = false;
                pauseItem.Enabled = true;
            }
            else
            {
                SetThreadExecutionState(ES_CONTINUOUS);
                trayIcon.Icon = CreateCircleIcon(Color.Red);
                trayIcon.Text = "Activity Manager (Paused)";
                startItem.Enabled = true;
                pauseItem.Enabled = false;
            }
        }

        private void StartActivity(object? sender, EventArgs e) => UpdateState(active: true);
        private void PauseActivity(object? sender, EventArgs e) => UpdateState(active: false);

        private Icon CreateCircleIcon(Color color)
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (SolidBrush brush = new SolidBrush(color))
                        g.FillEllipse(brush, 4, 4, 24, 24);
                }
                return Icon.FromHandle(bitmap.GetHicon());
            }
        }

        private async Task RunMouseLoop()
        {
            Random rng = new Random();
            while (isRunning)
            {
                if (!isPaused)
                {
                    mouse_event(MOUSEEVENTF_MOVE, 1, 1, 0, UIntPtr.Zero);
                    await Task.Delay(100);
                    mouse_event(MOUSEEVENTF_MOVE, -1, -1, 0, UIntPtr.Zero);
                }
                await Task.Delay(rng.Next(minSeconds, maxSeconds + 1) * 1000);
            }
        }

        void Exit(object? sender, EventArgs e)
        {
            isRunning = false;
            trayIcon.Visible = false;
            SetThreadExecutionState(ES_CONTINUOUS);
            Application.Exit();
        }
    }
}