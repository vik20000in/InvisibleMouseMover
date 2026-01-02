using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading.Tasks;

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
        private bool isRunning = true; // Overall app loop
        private bool isPaused = false; // Toggle for movement/sleep prevention

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Program());
        }

        public Program()
        {
            // 1. Setup Context Menu Items
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            
            ToolStripMenuItem startItem = new ToolStripMenuItem("Start", null, StartActivity);
            ToolStripMenuItem pauseItem = new ToolStripMenuItem("Pause", null, PauseActivity);
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit", null, Exit);

            contextMenu.Items.Add(startItem);
            contextMenu.Items.Add(pauseItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            // 2. Setup Tray Icon
            trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Shield,
                ContextMenuStrip = contextMenu,
                Visible = true,
                Text = "Hybrid Activity Manager"
            };

            // 3. Show Notification Pop-up
            trayIcon.BalloonTipTitle = "Activity Manager Active";
            trayIcon.BalloonTipText = "System will stay awake and active.";
            trayIcon.ShowBalloonTip(3000); // Show for 3 seconds

            // 4. Initial Activation
            StartActivity(null, EventArgs.Empty);

            // 5. Background Task
            _ = RunMouseLoop();
        }

        private void StartActivity(object? sender, EventArgs e)
        {
            isPaused = false;
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
            trayIcon.Text = "Hybrid Activity Manager (Running)";
            
            // Optional: Show tip when started
            trayIcon.BalloonTipText = "Activity Monitoring Resumed.";
            trayIcon.ShowBalloonTip(1000);
        }

        private void PauseActivity(object? sender, EventArgs e)
        {
            isPaused = true;
            SetThreadExecutionState(ES_CONTINUOUS); // Clear the active flags
            trayIcon.Text = "Hybrid Activity Manager (Paused)";
            
            trayIcon.BalloonTipText = "Activity Monitoring Paused.";
            trayIcon.ShowBalloonTip(1000);
        }

        private async Task RunMouseLoop()
        {
            Random rng = new Random();
            while (isRunning)
            {
                if (!isPaused)
                {
                    // Small nudge
                    mouse_event(MOUSEEVENTF_MOVE, 1, 1, 0, UIntPtr.Zero);
                    await Task.Delay(100);
                    mouse_event(MOUSEEVENTF_MOVE, -1, -1, 0, UIntPtr.Zero);
                }

                // Random delay between 30 and 90 seconds
                // Note: We still delay while paused so we don't spam the CPU check
                await Task.Delay(rng.Next(30, 91) * 1000);
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