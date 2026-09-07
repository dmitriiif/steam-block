using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SteamBlock
{
    internal sealed class SteamBlockConfig
    {
        public string BlockStart { get; set; }
        public string BlockEnd { get; set; }
        public int CheckIntervalSeconds { get; set; }
        public string TargetUserSid { get; set; }
        public string SteamPath { get; set; }
        public string[] SteamProcessNames { get; set; }
        public string[] GameDirectories { get; set; }
        public string[] ExtraExecutables { get; set; }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private const string TaskName = "SteamCurfew";
        private const string InstallDirectory = @"C:\ProgramData\SteamCurfew";
        private readonly string configPath = Path.Combine(InstallDirectory, "config.json");
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly Timer refreshTimer = new Timer();

        private SteamBlockConfig config;
        private DateTimePicker startPicker;
        private DateTimePicker endPicker;
        private Panel statusPanel;
        private Label statusTitle;
        private Label statusDetail;
        private Button toggleButton;
        private Button saveButton;

        public MainForm()
        {
            Text = "Steam Block";
            ClientSize = new Size(480, 415);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(246, 248, 251);

            refreshTimer.Interval = 5000;
            refreshTimer.Tick += delegate { RefreshStatus(); };
            refreshTimer.Start();

            ShowAppropriateScreen();
        }

        private static Label MakeLabel(string text, float size, FontStyle style, int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new Font("Segoe UI", size, style);
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private static Button MakeButton(string text, int x, int y, int width, int height)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.Cursor = Cursors.Hand;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void ShowAppropriateScreen()
        {
            if (File.Exists(configPath) && GetTask() != null)
                ShowControlScreen();
            else
                ShowInstallScreen();
        }

        private void AddHeader()
        {
            Controls.Add(MakeLabel("Steam Block", 22F, FontStyle.Bold, 24, 15, 300, 48));
            Controls.Add(MakeLabel("Set a daily stopping time for Steam", 10F, FontStyle.Regular, 27, 56, 400, 28));
        }

        private void ShowInstallScreen()
        {
            Controls.Clear();
            ClientSize = new Size(480, 315);
            AddHeader();

            Panel card = new Panel();
            card.Location = new Point(24, 100);
            card.Size = new Size(432, 105);
            card.BackColor = Color.FromArgb(255, 241, 214);
            card.Controls.Add(MakeLabel("Setup needed", 18F, FontStyle.Bold, 17, 12, 390, 38));
            card.Controls.Add(MakeLabel("Install once, then choose your hours and turn protection on.", 10F, FontStyle.Regular, 18, 50, 390, 42));
            Controls.Add(card);

            Button installButton = MakeButton("Install Steam Block", 24, 226, 432, 52);
            installButton.BackColor = Color.FromArgb(32, 102, 190);
            installButton.ForeColor = Color.White;
            installButton.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
            installButton.Click += delegate
            {
                installButton.Enabled = false;
                installButton.Text = "Installing...";
                Cursor = Cursors.WaitCursor;
                Application.DoEvents();
                try
                {
                    RunInstaller();
                    ShowControlScreen();
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, exception.Message, "Steam Block could not be installed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    installButton.Enabled = true;
                    installButton.Text = "Try installation again";
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            };
            Controls.Add(installButton);
        }

        private void ShowControlScreen()
        {
            config = LoadConfig();
            Controls.Clear();
            ClientSize = new Size(480, 415);
            AddHeader();

            statusPanel = new Panel();
            statusPanel.Location = new Point(24, 94);
            statusPanel.Size = new Size(432, 92);
            statusTitle = MakeLabel("", 18F, FontStyle.Bold, 18, 8, 395, 40);
            statusDetail = MakeLabel("", 10F, FontStyle.Regular, 19, 45, 395, 32);
            statusPanel.Controls.Add(statusTitle);
            statusPanel.Controls.Add(statusDetail);
            Controls.Add(statusPanel);

            Controls.Add(MakeLabel("Block every day from", 10F, FontStyle.Regular, 25, 208, 165, 32));
            startPicker = CreateTimePicker(config.BlockStart, 185, 208);
            Controls.Add(startPicker);
            Controls.Add(MakeLabel("until", 10F, FontStyle.Regular, 287, 208, 42, 32));
            endPicker = CreateTimePicker(config.BlockEnd, 329, 208);
            Controls.Add(endPicker);

            saveButton = MakeButton("Save times", 24, 263, 132, 43);
            saveButton.BackColor = Color.FromArgb(225, 230, 237);
            saveButton.Click += delegate { SaveTimesWithMessage(); };
            Controls.Add(saveButton);

            toggleButton = MakeButton("", 168, 263, 288, 43);
            toggleButton.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            toggleButton.Click += delegate { ToggleProtection(); };
            Controls.Add(toggleButton);

            Label note = MakeLabel("You can close this window. Protection keeps running quietly.", 9F, FontStyle.Regular, 25, 319, 425, 30);
            note.ForeColor = Color.FromArgb(90, 96, 105);
            Controls.Add(note);

            Button uninstallButton = MakeButton("Uninstall Steam Block", 296, 359, 160, 32);
            uninstallButton.BackColor = Color.FromArgb(235, 238, 242);
            uninstallButton.ForeColor = Color.FromArgb(90, 96, 105);
            uninstallButton.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            uninstallButton.Click += delegate { UninstallApplication(); };
            Controls.Add(uninstallButton);

            RefreshStatus();
        }

        private static DateTimePicker CreateTimePicker(string value, int x, int y)
        {
            DateTimePicker picker = new DateTimePicker();
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = "HH:mm";
            picker.ShowUpDown = true;
            picker.Location = new Point(x, y);
            picker.Size = new Size(88, 32);
            picker.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            picker.Value = DateTime.Today.Add(DateTime.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture).TimeOfDay);
            return picker;
        }

        private SteamBlockConfig LoadConfig()
        {
            return serializer.Deserialize<SteamBlockConfig>(File.ReadAllText(configPath, Encoding.UTF8));
        }

        private void SaveTimes()
        {
            string start = startPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            string end = endPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (start == end) throw new InvalidOperationException("Choose two different times.");

            config.BlockStart = start;
            config.BlockEnd = end;
            string temporaryPath = Path.Combine(InstallDirectory, ".config-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporaryPath, serializer.Serialize(config), new UTF8Encoding(false));
                File.Replace(temporaryPath, configPath, null, true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private void SaveTimesWithMessage()
        {
            try
            {
                SaveTimes();
                RefreshStatus();
                MessageBox.Show(this, "Your new blocking times are saved.", "Steam Block", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Could not save the times", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleProtection()
        {
            try
            {
                SaveTimes();
                dynamic task = GetTask();
                if (task == null) throw new InvalidOperationException("The background task is missing. Reinstall Steam Block.");

                if ((bool)task.Enabled)
                {
                    task.Stop(0);
                    task.Enabled = false;
                }
                else
                {
                    task.Enabled = true;
                    task.Run(null);
                }
                ReleaseComObject(task);
                RefreshStatus();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Could not change protection", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UninstallApplication()
        {
            DialogResult choice = MessageBox.Show(
                this,
                "This will turn protection off and completely remove Steam Block from this computer.\n\nDo you want to continue?",
                "Uninstall Steam Block?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2
            );
            if (choice != DialogResult.Yes) return;

            try
            {
                string uninstaller = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uninstall-SteamCurfew.ps1");
                if (!File.Exists(uninstaller)) uninstaller = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app", "Uninstall-SteamCurfew.ps1");
                if (!File.Exists(uninstaller)) throw new FileNotFoundException("The uninstall helper could not be found.");

                RunPowerShellScript(uninstaller, "-RemoveFiles -WaitForProcessId " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
                MessageBox.Show(this, "Steam Block has been turned off and will finish removing itself when this window closes.", "Uninstall started", MessageBoxButtons.OK, MessageBoxIcon.Information);
                refreshTimer.Stop();
                Application.Exit();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Could not uninstall Steam Block", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshStatus()
        {
            if (statusPanel == null || statusPanel.IsDisposed) return;
            try
            {
                config = LoadConfig();
                dynamic task = GetTask();
                bool enabled = task != null && (bool)task.Enabled;
                ReleaseComObject(task);

                if (enabled)
                {
                    statusPanel.BackColor = Color.FromArgb(215, 244, 226);
                    statusTitle.ForeColor = Color.FromArgb(20, 106, 62);
                    statusTitle.Text = "PROTECTION IS ON";
                    statusDetail.Text = IsBlockedNow(config) ? "Steam is blocked right now." : "Steam will be blocked at " + config.BlockStart + ".";
                    toggleButton.Text = "Turn protection OFF";
                    toggleButton.BackColor = Color.FromArgb(196, 57, 57);
                    toggleButton.ForeColor = Color.White;
                }
                else
                {
                    statusPanel.BackColor = Color.FromArgb(255, 224, 224);
                    statusTitle.ForeColor = Color.FromArgb(159, 37, 37);
                    statusTitle.Text = "PROTECTION IS OFF";
                    statusDetail.Text = "Steam is allowed at all times.";
                    toggleButton.Text = "Turn protection ON";
                    toggleButton.BackColor = Color.FromArgb(31, 139, 82);
                    toggleButton.ForeColor = Color.White;
                }
            }
            catch (Exception exception)
            {
                statusPanel.BackColor = Color.FromArgb(255, 241, 214);
                statusTitle.ForeColor = Color.FromArgb(120, 75, 0);
                statusTitle.Text = "NEEDS ATTENTION";
                statusDetail.Text = exception.Message;
            }
        }

        private static bool IsBlockedNow(SteamBlockConfig currentConfig)
        {
            TimeSpan start = DateTime.ParseExact(currentConfig.BlockStart, "HH:mm", CultureInfo.InvariantCulture).TimeOfDay;
            TimeSpan end = DateTime.ParseExact(currentConfig.BlockEnd, "HH:mm", CultureInfo.InvariantCulture).TimeOfDay;
            TimeSpan now = DateTime.Now.TimeOfDay;
            if (start < end) return now >= start && now < end;
            return now >= start || now < end;
        }

        private static dynamic GetTask()
        {
            dynamic service = null;
            dynamic folder = null;
            try
            {
                Type serviceType = Type.GetTypeFromProgID("Schedule.Service");
                service = Activator.CreateInstance(serviceType);
                service.Connect();
                folder = service.GetFolder("\\");
                return folder.GetTask(TaskName);
            }
            catch (COMException)
            {
                return null;
            }
            finally
            {
                ReleaseComObject(folder);
                ReleaseComObject(service);
            }
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }

        private static void RunInstaller()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string installer = Path.Combine(baseDirectory, "Install-SteamCurfew.ps1");
            if (!File.Exists(installer)) installer = Path.Combine(baseDirectory, "app", "Install-SteamCurfew.ps1");
            if (!File.Exists(installer)) throw new FileNotFoundException("The app folder is missing. Extract the complete download before installing.");

            RunPowerShellScript(installer, "-NoStart");
        }

        private static void RunPowerShellScript(string scriptPath, string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
            info.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + scriptPath + "\" " + arguments;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
            }
        }
    }
}
