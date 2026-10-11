using System.Drawing.Drawing2D;
using System.Drawing.Text;
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
        Application.Run(new TrayContext());
    }
}

sealed class TrayContext : ApplicationContext
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "JulianBar";

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyIcon(IntPtr handle);

    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer timer;
    readonly ToolStripMenuItem startupItem;
    string lastText = "";

    public TrayContext()
    {
        startupItem = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = IsStartupEnabled() };
        startupItem.CheckedChanged += (_, _) => SetStartup(startupItem.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit App", null, (_, _) => ExitThread());

        tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };

        Refresh();

        // Same as the Mac app: re-check every 60 seconds.
        timer = new System.Windows.Forms.Timer { Interval = 60_000 };
        timer.Tick += (_, _) => Refresh();
        timer.Start();

        // Update right away after sleep/resume or a clock change.
        SystemEvents.TimeChanged += (_, _) => Refresh();
        SystemEvents.PowerModeChanged += (_, _) => Refresh();
    }

    // Matches DateFormatter "yyD": 2-digit year + day of year (not zero-padded).
    static string FormatDate(DateTime now) =>
        (now.Year % 100).ToString("00", CultureInfo.InvariantCulture) +
        now.DayOfYear.ToString(CultureInfo.InvariantCulture);

    void Refresh()
    {
        var now = DateTime.Now;
        var text = FormatDate(now);
        if (text == lastText) return;
        lastText = text;

        var old = tray.Icon;
        tray.Icon = RenderIcon(now.Year % 100, now.DayOfYear);
        tray.Text = $"{text}  ({now:dddd, MMMM d, yyyy})";
        if (old != null) { DestroyIcon(old.Handle); old.Dispose(); }
    }

    // Tray icons are tiny, so stack the year over the day of year.
    static Icon RenderIcon(int yy, int doy)
    {
        const int size = 32; // Windows downscales for 100% DPI
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var font = new Font("Segoe UI", 14.5f, FontStyle.Bold, GraphicsUnit.Pixel);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var brush = new SolidBrush(Color.White);

            g.DrawString(yy.ToString("00", CultureInfo.InvariantCulture), font, brush, new RectangleF(0, 0, size, size / 2f), fmt);
            g.DrawString(doy.ToString(CultureInfo.InvariantCulture), font, brush, new RectangleF(0, size / 2f - 1, size, size / 2f), fmt);
        }
        IntPtr h = bmp.GetHicon();
        using var temp = Icon.FromHandle(h);
        var icon = (Icon)temp.Clone();
        DestroyIcon(h);
        return icon;
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

    protected override void ExitThreadCore()
    {
        timer.Stop();
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}
