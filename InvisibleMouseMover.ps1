Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$signature = @"
using System;
using System.Runtime.InteropServices;

public class NativeMethods {
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern uint SetThreadExecutionState(uint esFlags);

    public const uint MOUSEEVENTF_MOVE = 0x0001;
    public const uint ES_CONTINUOUS = 0x80000000;
    public const uint ES_SYSTEM_REQUIRED = 0x00000001;
    public const uint ES_DISPLAY_REQUIRED = 0x00000002;
}
"@

Add-Type -TypeDefinition $signature

# Global state
$script:isPaused = $false
$script:rng = New-Object System.Random

# Create Context Menu
$contextMenu = New-Object System.Windows.Forms.ContextMenuStrip
$startItem = $contextMenu.Items.Add("Start")
$pauseItem = $contextMenu.Items.Add("Pause")
$contextMenu.Items.Add("-") | Out-Null
$exitItem = $contextMenu.Items.Add("Exit")

# Create Tray Icon
$trayIcon = New-Object System.Windows.Forms.NotifyIcon
$trayIcon.ContextMenuStrip = $contextMenu
$trayIcon.Visible = $true
$trayIcon.Text = "Invisible Mouse Mover (PowerShell)"

# Helper to create a simple circle icon
function Get-CircleIcon {
    param([System.Drawing.Color]$color)
    $bitmap = New-Object System.Drawing.Bitmap(32, 32)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $brush = New-Object System.Drawing.SolidBrush($color)
    $g.FillEllipse($brush, 0, 0, 31, 31)
    $icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
    $brush.Dispose()
    $g.Dispose()
    return $icon
}

# Update State Function
function Update-State {
    param([bool]$active)
    $script:isPaused = -not $active
    
    if ($active) {
        [NativeMethods]::SetThreadExecutionState([NativeMethods]::ES_CONTINUOUS -bor [NativeMethods]::ES_SYSTEM_REQUIRED -bor [NativeMethods]::ES_DISPLAY_REQUIRED)
        $trayIcon.Icon = Get-CircleIcon ([System.Drawing.Color]::LimeGreen)
        $trayIcon.Text = "Invisible Mouse Mover (Running)"
        $startItem.Enabled = $false
        $pauseItem.Enabled = $true
    } else {
        [NativeMethods]::SetThreadExecutionState([NativeMethods]::ES_CONTINUOUS)
        $trayIcon.Icon = Get-CircleIcon ([System.Drawing.Color]::Red)
        $trayIcon.Text = "Invisible Mouse Mover (Paused)"
        $startItem.Enabled = $true
        $pauseItem.Enabled = $false
    }
}

# Event Handlers
$startItem.add_Click({ Update-State -active $true })
$pauseItem.add_Click({ Update-State -active $false })
$exitItem.add_Click({
    $trayIcon.Visible = $false
    $timer.Stop()
    [System.Windows.Forms.Application]::Exit()
})

# Timer for Mouse Movement
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 1000 # Check every second
$timer.add_Tick({
    if (-not $script:isPaused) {
        if ($script:nextMoveCounter -le 0) {
            # Move Mouse
            [NativeMethods]::mouse_event([NativeMethods]::MOUSEEVENTF_MOVE, 1, 0, 0, [UIntPtr]::Zero)
            [NativeMethods]::mouse_event([NativeMethods]::MOUSEEVENTF_MOVE, -1, 0, 0, [UIntPtr]::Zero)
            
            # Reset counter
            $script:nextMoveCounter = $script:rng.Next(30, 91)
        } else {
            $script:nextMoveCounter--
        }
    }
})

# Initialize
$script:nextMoveCounter = 0
Update-State -active $true
$timer.Start()

# Run Application
[System.Windows.Forms.Application]::Run()
