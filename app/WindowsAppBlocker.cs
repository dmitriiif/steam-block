using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WindowsAppBlocker
{
    internal sealed class AppBlockerConfig
    {
        public string BlockStart { get; set; }
        public string BlockEnd { get; set; }
        public int CheckIntervalSeconds { get; set; }
        public string TargetUserSid { get; set; }
        public string[] Executables { get; set; }
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
        private const string AppName = "Windows App Blocker";
        private const string TaskName = "WindowsAppBlocker";
        private const string InstallDirectory = @"C:\ProgramData\WindowsAppBlocker";
        private readonly string configPath = Path.Combine(InstallDirectory, "config.json");
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly Timer refreshTimer = new Timer();

        private AppBlockerConfig config;
        private DateTimePicker startPicker;
        private DateTimePicker endPicker;
        private ListBox executableList;
        private Panel statusPanel;
        private Label statusTitle;
        private Label statusDetail;
        private Button toggleButton;

        public MainForm()
        {
            Text = AppName;
            ClientSize = new Size(600, 620);
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
            if (File.Exists(configPath) && GetTask() != null) ShowControlScreen();
            else ShowInstallScreen();
        }

        private void AddHeader()
        {
            Controls.Add(MakeLabel(AppName, 22F, FontStyle.Bold, 24, 15, 420, 48));
            Controls.Add(MakeLabel("Block selected Windows apps on a daily schedule", 10F, FontStyle.Regular, 27, 56, 520, 28));
        }

        private void ShowInstallScreen()
        {
            Controls.Clear();
            ClientSize = new Size(600, 410);
            AddHeader();
            Panel card = new Panel();
            card.Location = new Point(24, 100);
            card.Size = new Size(552, 105);
            card.BackColor = Color.FromArgb(255, 241, 214);
            card.Controls.Add(MakeLabel("Setup needed", 18F, FontStyle.Bold, 17, 12, 510, 38));
            card.Controls.Add(MakeLabel("Install once, then choose apps and the hours when they are blocked.", 10F, FontStyle.Regular, 18, 50, 510, 42));
            Controls.Add(card);

            Controls.Add(MakeLabel("Create shortcuts", 11F, FontStyle.Bold, 24, 220, 250, 28));
            CheckBox startMenuShortcut = new CheckBox();
            startMenuShortcut.Text = "Add to Start menu";
            startMenuShortcut.Checked = true;
            startMenuShortcut.Location = new Point(28, 253);
            startMenuShortcut.Size = new Size(220, 28);
            Controls.Add(startMenuShortcut);
            CheckBox desktopShortcut = new CheckBox();
            desktopShortcut.Text = "Add to desktop";
            desktopShortcut.Checked = false;
            desktopShortcut.Location = new Point(292, 253);
            desktopShortcut.Size = new Size(220, 28);
            Controls.Add(desktopShortcut);

            Button installButton = MakeButton("Install Windows App Blocker", 24, 310, 552, 52);
            installButton.BackColor = Color.FromArgb(32, 102, 190);
            installButton.ForeColor = Color.White;
            installButton.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
            installButton.Click += delegate
            {
                installButton.Enabled = false;
                installButton.Text = "Installing...";
                Cursor = Cursors.WaitCursor;
                Application.DoEvents();
                try { RunInstaller(startMenuShortcut.Checked, desktopShortcut.Checked); ShowControlScreen(); }
                catch (Exception exception)
                {
                    MessageBox.Show(this, exception.Message, AppName + " could not be installed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    installButton.Enabled = true;
                    installButton.Text = "Try installation again";
                }
                finally { Cursor = Cursors.Default; }
            };
            Controls.Add(installButton);
        }

        private void ShowControlScreen()
        {
            config = LoadConfig();
            Controls.Clear();
            ClientSize = new Size(600, 655);
            AddHeader();
            statusPanel = new Panel();
            statusPanel.Location = new Point(24, 94);
            statusPanel.Size = new Size(552, 86);
            statusTitle = MakeLabel("", 17F, FontStyle.Bold, 18, 6, 515, 38);
            statusDetail = MakeLabel("", 10F, FontStyle.Regular, 19, 42, 515, 32);
            statusPanel.Controls.Add(statusTitle);
            statusPanel.Controls.Add(statusDetail);
            Controls.Add(statusPanel);

            Controls.Add(MakeLabel("Apps to block", 12F, FontStyle.Bold, 24, 198, 250, 28));
            executableList = new ListBox();
            executableList.Location = new Point(24, 232);
            executableList.Size = new Size(552, 155);
            executableList.HorizontalScrollbar = true;
            executableList.SelectionMode = SelectionMode.MultiExtended;
            foreach (string executable in config.Executables ?? new string[0]) executableList.Items.Add(executable);
            Controls.Add(executableList);

            Button addButton = MakeButton("Add .exe...", 24, 398, 130, 38);
            addButton.BackColor = Color.FromArgb(32, 102, 190);
            addButton.ForeColor = Color.White;
            addButton.Click += delegate { AddExecutables(); };
            Controls.Add(addButton);
            Button removeButton = MakeButton("Remove selected", 165, 398, 150, 38);
            removeButton.BackColor = Color.FromArgb(225, 230, 237);
            removeButton.Click += delegate { RemoveSelectedExecutables(); };
            Controls.Add(removeButton);

            GroupBox scheduleGroup = new GroupBox();
            scheduleGroup.Text = "Daily blocking hours";
            scheduleGroup.Location = new Point(24, 448);
            scheduleGroup.Size = new Size(552, 82);
            scheduleGroup.Controls.Add(MakeLabel("From", 10F, FontStyle.Regular, 20, 29, 48, 28));
            startPicker = CreateTimePicker(config.BlockStart, 72, 30);
            scheduleGroup.Controls.Add(startPicker);
            scheduleGroup.Controls.Add(MakeLabel("Until", 10F, FontStyle.Regular, 202, 29, 45, 28));
            endPicker = CreateTimePicker(config.BlockEnd, 251, 30);
            scheduleGroup.Controls.Add(endPicker);
            Label overnightHint = MakeLabel("overnight is OK", 9F, FontStyle.Regular, 365, 29, 150, 28);
            overnightHint.ForeColor = Color.FromArgb(90, 96, 105);
            scheduleGroup.Controls.Add(overnightHint);
            Controls.Add(scheduleGroup);

            Button saveButton = MakeButton("Save changes", 24, 544, 150, 43);
            saveButton.BackColor = Color.FromArgb(225, 230, 237);
            saveButton.Click += delegate { SaveWithMessage(); };
            Controls.Add(saveButton);
            toggleButton = MakeButton("", 186, 544, 390, 43);
            toggleButton.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            toggleButton.Click += delegate { ToggleProtection(); };
            Controls.Add(toggleButton);

            Label note = MakeLabel("The end time is exclusive. Protection keeps running when this window closes.", 9F, FontStyle.Regular, 25, 598, 440, 28);
            note.ForeColor = Color.FromArgb(90, 96, 105);
            Controls.Add(note);
            Button uninstallButton = MakeButton("Uninstall", 446, 598, 130, 32);
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

        private AppBlockerConfig LoadConfig()
        {
            AppBlockerConfig loaded = serializer.Deserialize<AppBlockerConfig>(File.ReadAllText(configPath, Encoding.UTF8));
            if (loaded.Executables == null) loaded.Executables = new string[0];
            return loaded;
        }

        private void AddExecutables()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose apps to block";
                dialog.Filter = "Windows applications (*.exe)|*.exe";
                dialog.Multiselect = true;
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                foreach (string selectedPath in dialog.FileNames)
                {
                    string fullPath = Path.GetFullPath(selectedPath);
                    bool alreadyAdded = false;
                    foreach (object item in executableList.Items)
                    {
                        if (string.Equals((string)item, fullPath, StringComparison.OrdinalIgnoreCase)) { alreadyAdded = true; break; }
                    }
                    if (!alreadyAdded) executableList.Items.Add(fullPath);
                }
            }
        }

        private void RemoveSelectedExecutables()
        {
            while (executableList.SelectedIndices.Count > 0) executableList.Items.RemoveAt(executableList.SelectedIndices[0]);
        }

        private void SaveConfig()
        {
            string start = startPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            string end = endPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (start == end) throw new InvalidOperationException("Choose two different times.");
            List<string> executables = new List<string>();
            foreach (object item in executableList.Items) executables.Add((string)item);
            config.BlockStart = start;
            config.BlockEnd = end;
            config.Executables = executables.ToArray();
            string temporaryPath = Path.Combine(InstallDirectory, ".config-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporaryPath, serializer.Serialize(config), new UTF8Encoding(false));
                File.Replace(temporaryPath, configPath, null, true);
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        }

        private void SaveWithMessage()
        {
            try
            {
                SaveConfig();
                RefreshStatus();
                MessageBox.Show(this, "Your apps and blocking times are saved.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "Could not save changes", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ToggleProtection()
        {
            try
            {
                SaveConfig();
                if (config.Executables.Length == 0) throw new InvalidOperationException("Add at least one .exe before turning protection on.");
                dynamic task = GetTask();
                if (task == null) throw new InvalidOperationException("The background task is missing. Reinstall " + AppName + ".");
                if ((bool)task.Enabled) { task.Stop(0); task.Enabled = false; }
                else { task.Enabled = true; task.Run(null); }
                ReleaseComObject(task);
                RefreshStatus();
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "Could not change protection", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void UninstallApplication()
        {
            DialogResult choice = MessageBox.Show(this, "This will turn protection off and completely remove " + AppName + ".\n\nDo you want to continue?", "Uninstall " + AppName + "?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (choice != DialogResult.Yes) return;
            try
            {
                string uninstaller = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uninstall-AppBlocker.ps1");
                if (!File.Exists(uninstaller)) uninstaller = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app", "Uninstall-AppBlocker.ps1");
                if (!File.Exists(uninstaller)) throw new FileNotFoundException("The uninstall helper could not be found.");
                RunPowerShellScript(uninstaller, "-RemoveFiles -WaitForProcessId " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
                MessageBox.Show(this, AppName + " has been turned off and will finish removing itself when this window closes.", "Uninstall started", MessageBoxButtons.OK, MessageBoxIcon.Information);
                refreshTimer.Stop();
                Application.Exit();
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "Could not uninstall " + AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
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
                int count = config.Executables == null ? 0 : config.Executables.Length;
                if (enabled)
                {
                    statusPanel.BackColor = Color.FromArgb(215, 244, 226);
                    statusTitle.ForeColor = Color.FromArgb(20, 106, 62);
                    statusTitle.Text = "PROTECTION IS ON";
                    statusDetail.Text = IsBlockedNow(config) ? FormatCount(count) + " blocked right now." : FormatCount(count) + " blocked from " + config.BlockStart + ".";
                    toggleButton.Text = "Turn protection OFF";
                    toggleButton.BackColor = Color.FromArgb(196, 57, 57);
                    toggleButton.ForeColor = Color.White;
                }
                else
                {
                    statusPanel.BackColor = Color.FromArgb(255, 224, 224);
                    statusTitle.ForeColor = Color.FromArgb(159, 37, 37);
                    statusTitle.Text = "PROTECTION IS OFF";
                    statusDetail.Text = FormatCount(count) + " configured; all apps are currently allowed.";
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

        private static string FormatCount(int count)
        {
            return count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " app is" : " apps are");
        }

        private static bool IsBlockedNow(AppBlockerConfig currentConfig)
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
            catch (COMException) { return null; }
            finally { ReleaseComObject(folder); ReleaseComObject(service); }
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }

        private static void RunInstaller(bool addStartMenuShortcut, bool addDesktopShortcut)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string installer = Path.Combine(baseDirectory, "Install-AppBlocker.ps1");
            if (!File.Exists(installer)) installer = Path.Combine(baseDirectory, "app", "Install-AppBlocker.ps1");
            if (!File.Exists(installer)) throw new FileNotFoundException("The app folder is missing. Extract the complete download before installing.");
            string arguments = "-NoStart";
            if (!addStartMenuShortcut) arguments += " -SkipStartMenuShortcut";
            if (addDesktopShortcut) arguments += " -AddDesktopShortcut";
            RunPowerShellScript(installer, arguments);
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
                if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
            }
        }
    }
}
