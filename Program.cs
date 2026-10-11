using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace JulianBar;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Single instance only, like a menu bar app.
        using var mutex = new Mutex(true, "JulianBar.SingleInstance", out bool isNew);
        if (!isNew) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new BarWidget());
    }
}

// Borderless always-on-top widget that docks on the taskbar, left of the system tray / clock.
sealed class BarWidget : Form
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "JulianBar";
    const string ThemeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? title);
    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    static readonly IntPtr HWND_TOPMOST = new(-1);
    const uint SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
    const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

    readonly System.Windows.Forms.Timer dateTimer;
    readonly System.Windows.Forms.Timer dockTimer;
    readonly ToolStripMenuItem startupItem;
    string text = "";
    string tooltip = "";
    readonly ToolTip tip = new();

    public BarWidget()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;

        startupItem = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = IsStartupEnabled() };
        startupItem.CheckedChanged += (_, _) => SetStartup(startupItem.Checked);
        var menu = new ContextMenuStrip();
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit App", null, (_, _) => Close());
        ContextMenuStrip = menu;

        Refresh_();
        Dock_();

        // Same as the Mac app: re-check every 60 seconds.
        dateTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        dateTimer.Tick += (_, _) => Refresh_();
        dateTimer.Start();

        // The taskbar can reclaim z-order or move/resize, so re-dock every couple of seconds.
        dockTimer = new System.Windows.Forms.Timer { Interval = 2_000 };
        dockTimer.Tick += (_, _) => Dock_();
        dockTimer.Start();

        SystemEvents.TimeChanged += (_, _) => Refresh_();
        SystemEvents.PowerModeChanged += (_, _) => Refresh_();
        SystemEvents.UserPreferenceChanged += (_, _) => Invalidate();
        SystemEvents.DisplaySettingsChanged += (_, _) => Dock_();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE; // no Alt-Tab entry, never steals focus
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    // Zero-padded YYDDD, e.g. 26005.
    static string FormatDate(DateTime now) =>
        (now.Year % 100).ToString("00", CultureInfo.InvariantCulture) +
        now.DayOfYear.ToString("000", CultureInfo.InvariantCulture);

    void Refresh_()
    {
        var now = DateTime.Now;
        var t = FormatDate(now);
        tooltip = now.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);
        tip.SetToolTip(this, tooltip);
        if (t == text) return;
        text = t;
        Invalidate();
    }

    void Dock_()
    {
        var shell = FindWindow("Shell_TrayWnd", null);
        if (shell == IntPtr.Zero || !GetWindowRect(shell, out var bar))
        {
            // Fallback: bottom-right of primary screen.
            var wa = Screen.PrimaryScreen!.Bounds;
            Place(wa.Right - 260, wa.Bottom - 40, 80, 40);
            return;
        }

        int barH = bar.Bottom - bar.Top;
        int barW = bar.Right - bar.Left;
        bool horizontal = barW >= barH;

        using var g = CreateGraphics();
        using var font = MakeFont();
        var size = TextRenderer.MeasureText(g, string.IsNullOrEmpty(text) ? "00000" : text, font);
        int w = size.Width + 20;

        if (!horizontal)
        {
            // Vertical taskbar (left/right): sit at the bottom of the bar, above the tray.
            Place(bar.Left, bar.Bottom - 120, barW, 28);
            return;
        }

        // Anchor to the left edge of the system tray area (clock / icons).
        int right = bar.Right;
        var tray = FindWindowEx(shell, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray != IntPtr.Zero && GetWindowRect(tray, out var t) && t.Left > bar.Left)
            right = t.Left;
        else
            right = bar.Right - 240;

        Place(right - w - 4, bar.Top, w, barH);
    }

    void Place(int x, int y, int w, int h)
    {
        SetWindowPos(Handle, HWND_TOPMOST, x, y, w, h, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    static Font MakeFont() => new("Segoe UI Semibold", 11f, FontStyle.Regular, GraphicsUnit.Point);

    static bool SystemIsLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ThemeKey);
        return key?.GetValue("SystemUsesLightTheme") is int v && v == 1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        bool light = SystemIsLight();
        var bg = light ? Color.FromArgb(0xF3, 0xF3, 0xF3) : Color.FromArgb(0x1C, 0x1C, 0x1C);
        var fg = light ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.White;
        e.Graphics.Clear(bg);
        using var font = MakeFont();
        TextRenderer.DrawText(e.Graphics, text, font, ClientRectangle, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunName) != null;
    }

    static void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key == null) return;
        if (enable) key.SetValue(RunName, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(RunName, throwOnMissingValue: false);
    }
}
