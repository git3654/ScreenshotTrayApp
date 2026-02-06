using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TrayApp
{
    public class IniFile
    {
        private string path;

        public IniFile(string iniPath)
        {
            path = iniPath;
        }

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string value, string filePath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder retVal, int size, string filePath);

        public void WriteValue(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, path);
        }

        public string ReadValue(string section, string key)
        {
            StringBuilder temp = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", temp, 255, path);
            return temp.ToString();
        }
    }

    class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem screenshotMenuItem;
        private ToolStripMenuItem defaultClickItem;
        private int defaultClickAction;
        private IniFile settingsFile;
        private string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");

        public TrayApplicationContext()
        {
            settingsFile = new IniFile(settingsPath);
            string savedAction = settingsFile.ReadValue("Settings", "DefaultClickAction");
            defaultClickAction = string.IsNullOrEmpty(savedAction) ? 0 : int.Parse(savedAction);

            trayMenu = new ContextMenuStrip();

            screenshotMenuItem = new ToolStripMenuItem("Screenshot");

            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                int index = i;
                screenshotMenuItem.DropDownItems.Add(
                    $"Monitor {i + 1}",
                    null,
                    (s, e) => OnScreenshot(index)
                );
            }

            screenshotMenuItem.DropDownItems.Add(
                "All Monitors",
                null,
                (s, e) => OnScreenshot(-1)
            );

            screenshotMenuItem.DropDownItems.Add(
                "Select Region",
                null,
                (s, e) => OnScreenshotRegion()
            );

            defaultClickItem = new ToolStripMenuItem("Set Default Left-Click Action");

            var defaultClickAllMonitors = new ToolStripMenuItem("All Monitors", null, (s, e) => SetDefaultClickAction(-1));
            var defaultClickPrimaryMonitor = new ToolStripMenuItem("Primary Monitor", null, (s, e) => SetDefaultClickAction(0));
            var defaultClickRegion = new ToolStripMenuItem("Select Region", null, (s, e) => SetDefaultClickAction(-3));

            defaultClickItem.DropDownItems.Add(defaultClickAllMonitors);
            defaultClickItem.DropDownItems.Add(defaultClickPrimaryMonitor);
            defaultClickItem.DropDownItems.Add(defaultClickRegion);

            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                int index = i;
                defaultClickItem.DropDownItems.Add(
                    $"Monitor {i + 1}",
                    null,
                    (s, e) => SetDefaultClickAction(index)
                );
            }

            trayMenu.Items.Add(screenshotMenuItem);
            trayMenu.Items.Add(defaultClickItem);
            trayMenu.Items.Add("Exit", null, OnExit);

            trayIcon = new NotifyIcon
            {
                Text = "Screenshot Tool",
                Icon = LoadIconFromResource(),
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            UpdateDefaultClickCheckmarks();
            trayIcon.MouseClick += TrayIcon_MouseClick;
        }

        private void SetDefaultClickAction(int monitorIndex)
        {
            defaultClickAction = monitorIndex;
            settingsFile.WriteValue("Settings", "DefaultClickAction", monitorIndex.ToString());
            UpdateDefaultClickCheckmarks();
        }

        private void UpdateDefaultClickCheckmarks()
        {
            foreach (ToolStripMenuItem item in defaultClickItem.DropDownItems)
            {
                item.Checked = false;

                if ((defaultClickAction == -1 && item.Text == "All Monitors") ||
                    (defaultClickAction == 0 && item.Text == "Primary Monitor") ||
                    (defaultClickAction > 0 && item.Text == $"Monitor {defaultClickAction + 1}") ||
                    (defaultClickAction == -3 && item.Text == "Select Region"))
                {
                    item.Checked = true;
                }
            }
        }

        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                switch (defaultClickAction)
                {
                    case -1: OnScreenshot(-1); break;
                    case -3: OnScreenshotRegion(); break;
                    default: OnScreenshot(defaultClickAction); break;
                }
            }
        }

        private void OnScreenshot(int monitorIndex)
        {
            try
            {
                string screenshotPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Screenshots"
                );

                Directory.CreateDirectory(screenshotPath);

                string fileName = Path.Combine(
                    screenshotPath,
                    $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"
                );

                Rectangle bounds = monitorIndex == -1
                    ? SystemInformation.VirtualScreen
                    : Screen.AllScreens[monitorIndex].Bounds;

                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    bitmap.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
                }

                trayIcon.ShowBalloonTip(
                    1500,
                    "Screenshot created",
                    Path.GetFileName(fileName),
                    ToolTipIcon.Info
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void OnScreenshotRegion()
        {
            try
            {
                using (var regionForm = new RegionSelectionForm())
                {
                    if (regionForm.ShowDialog() == DialogResult.OK)
                    {
                        Rectangle selectedRegion = regionForm.SelectedRegion;

                        string screenshotPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                            "Screenshots"
                        );

                        Directory.CreateDirectory(screenshotPath);

                        string fileName = Path.Combine(
                            screenshotPath,
                            $"Screenshot_Region_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"
                        );

                        using (Bitmap bitmap = new Bitmap(selectedRegion.Width, selectedRegion.Height))
                        using (Graphics graphics = Graphics.FromImage(bitmap))
                        {
                            graphics.CopyFromScreen(selectedRegion.Location, Point.Empty, selectedRegion.Size);
                            bitmap.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
                        }

                        trayIcon.ShowBalloonTip(
                            1500,
                            "Screenshot created",
                            Path.GetFileName(fileName),
                            ToolTipIcon.Info
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            ExitThread();
        }

        private Icon LoadIconFromResource()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream("TrayApp.Assets.tray.ico");

            if (stream == null)
                throw new Exception("Icon resource not found");

            return new Icon(stream);
        }
    }

    public class RegionSelectionForm : Form
    {
        public Rectangle SelectedRegion { get; private set; }
        private Point startPoint;
        private Point endPoint;
        private bool isSelecting;

        public RegionSelectionForm()
        {
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.Opacity = 0.5;
            this.Cursor = Cursors.Cross;

            this.MouseDown += (s, e) =>
            {
                isSelecting = true;
                startPoint = e.Location;
            };

            this.MouseMove += (s, e) =>
            {
                if (isSelecting)
                {
                    endPoint = e.Location;
                    this.Invalidate();
                }
            };

            this.MouseUp += (s, e) =>
            {
                if (isSelecting)
                {
                    isSelecting = false;
                    endPoint = e.Location;
                    SelectedRegion = new Rectangle(
                        Math.Min(startPoint.X, endPoint.X),
                        Math.Min(startPoint.Y, endPoint.Y),
                        Math.Abs(endPoint.X - startPoint.X),
                        Math.Abs(endPoint.Y - startPoint.Y)
                    );
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            };

            this.Paint += (s, e) =>
            {
                if (isSelecting)
                {
                    using (Pen pen = new Pen(Color.Red, 2))
                    {
                        e.Graphics.DrawRectangle(
                            pen,
                            Math.Min(startPoint.X, endPoint.X),
                            Math.Min(startPoint.Y, endPoint.Y),
                            Math.Abs(endPoint.X - startPoint.X),
                            Math.Abs(endPoint.Y - startPoint.Y)
                        );
                    }
                }
            };
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApplicationContext());
        }
    }
}


/* 
Projekt bauen (Test)    dotnet build
Starten zum Test:       dotnet run
single file             dotnet publish -c Release
*/
