// Zoomy - a tiny "zoom in on part of the screen" tool for Windows.
//
// Pick "Zoom in on screen" from the desktop / folder right-click menu (or press
// Ctrl+Alt+Z), drag a box over anything, and a small window pops up showing that
// area blown up. Right-click the window to save or copy it, click X to close.
//
// Written for the C# 5 compiler that ships with every Windows 10 install, so it
// can be built with build.bat and nothing else.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Zoomy")]
[assembly: AssemblyProduct("Zoomy")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace Zoomy
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            string cmd = args.Length > 0 ? args[0].Trim().TrimStart('-', '/').ToLowerInvariant() : "";

            Native.MakeDpiAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (cmd == "install")
            {
                Setup.Install(true);
                return 0;
            }
            if (cmd == "uninstall")
            {
                Setup.Uninstall();
                return 0;
            }

            // Double-clicking the exe before it has been set up: offer to set it up.
            if (cmd == "" && !Setup.IsInstalled())
            {
                DialogResult answer = MessageBox.Show(
                    "Add \"Zoom in on screen\" to your right-click menu and set up the Ctrl+Alt+Z shortcut?\n\n" +
                    "(Choose No to just zoom once.)",
                    "Zoomy", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) return 0;
                if (answer == DialogResult.Yes)
                {
                    Setup.Install(true);
                    return 0;
                }
            }

            // Launched from the context menu: give the menu a moment to fade away
            // so it doesn't end up in the screenshot.
            if (cmd == "menu") Thread.Sleep(250);

            Rectangle virt = SystemInformation.VirtualScreen;
            Bitmap shot = new Bitmap(virt.Width, virt.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(shot))
            {
                g.CopyFromScreen(virt.Location, Point.Empty, virt.Size);
            }

            Rectangle sel;
            using (SelectForm picker = new SelectForm(shot, virt))
            {
                Application.Run(picker);
                sel = picker.Selection;
            }
            if (sel.IsEmpty) return 0;

            Bitmap crop = shot.Clone(sel, shot.PixelFormat);
            shot.Dispose();

            Rectangle onScreen = new Rectangle(sel.X + virt.X, sel.Y + virt.Y, sel.Width, sel.Height);
            Application.Run(new ZoomForm(crop, onScreen));
            return 0;
        }
    }

    // Full-screen, borderless window showing a frozen screenshot (not dimmed)
    // that you drag a box on.
    class SelectForm : Form
    {
        readonly Bitmap shot;
        Point start;
        Rectangle box;
        bool dragging;

        // The chosen area in screenshot coordinates, or empty if cancelled.
        public Rectangle Selection;

        public SelectForm(Bitmap shot, Rectangle virt)
        {
            this.shot = shot;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = virt;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            Text = "Zoomy";
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Everything is painted in OnPaint; skipping this avoids flicker.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle r = e.ClipRectangle;
            g.DrawImage(shot, r, r, GraphicsUnit.Pixel);

            if (dragging && box.Width > 0 && box.Height > 0)
            {
                Rectangle outline = new Rectangle(box.X, box.Y, box.Width - 1, box.Height - 1);
                using (Pen light = new Pen(Color.White))
                using (Pen dark = new Pen(Color.Black))
                {
                    dark.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(light, outline);
                    g.DrawRectangle(dark, outline);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                start = e.Location;
                box = new Rectangle(start, Size.Empty);
            }
            else
            {
                Close(); // any other button cancels
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;

            Rectangle old = box;
            int x1 = Math.Max(0, Math.Min(start.X, e.X));
            int y1 = Math.Max(0, Math.Min(start.Y, e.Y));
            int x2 = Math.Min(ClientSize.Width, Math.Max(start.X, e.X) + 1);
            int y2 = Math.Min(ClientSize.Height, Math.Max(start.Y, e.Y) + 1);
            box = Rectangle.FromLTRB(x1, y1, x2, y2);

            Rectangle dirty = Rectangle.Union(old, box);
            dirty.Inflate(2, 2);
            Invalidate(dirty);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging || e.Button != MouseButtons.Left) return;
            dragging = false;
            if (box.Width >= 3 && box.Height >= 3) Selection = box;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
        }
    }

    // The little window showing the zoomed-in area. Only an X button; right-click
    // for Save / Copy.
    class ZoomForm : Form
    {
        readonly Bitmap image;
        bool smooth;
        MenuItem smoothItem;

        public ZoomForm(Bitmap image, Rectangle onScreen)
        {
            this.image = image;
            AutoScaleMode = AutoScaleMode.None;
            Text = "Zoomy";
            ShowIcon = false;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            ResizeRedraw = true;
            KeyPreview = true;
            BackColor = Color.FromArgb(32, 32, 32);

            smoothItem = new MenuItem("Smooth scaling", delegate { ToggleSmooth(); });
            ContextMenu = new ContextMenu(new MenuItem[] {
                new MenuItem("Save image...", delegate { SaveImage(); }),
                new MenuItem("Copy image", delegate { CopyImage(); }),
                new MenuItem("-"),
                smoothItem,
                new MenuItem("-"),
                new MenuItem("Close", delegate { Close(); }),
            });

            // Pick a zoom: about 3x (more for tiny areas), but never bigger than
            // most of the screen the area came from.
            Rectangle work = Screen.FromRectangle(onScreen).WorkingArea;
            double fit = Math.Min(work.Width * 0.9 / image.Width, work.Height * 0.9 / image.Height);
            double want = Math.Max(3.0, 480.0 / Math.Max(image.Width, image.Height));
            double scale = Math.Min(fit, want);
            if (scale >= 2) scale = Math.Floor(scale); // whole numbers keep pixels crisp
            ClientSize = new Size((int)Math.Round(image.Width * scale), (int)Math.Round(image.Height * scale));

            // Centre it over the area that was selected, kept on screen.
            int x = onScreen.X + onScreen.Width / 2 - Width / 2;
            int y = onScreen.Y + onScreen.Height / 2 - Height / 2;
            x = Math.Max(work.Left, Math.Min(x, work.Right - Width));
            y = Math.Max(work.Top, Math.Min(y, work.Bottom - Height));
            Location = new Point(x, y);
        }

        // Where the image goes inside the window, keeping its shape.
        Rectangle ImageRect()
        {
            Size c = ClientSize;
            double s = Math.Min((double)c.Width / image.Width, (double)c.Height / image.Height);
            int w = Math.Max(1, (int)Math.Round(image.Width * s));
            int h = Math.Max(1, (int)Math.Round(image.Height * s));
            return new Rectangle((c.Width - w) / 2, (c.Height - h) / 2, w, h);
        }

        void DrawZoomed(Graphics g, Rectangle dest)
        {
            if (smooth)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            }
            else
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
            }
            using (ImageAttributes attrs = new ImageAttributes())
            {
                attrs.SetWrapMode(WrapMode.TileFlipXY); // no faded edges
                g.DrawImage(image, dest, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attrs);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawZoomed(e.Graphics, ImageRect());
        }

        // The image exactly as it's currently shown.
        Bitmap RenderZoomed()
        {
            Rectangle r = ImageRect();
            Bitmap bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                DrawZoomed(g, new Rectangle(0, 0, r.Width, r.Height));
            }
            return bmp;
        }

        void SaveImage()
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Save zoomed image";
                dlg.Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg|Bitmap image (*.bmp)|*.bmp";
                dlg.FileName = "zoomy_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                ImageFormat format = ImageFormat.Png;
                string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                if (ext == ".jpg" || ext == ".jpeg") format = ImageFormat.Jpeg;
                else if (ext == ".bmp") format = ImageFormat.Bmp;

                try
                {
                    using (Bitmap bmp = RenderZoomed())
                    {
                        bmp.Save(dlg.FileName, format);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Couldn't save the image:\n" + ex.Message, "Zoomy",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        void CopyImage()
        {
            using (Bitmap bmp = RenderZoomed())
            {
                try { Clipboard.SetImage(bmp); }
                catch (ExternalException) { } // clipboard busy; nothing useful to do
            }
        }

        void ToggleSmooth()
        {
            smooth = !smooth;
            smoothItem.Checked = smooth;
            Invalidate();
        }

        // Scroll wheel grows / shrinks the window around its centre.
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            double f = e.Delta > 0 ? 1.15 : 1 / 1.15;
            Rectangle work = Screen.FromControl(this).WorkingArea;
            Size c = ClientSize;
            int w = (int)Math.Round(c.Width * f);
            int h = (int)Math.Round(c.Height * f);
            if (w < 40 || h < 40) return;
            if (f > 1 && (w > work.Width || h > work.Height)) return;

            Point centre = new Point(Left + Width / 2, Top + Height / 2);
            ClientSize = new Size(w, h);
            Location = new Point(centre.X - Width / 2, centre.Y - Height / 2);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.Control && e.KeyCode == Keys.C) CopyImage();
            else if (e.Control && e.KeyCode == Keys.S) SaveImage();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) image.Dispose();
            base.Dispose(disposing);
        }
    }

    // Adds / removes the right-click menu entry and the Ctrl+Alt+Z shortcut.
    // Everything goes in the current user's settings, so no admin rights needed.
    static class Setup
    {
        const string MenuKey = @"Software\Classes\Directory\Background\shell\Zoomy";
        const string MenuText = "Zoom in on screen";
        const string Hotkey = "Ctrl+Alt+Z";

        static string ExePath
        {
            get { return Application.ExecutablePath; }
        }

        static string ShortcutPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Zoomy.lnk");
            }
        }

        static string MenuCommand
        {
            get { return "\"" + ExePath + "\" menu"; }
        }

        public static bool IsInstalled()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(MenuKey + @"\command"))
            {
                return k != null && string.Equals(k.GetValue("") as string, MenuCommand, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void Install(bool announce)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(MenuKey))
            {
                k.SetValue("", MenuText);
                k.SetValue("Icon", "\"" + ExePath + "\",0");
                using (RegistryKey c = k.CreateSubKey("command"))
                {
                    c.SetValue("", MenuCommand);
                }
            }

            string hotkeyNote;
            try
            {
                CreateShortcut();
                hotkeyNote = "\n\nYou can also press " + Hotkey + " anywhere.";
            }
            catch (Exception)
            {
                hotkeyNote = "";
            }

            if (announce)
            {
                MessageBox.Show(
                    "Done! Right-click the desktop (or the empty space in any folder) and choose \"" +
                    MenuText + "\"." + hotkeyNote +
                    "\n\nKeep Zoomy.exe where it is - if you move it, just run it again.",
                    "Zoomy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public static void Uninstall()
        {
            Registry.CurrentUser.DeleteSubKeyTree(MenuKey, false);
            try { File.Delete(ShortcutPath); }
            catch (Exception) { }
            MessageBox.Show("Zoomy has been removed from the right-click menu.", "Zoomy",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // A Start Menu shortcut with a hotkey: Windows itself listens for the key,
        // so Zoomy doesn't need to keep running in the background.
        static void CreateShortcut()
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            try
            {
                object lnk = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                    new object[] { ShortcutPath });
                Type lnkType = lnk.GetType();
                lnkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { ExePath });
                lnkType.InvokeMember("Arguments", BindingFlags.SetProperty, null, lnk, new object[] { "zoom" });
                lnkType.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { MenuText });
                lnkType.InvokeMember("Hotkey", BindingFlags.SetProperty, null, lnk, new object[] { Hotkey });
                lnkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
                Marshal.FinalReleaseComObject(lnk);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    static class Native
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("shcore.dll")]
        static extern int SetProcessDpiAwareness(int value);

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        // Without this, Windows' display scaling (125%, 150%...) would make the
        // screenshot blurry and the selection land in the wrong place.
        public static void MakeDpiAware()
        {
            try
            {
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; // per-monitor v2 (Win10 1703+)
            }
            catch (EntryPointNotFoundException) { }
            try
            {
                if (SetProcessDpiAwareness(2) == 0) return; // per-monitor (Win8.1+)
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            try { SetProcessDPIAware(); }
            catch (EntryPointNotFoundException) { }
        }
    }
}
