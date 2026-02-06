using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace TrayApp
{
    class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem screenshotMenuItem;
        private ToolStripMenuItem defaultClickItem;

        public TrayApplicationContext()
        {
            trayMenu = new ContextMenuStrip();

            screenshotMenuItem = new ToolStripMenuItem("Screenshot");

            // Menüpunkte für jeden Monitor hinzufügen
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                int index = i;
                screenshotMenuItem.DropDownItems.Add(
                    $"Monitor {i + 1}",
                    null,
                    (s, e) => OnScreenshot(index)
                );
            }

            // Option für alle Monitore
            screenshotMenuItem.DropDownItems.Add(
                "Alle Monitore",
                null,
                (s, e) => OnScreenshot(-1)
            );

            // Option für Standard-Linksklick
            defaultClickItem = new ToolStripMenuItem("Standard-Linksklick festlegen");

            var defaultClickAllMonitors = new ToolStripMenuItem("Alle Monitore", null, (s, e) => SetDefaultClickAction(-1));
            var defaultClickPrimaryMonitor = new ToolStripMenuItem("Primärer Monitor", null, (s, e) => SetDefaultClickAction(0));

            defaultClickItem.DropDownItems.Add(defaultClickAllMonitors);
            defaultClickItem.DropDownItems.Add(defaultClickPrimaryMonitor);

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
            trayMenu.Items.Add("Beenden", null, OnExit);

            trayIcon = new NotifyIcon
            {
                Text = "Screenshot-Tool",
                Icon = LoadIconFromResource(),
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            //defaultClickAction = -1; // Standardmäßig alle Monitore
            defaultClickAction = 0; // Standardmäßig primärer Monitor
            UpdateDefaultClickCheckmarks();

            trayIcon.MouseClick += TrayIcon_MouseClick;
        }

        private int defaultClickAction = -1;

        private void SetDefaultClickAction(int monitorIndex)
        {
            defaultClickAction = monitorIndex;
            UpdateDefaultClickCheckmarks();
        }

        private void UpdateDefaultClickCheckmarks()
        {
            foreach (ToolStripMenuItem item in defaultClickItem.DropDownItems)
            {
                item.Checked = false;

                // Determine which menu item corresponds to the current defaultClickAction
                if ((defaultClickAction == -1 && item.Text == "Alle Monitore") ||
                    (defaultClickAction == 0 && item.Text == "Primärer Monitor") ||
                    (defaultClickAction > 0 && item.Text == $"Monitor {defaultClickAction + 1}"))
                {
                    item.Checked = true;
                }
            }
        }

        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OnScreenshot(defaultClickAction);
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
                    "Screenshot erstellt",
                    Path.GetFileName(fileName),
                    ToolTipIcon.Info
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Fehler");
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
                throw new Exception("Icon-Resource nicht gefunden");

            return new Icon(stream);
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
