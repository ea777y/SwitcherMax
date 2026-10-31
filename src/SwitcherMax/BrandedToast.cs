using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SwitcherMax;

// Small, branded, non-activating WinForms notification. Unlike a Shell balloon,
// the displayed logo is controlled by SwitcherMax even on Windows 11.
// Intentionally does NOT create a Windows Action Center notification.
internal sealed class BrandedToast : Form
{
    private readonly System.Windows.Forms.Timer _dismissTimer;
    private readonly PictureBox _logo;

    public BrandedToast(string message, Icon appIcon)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.FromArgb(31, 31, 31);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(352, 106);

        _logo = new PictureBox
        {
            Location = new Point(16, 17),
            Size = new Size(40, 40),
            Image = appIcon.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        var heading = new Label
        {
            Text = "SwitcherMax",
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Location = new Point(68, 18),
            Size = new Size(270, 25)
        };
        var body = new Label
        {
            Text = message.Length > 150 ? message[..147] + "..." : message,
            ForeColor = Color.FromArgb(225, 225, 225),
            BackColor = Color.Transparent,
            Location = new Point(68, 46),
            Size = new Size(270, 48),
            AutoEllipsis = true
        };
        Controls.Add(_logo);
        Controls.Add(heading);
        Controls.Add(body);

        // A click anywhere on the notification dismisses it.
        Click += (_, _) => Close();
        _logo.Click += (_, _) => Close();
        heading.Click += (_, _) => Close();
        body.Click += (_, _) => Close();

        _dismissTimer = new System.Windows.Forms.Timer { Interval = 4500 };
        _dismissTimer.Tick += (_, _) => Close();
        Shown += (_, _) => _dismissTimer.Start();

        // Stay inside the current monitor's usable desktop area.
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
    }

    // Do not take keyboard focus away from the user's editor / hotkey target.
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return parameters;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Rounded popup, aligned with the Windows 11 visual style.
        using var path = new GraphicsPath();
        const int diameter = 22;
        var r = new Rectangle(0, 0, Width, Height);
        path.AddArc(r.X, r.Y, diameter, diameter, 180, 90);
        path.AddArc(r.Right - diameter, r.Y, diameter, diameter, 270, 90);
        path.AddArc(r.Right - diameter, r.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(r.X, r.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        Region = new Region(path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dismissTimer.Stop();
            _dismissTimer.Dispose();
            _logo.Image?.Dispose();
            Region?.Dispose();
        }
        base.Dispose(disposing);
    }
}
