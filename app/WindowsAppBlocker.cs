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
        public string ScheduleMode { get; set; }
        public string WeekdayBlockStart { get; set; }
        public string WeekdayBlockEnd { get; set; }
        public string WeekendBlockStart { get; set; }
        public string WeekendBlockEnd { get; set; }
        public bool? WeekdayEnabled { get; set; }
        public bool? WeekendEnabled { get; set; }
        public string MondayBlockStart { get; set; }
        public string MondayBlockEnd { get; set; }
        public bool? MondayEnabled { get; set; }
        public string TuesdayBlockStart { get; set; }
        public string TuesdayBlockEnd { get; set; }
        public bool? TuesdayEnabled { get; set; }
        public string WednesdayBlockStart { get; set; }
        public string WednesdayBlockEnd { get; set; }
        public bool? WednesdayEnabled { get; set; }
        public string ThursdayBlockStart { get; set; }
        public string ThursdayBlockEnd { get; set; }
        public bool? ThursdayEnabled { get; set; }
        public string FridayBlockStart { get; set; }
        public string FridayBlockEnd { get; set; }
        public bool? FridayEnabled { get; set; }
        public string SaturdayBlockStart { get; set; }
        public string SaturdayBlockEnd { get; set; }
        public bool? SaturdayEnabled { get; set; }
        public string SundayBlockStart { get; set; }
        public string SundayBlockEnd { get; set; }
        public bool? SundayEnabled { get; set; }
        public string ChangeTimesPolicy { get; set; }
        public string UninstallPolicy { get; set; }
        public string RemoveExecutablesPolicy { get; set; }
        public string TurnOffProtectionPolicy { get; set; }
        public bool SetupCompleted { get; set; }
        public int SetupVersion { get; set; }
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
        private const string EveryDay = "EveryDay";
        private const string WeekdayWeekend = "WeekdayWeekend";
        private const string IndividualDays = "IndividualDays";
        private const string Always = "Always";
        private const string Never = "Never";
        private const string AllowedHoursOnly = "AllowedHoursOnly";

        private readonly string configPath = Path.Combine(InstallDirectory, "config.json");
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly Timer refreshTimer = new Timer();
        private readonly ToolTip toolTip = new ToolTip();

        private AppBlockerConfig config;
        private RadioButton everyDayRadio;
        private RadioButton splitScheduleRadio;
        private RadioButton individualDaysRadio;
        private DateTimePicker everydayStartPicker;
        private DateTimePicker everydayEndPicker;
        private DateTimePicker weekdayStartPicker;
        private DateTimePicker weekdayEndPicker;
        private DateTimePicker weekendStartPicker;
        private DateTimePicker weekendEndPicker;
        private CheckBox weekdayEnabledCheckBox;
        private CheckBox weekendEnabledCheckBox;
        private Panel everydayRow;
        private Panel splitRows;
        private Panel individualRows;
        private readonly DateTimePicker[] individualStartPickers = new DateTimePicker[7];
        private readonly DateTimePicker[] individualEndPickers = new DateTimePicker[7];
        private readonly CheckBox[] individualEnabledCheckBoxes = new CheckBox[7];
        private ListBox executableList;
        private Panel statusPanel;
        private Label statusTitle;
        private Label statusDetail;
        private Label permissionSummary;
        private Button removeButton;
        private Button uninstallButton;
        private Button toggleButton;

        public MainForm()
        {
            Text = AppName;
            ClientSize = new Size(680, 750);
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
            dynamic task = GetTask();
            bool taskExists = task != null;
            ReleaseComObject(task);
            if (File.Exists(configPath) && taskExists)
            {
                config = LoadConfig();
                if (config.SetupCompleted && config.SetupVersion >= 2) { ShowControlScreen(); return; }
            }
            ShowSetupScreen();
        }

        private void AddHeader(string subtitle)
        {
            Controls.Add(MakeLabel(AppName, 22F, FontStyle.Bold, 24, 15, 500, 48));
            Controls.Add(MakeLabel(subtitle, 10F, FontStyle.Regular, 27, 56, 620, 28));
        }

        private void ShowSetupScreen()
        {
            AppBlockerConfig setupSeed = null;
            if (File.Exists(configPath))
            {
                try { setupSeed = LoadConfig(); }
                catch { setupSeed = null; }
            }
            Controls.Clear();
            ClientSize = new Size(680, 760);
            AddHeader("One-time setup — choose your schedule and which controls stay available");

            Panel content = new Panel();
            content.Location = new Point(24, 92);
            content.Size = new Size(632, 590);
            content.AutoScroll = true;
            content.BackColor = Color.White;
            Controls.Add(content);

            content.Controls.Add(MakeLabel("1. When should apps be blocked?", 13F, FontStyle.Bold, 18, 12, 560, 32));
            everyDayRadio = new RadioButton();
            everyDayRadio.Text = "Same hours every day";
            everyDayRadio.Location = new Point(22, 51);
            everyDayRadio.Size = new Size(185, 28);
            everyDayRadio.Checked = setupSeed == null || setupSeed.ScheduleMode == EveryDay;
            content.Controls.Add(everyDayRadio);
            splitScheduleRadio = new RadioButton();
            splitScheduleRadio.Text = "Weekdays / weekend";
            splitScheduleRadio.Location = new Point(215, 51);
            splitScheduleRadio.Size = new Size(190, 28);
            splitScheduleRadio.Checked = setupSeed != null && setupSeed.ScheduleMode == WeekdayWeekend;
            content.Controls.Add(splitScheduleRadio);
            individualDaysRadio = new RadioButton();
            individualDaysRadio.Text = "Each day individually";
            individualDaysRadio.Location = new Point(413, 51);
            individualDaysRadio.Size = new Size(190, 28);
            individualDaysRadio.Checked = setupSeed != null && setupSeed.ScheduleMode == IndividualDays;
            content.Controls.Add(individualDaysRadio);

            string dailyStart = setupSeed == null ? "23:00" : setupSeed.BlockStart;
            string dailyEnd = setupSeed == null ? "07:00" : setupSeed.BlockEnd;
            string weekdayStart = setupSeed == null ? "23:00" : setupSeed.WeekdayBlockStart;
            string weekdayEnd = setupSeed == null ? "07:00" : setupSeed.WeekdayBlockEnd;
            string weekendStart = setupSeed == null ? "00:00" : setupSeed.WeekendBlockStart;
            string weekendEnd = setupSeed == null ? "09:00" : setupSeed.WeekendBlockEnd;
            everydayRow = CreateTimeRow("Every day", dailyStart, dailyEnd, 18, 83, out everydayStartPicker, out everydayEndPicker);
            content.Controls.Add(everydayRow);
            splitRows = new Panel();
            splitRows.Location = new Point(18, 83);
            splitRows.Size = new Size(570, 82);
            splitRows.Controls.Add(CreateOptionalTimeRow("Mon–Fri", weekdayStart, weekdayEnd, IsScheduleEnabled(setupSeed == null ? null : setupSeed.WeekdayEnabled), 0, 0, out weekdayStartPicker, out weekdayEndPicker, out weekdayEnabledCheckBox));
            splitRows.Controls.Add(CreateOptionalTimeRow("Sat–Sun", weekendStart, weekendEnd, IsScheduleEnabled(setupSeed == null ? null : setupSeed.WeekendEnabled), 0, 41, out weekendStartPicker, out weekendEndPicker, out weekendEnabledCheckBox));
            splitRows.Visible = splitScheduleRadio.Checked;
            content.Controls.Add(splitRows);
            individualRows = CreateIndividualRows(setupSeed, 18, 83);
            content.Controls.Add(individualRows);
            Label overnightHint = MakeLabel("The start time is included; the end time is not. Overnight periods are supported.", 9F, FontStyle.Regular, 22, 250, 565, 28);
            overnightHint.ForeColor = Color.FromArgb(90, 96, 105);
            content.Controls.Add(overnightHint);
            everyDayRadio.CheckedChanged += delegate { UpdateScheduleRows(); };
            splitScheduleRadio.CheckedChanged += delegate { UpdateScheduleRows(); };
            individualDaysRadio.CheckedChanged += delegate { UpdateScheduleRows(); };
            UpdateScheduleRows();

            content.Controls.Add(MakeLabel("2. Choose what can be changed later", 13F, FontStyle.Bold, 18, 291, 560, 32));
            Label policyHelp = MakeLabel("“Only during allowed hours” means only while the selected apps are not scheduled to be blocked.", 9F, FontStyle.Regular, 22, 322, 570, 36);
            policyHelp.ForeColor = Color.FromArgb(90, 96, 105);
            content.Controls.Add(policyHelp);
            ComboBox changeTimesPolicy = AddPolicyRow(content, "Change blocking hours", 365);
            ComboBox turnOffPolicy = AddPolicyRow(content, "Turn protection off", 409);
            ComboBox uninstallPolicy = AddPolicyRow(content, "Uninstall this app", 453);
            ComboBox removePolicy = AddPolicyRow(content, "Remove apps from the block list", 497);
            if (setupSeed != null)
            {
                SetPolicySelection(changeTimesPolicy, setupSeed.ChangeTimesPolicy);
                SetPolicySelection(turnOffPolicy, setupSeed.TurnOffProtectionPolicy);
                SetPolicySelection(uninstallPolicy, setupSeed.UninstallPolicy);
                SetPolicySelection(removePolicy, setupSeed.RemoveExecutablesPolicy);
            }

            content.Controls.Add(MakeLabel("3. Choose apps to block", 13F, FontStyle.Bold, 18, 549, 560, 32));
            executableList = new ListBox();
            executableList.Location = new Point(22, 586);
            executableList.Size = new Size(566, 96);
            executableList.HorizontalScrollbar = true;
            executableList.SelectionMode = SelectionMode.MultiExtended;
            if (setupSeed != null)
            {
                foreach (string executable in setupSeed.Executables) executableList.Items.Add(executable);
            }
            content.Controls.Add(executableList);
            Button addButton = MakeButton("Add .exe...", 22, 692, 130, 36);
            addButton.BackColor = Color.FromArgb(32, 102, 190);
            addButton.ForeColor = Color.White;
            addButton.Click += delegate { AddExecutables(); };
            content.Controls.Add(addButton);
            Button setupRemoveButton = MakeButton("Remove selected", 162, 692, 150, 36);
            setupRemoveButton.BackColor = Color.FromArgb(225, 230, 237);
            setupRemoveButton.Click += delegate { RemoveSelectedExecutablesUnchecked(); };
            content.Controls.Add(setupRemoveButton);

            content.Controls.Add(MakeLabel("4. Create shortcuts", 13F, FontStyle.Bold, 18, 747, 560, 32));
            CheckBox startMenuShortcut = new CheckBox();
            startMenuShortcut.Text = "Add to Start menu";
            startMenuShortcut.Checked = true;
            startMenuShortcut.Location = new Point(22, 784);
            startMenuShortcut.Size = new Size(240, 28);
            content.Controls.Add(startMenuShortcut);
            CheckBox desktopShortcut = new CheckBox();
            desktopShortcut.Text = "Add to desktop";
            desktopShortcut.Location = new Point(310, 784);
            desktopShortcut.Size = new Size(240, 28);
            content.Controls.Add(desktopShortcut);
            content.AutoScrollMinSize = new Size(0, 830);

            Button installButton = MakeButton("Install and finish setup", 24, 696, 632, 48);
            installButton.BackColor = Color.FromArgb(32, 102, 190);
            installButton.ForeColor = Color.White;
            installButton.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
            installButton.Click += delegate
            {
                if (!ConfirmPermanentChoices(changeTimesPolicy, turnOffPolicy, uninstallPolicy, removePolicy)) return;
                installButton.Enabled = false;
                installButton.Text = "Installing...";
                Cursor = Cursors.WaitCursor;
                Application.DoEvents();
                try
                {
                    ValidateSchedulePickers();
                    if (executableList.Items.Count == 0) throw new InvalidOperationException("Choose at least one .exe to block before finishing setup.");
                    dynamic existingTask = GetTask();
                    bool restoreProtection = existingTask != null && (bool)existingTask.Enabled;
                    ReleaseComObject(existingTask);
                    RunInstaller(startMenuShortcut.Checked, desktopShortcut.Checked);
                    config = LoadConfig();
                    ApplyScheduleFromControls(config);
                    config.ChangeTimesPolicy = GetPolicyValue(changeTimesPolicy);
                    config.TurnOffProtectionPolicy = GetPolicyValue(turnOffPolicy);
                    config.UninstallPolicy = GetPolicyValue(uninstallPolicy);
                    config.RemoveExecutablesPolicy = GetPolicyValue(removePolicy);
                    config.Executables = GetExecutableItems();
                    config.SetupCompleted = true;
                    config.SetupVersion = 2;
                    SaveConfigFile(config);
                    if (restoreProtection)
                    {
                        dynamic restoredTask = null;
                        try
                        {
                            restoredTask = GetTask();
                            if (restoredTask == null) throw new InvalidOperationException("The background task could not be restarted after setup.");
                            restoredTask.Enabled = true;
                            restoredTask.Run(null);
                        }
                        finally { ReleaseComObject(restoredTask); }
                    }
                    ShowControlScreen();
                }
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

        private bool ConfirmPermanentChoices(ComboBox changeTimes, ComboBox turnOff, ComboBox uninstall, ComboBox removeApps)
        {
            List<string> locked = new List<string>();
            if (GetPolicyValue(changeTimes) == Never) locked.Add("change blocking hours");
            if (GetPolicyValue(turnOff) == Never) locked.Add("turn protection off");
            if (GetPolicyValue(uninstall) == Never) locked.Add("uninstall from this app");
            if (GetPolicyValue(removeApps) == Never) locked.Add("remove apps from the block list");
            if (locked.Count == 0) return true;

            string message = "You chose ‘No — never’ for:\n\n• " + string.Join("\n• ", locked.ToArray()) +
                "\n\nThese controls will stay disabled after setup. Windows administrators can still bypass this self-control tool outside the app. Continue?";
            return MessageBox.Show(this, message, "Confirm locked controls", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private static ComboBox AddPolicyRow(Control parent, string labelText, int y)
        {
            parent.Controls.Add(MakeLabel(labelText, 10F, FontStyle.Regular, 22, y, 285, 30));
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Add("Yes — anytime");
            combo.Items.Add("No — never");
            combo.Items.Add("Only during allowed hours");
            combo.SelectedIndex = 0;
            combo.Location = new Point(318, y);
            combo.Size = new Size(270, 30);
            parent.Controls.Add(combo);
            return combo;
        }

        private static string GetPolicyValue(ComboBox combo)
        {
            if (combo.SelectedIndex == 1) return Never;
            if (combo.SelectedIndex == 2) return AllowedHoursOnly;
            return Always;
        }

        private static void SetPolicySelection(ComboBox combo, string policy)
        {
            combo.SelectedIndex = policy == Never ? 1 : (policy == AllowedHoursOnly ? 2 : 0);
        }

        private void ShowControlScreen()
        {
            config = LoadConfig();
            Controls.Clear();
            ClientSize = new Size(680, 760);
            AddHeader("Block selected Windows apps on your schedule");
            statusPanel = new Panel();
            statusPanel.Location = new Point(24, 94);
            statusPanel.Size = new Size(632, 86);
            statusTitle = MakeLabel("", 17F, FontStyle.Bold, 18, 6, 595, 38);
            statusDetail = MakeLabel("", 10F, FontStyle.Regular, 19, 42, 595, 32);
            statusPanel.Controls.Add(statusTitle);
            statusPanel.Controls.Add(statusDetail);
            Controls.Add(statusPanel);

            Controls.Add(MakeLabel("Apps to block", 12F, FontStyle.Bold, 24, 194, 250, 28));
            executableList = new ListBox();
            executableList.Location = new Point(24, 226);
            executableList.Size = new Size(632, 100);
            executableList.HorizontalScrollbar = true;
            executableList.SelectionMode = SelectionMode.MultiExtended;
            foreach (string executable in config.Executables) executableList.Items.Add(executable);
            Controls.Add(executableList);

            Button addButton = MakeButton("Add .exe...", 24, 337, 130, 38);
            addButton.BackColor = Color.FromArgb(32, 102, 190);
            addButton.ForeColor = Color.White;
            addButton.Click += delegate { AddExecutables(); };
            Controls.Add(addButton);
            removeButton = MakeButton("Remove selected", 165, 337, 150, 38);
            removeButton.BackColor = Color.FromArgb(225, 230, 237);
            removeButton.Click += delegate { RemoveSelectedExecutables(); };
            Controls.Add(removeButton);

            GroupBox scheduleGroup = new GroupBox();
            scheduleGroup.Text = "Blocking hours";
            scheduleGroup.Location = new Point(24, 387);
            scheduleGroup.Size = new Size(632, 200);
            everyDayRadio = new RadioButton();
            everyDayRadio.Text = "Same every day";
            everyDayRadio.Location = new Point(18, 24);
            everyDayRadio.Size = new Size(155, 27);
            everyDayRadio.Checked = config.ScheduleMode == EveryDay;
            scheduleGroup.Controls.Add(everyDayRadio);
            splitScheduleRadio = new RadioButton();
            splitScheduleRadio.Text = "Weekdays / weekend";
            splitScheduleRadio.Location = new Point(175, 24);
            splitScheduleRadio.Size = new Size(185, 27);
            splitScheduleRadio.Checked = config.ScheduleMode == WeekdayWeekend;
            scheduleGroup.Controls.Add(splitScheduleRadio);
            individualDaysRadio = new RadioButton();
            individualDaysRadio.Text = "Each day individually";
            individualDaysRadio.Location = new Point(378, 24);
            individualDaysRadio.Size = new Size(190, 27);
            individualDaysRadio.Checked = config.ScheduleMode == IndividualDays;
            scheduleGroup.Controls.Add(individualDaysRadio);
            everydayRow = CreateTimeRow("Every day", config.BlockStart, config.BlockEnd, 14, 58, out everydayStartPicker, out everydayEndPicker);
            scheduleGroup.Controls.Add(everydayRow);
            splitRows = new Panel();
            splitRows.Location = new Point(14, 58);
            splitRows.Size = new Size(590, 82);
            splitRows.Controls.Add(CreateOptionalTimeRow("Mon–Fri", config.WeekdayBlockStart, config.WeekdayBlockEnd, IsScheduleEnabled(config.WeekdayEnabled), 0, 0, out weekdayStartPicker, out weekdayEndPicker, out weekdayEnabledCheckBox));
            splitRows.Controls.Add(CreateOptionalTimeRow("Sat–Sun", config.WeekendBlockStart, config.WeekendBlockEnd, IsScheduleEnabled(config.WeekendEnabled), 0, 41, out weekendStartPicker, out weekendEndPicker, out weekendEnabledCheckBox));
            scheduleGroup.Controls.Add(splitRows);
            individualRows = CreateIndividualRows(config, 14, 58);
            scheduleGroup.Controls.Add(individualRows);
            everyDayRadio.CheckedChanged += delegate { UpdateScheduleRows(); };
            splitScheduleRadio.CheckedChanged += delegate { UpdateScheduleRows(); };
            individualDaysRadio.CheckedChanged += delegate { UpdateScheduleRows(); };
            Controls.Add(scheduleGroup);
            UpdateScheduleRows();

            Button saveButton = MakeButton("Save changes", 24, 603, 160, 43);
            saveButton.BackColor = Color.FromArgb(225, 230, 237);
            saveButton.Click += delegate { SaveWithMessage(); };
            Controls.Add(saveButton);
            toggleButton = MakeButton("", 196, 603, 460, 43);
            toggleButton.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            toggleButton.Click += delegate { ToggleProtection(); };
            Controls.Add(toggleButton);

            permissionSummary = MakeLabel("", 9F, FontStyle.Regular, 25, 656, 610, 56);
            permissionSummary.ForeColor = Color.FromArgb(90, 96, 105);
            Controls.Add(permissionSummary);
            Label note = MakeLabel("Protection keeps running when this window closes.", 9F, FontStyle.Regular, 25, 718, 400, 28);
            note.ForeColor = Color.FromArgb(90, 96, 105);
            Controls.Add(note);
            uninstallButton = MakeButton("Uninstall", 526, 715, 130, 32);
            uninstallButton.BackColor = Color.FromArgb(235, 238, 242);
            uninstallButton.ForeColor = Color.FromArgb(90, 96, 105);
            uninstallButton.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            uninstallButton.Click += delegate { UninstallApplication(); };
            Controls.Add(uninstallButton);
            RefreshStatus();
        }

        private static Panel CreateTimeRow(string labelText, string start, string end, int x, int y, out DateTimePicker startPicker, out DateTimePicker endPicker)
        {
            Panel panel = new Panel();
            panel.Location = new Point(x, y);
            panel.Size = new Size(570, 41);
            panel.Controls.Add(MakeLabel(labelText, 10F, FontStyle.Regular, 4, 4, 92, 28));
            panel.Controls.Add(MakeLabel("From", 9F, FontStyle.Regular, 105, 4, 42, 28));
            startPicker = CreateTimePicker(start, 148, 4);
            panel.Controls.Add(startPicker);
            panel.Controls.Add(MakeLabel("Until", 9F, FontStyle.Regular, 252, 4, 42, 28));
            endPicker = CreateTimePicker(end, 297, 4);
            panel.Controls.Add(endPicker);
            return panel;
        }

        private static Panel CreateOptionalTimeRow(string labelText, string start, string end, bool enabled, int x, int y, out DateTimePicker startPicker, out DateTimePicker endPicker, out CheckBox enabledCheckBox)
        {
            Panel panel = CreateTimeRow(labelText, start, end, x, y, out startPicker, out endPicker);
            enabledCheckBox = new CheckBox();
            enabledCheckBox.Text = "Enabled";
            enabledCheckBox.Checked = enabled;
            enabledCheckBox.Location = new Point(404, 5);
            enabledCheckBox.Size = new Size(90, 28);
            DateTimePicker capturedStart = startPicker;
            DateTimePicker capturedEnd = endPicker;
            CheckBox capturedCheckBox = enabledCheckBox;
            enabledCheckBox.CheckedChanged += delegate
            {
                capturedStart.Enabled = capturedCheckBox.Checked;
                capturedEnd.Enabled = capturedCheckBox.Checked;
            };
            startPicker.Enabled = enabled;
            endPicker.Enabled = enabled;
            panel.Controls.Add(enabledCheckBox);
            return panel;
        }

        private Panel CreateIndividualRows(AppBlockerConfig source, int x, int y)
        {
            Panel panel = new Panel();
            panel.Location = new Point(x, y);
            panel.Size = new Size(590, 126);
            string[] names = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            for (int index = 0; index < names.Length; index++)
            {
                string start;
                string end;
                if (source == null)
                {
                    start = index < 5 ? "23:00" : "00:00";
                    end = index < 5 ? "07:00" : "09:00";
                }
                else GetIndividualSchedule(source, index, out start, out end);
                int column = index % 2;
                int row = index / 2;
                bool enabled = source == null || GetIndividualEnabled(source, index);
                panel.Controls.Add(CreateCompactTimeRow(names[index], start, end, enabled, column * 292, row * 31, out individualStartPickers[index], out individualEndPickers[index], out individualEnabledCheckBoxes[index]));
            }
            return panel;
        }

        private static Panel CreateCompactTimeRow(string labelText, string start, string end, bool enabled, int x, int y, out DateTimePicker startPicker, out DateTimePicker endPicker, out CheckBox enabledCheckBox)
        {
            Panel panel = new Panel();
            panel.Location = new Point(x, y);
            panel.Size = new Size(286, 31);
            panel.Controls.Add(MakeLabel(labelText, 9F, FontStyle.Regular, 0, 1, 72, 27));
            startPicker = CreateTimePicker(start, 74, 1);
            startPicker.Size = new Size(76, 28);
            startPicker.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            panel.Controls.Add(startPicker);
            panel.Controls.Add(MakeLabel("–", 9F, FontStyle.Regular, 153, 1, 15, 27));
            endPicker = CreateTimePicker(end, 170, 1);
            endPicker.Size = new Size(76, 28);
            endPicker.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            panel.Controls.Add(endPicker);
            enabledCheckBox = new CheckBox();
            enabledCheckBox.Text = "On";
            enabledCheckBox.Checked = enabled;
            enabledCheckBox.Location = new Point(248, 2);
            enabledCheckBox.Size = new Size(38, 27);
            DateTimePicker capturedStart = startPicker;
            DateTimePicker capturedEnd = endPicker;
            CheckBox capturedCheckBox = enabledCheckBox;
            enabledCheckBox.CheckedChanged += delegate
            {
                capturedStart.Enabled = capturedCheckBox.Checked;
                capturedEnd.Enabled = capturedCheckBox.Checked;
            };
            startPicker.Enabled = enabled;
            endPicker.Enabled = enabled;
            panel.Controls.Add(enabledCheckBox);
            return panel;
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

        private void UpdateScheduleRows()
        {
            if (everydayRow == null || splitRows == null || individualRows == null) return;
            everydayRow.Visible = everyDayRadio.Checked;
            splitRows.Visible = splitScheduleRadio.Checked;
            individualRows.Visible = individualDaysRadio.Checked;
        }

        private AppBlockerConfig LoadConfig()
        {
            AppBlockerConfig loaded = serializer.Deserialize<AppBlockerConfig>(File.ReadAllText(configPath, Encoding.UTF8));
            if (loaded.Executables == null) loaded.Executables = new string[0];
            if (string.IsNullOrEmpty(loaded.ScheduleMode)) loaded.ScheduleMode = EveryDay;
            if (string.IsNullOrEmpty(loaded.WeekdayBlockStart)) loaded.WeekdayBlockStart = loaded.BlockStart;
            if (string.IsNullOrEmpty(loaded.WeekdayBlockEnd)) loaded.WeekdayBlockEnd = loaded.BlockEnd;
            if (string.IsNullOrEmpty(loaded.WeekendBlockStart)) loaded.WeekendBlockStart = loaded.BlockStart;
            if (string.IsNullOrEmpty(loaded.WeekendBlockEnd)) loaded.WeekendBlockEnd = loaded.BlockEnd;
            if (!loaded.WeekdayEnabled.HasValue) loaded.WeekdayEnabled = true;
            if (!loaded.WeekendEnabled.HasValue) loaded.WeekendEnabled = true;
            NormalizeIndividualSchedules(loaded);
            if (string.IsNullOrEmpty(loaded.ChangeTimesPolicy)) loaded.ChangeTimesPolicy = Always;
            if (string.IsNullOrEmpty(loaded.TurnOffProtectionPolicy)) loaded.TurnOffProtectionPolicy = Always;
            if (string.IsNullOrEmpty(loaded.UninstallPolicy)) loaded.UninstallPolicy = Always;
            if (string.IsNullOrEmpty(loaded.RemoveExecutablesPolicy)) loaded.RemoveExecutablesPolicy = Always;
            return loaded;
        }

        private static void NormalizeIndividualSchedules(AppBlockerConfig value)
        {
            if (string.IsNullOrEmpty(value.MondayBlockStart)) value.MondayBlockStart = value.WeekdayBlockStart;
            if (string.IsNullOrEmpty(value.MondayBlockEnd)) value.MondayBlockEnd = value.WeekdayBlockEnd;
            if (string.IsNullOrEmpty(value.TuesdayBlockStart)) value.TuesdayBlockStart = value.WeekdayBlockStart;
            if (string.IsNullOrEmpty(value.TuesdayBlockEnd)) value.TuesdayBlockEnd = value.WeekdayBlockEnd;
            if (string.IsNullOrEmpty(value.WednesdayBlockStart)) value.WednesdayBlockStart = value.WeekdayBlockStart;
            if (string.IsNullOrEmpty(value.WednesdayBlockEnd)) value.WednesdayBlockEnd = value.WeekdayBlockEnd;
            if (string.IsNullOrEmpty(value.ThursdayBlockStart)) value.ThursdayBlockStart = value.WeekdayBlockStart;
            if (string.IsNullOrEmpty(value.ThursdayBlockEnd)) value.ThursdayBlockEnd = value.WeekdayBlockEnd;
            if (string.IsNullOrEmpty(value.FridayBlockStart)) value.FridayBlockStart = value.WeekdayBlockStart;
            if (string.IsNullOrEmpty(value.FridayBlockEnd)) value.FridayBlockEnd = value.WeekdayBlockEnd;
            if (string.IsNullOrEmpty(value.SaturdayBlockStart)) value.SaturdayBlockStart = value.WeekendBlockStart;
            if (string.IsNullOrEmpty(value.SaturdayBlockEnd)) value.SaturdayBlockEnd = value.WeekendBlockEnd;
            if (string.IsNullOrEmpty(value.SundayBlockStart)) value.SundayBlockStart = value.WeekendBlockStart;
            if (string.IsNullOrEmpty(value.SundayBlockEnd)) value.SundayBlockEnd = value.WeekendBlockEnd;
            if (!value.MondayEnabled.HasValue) value.MondayEnabled = true;
            if (!value.TuesdayEnabled.HasValue) value.TuesdayEnabled = true;
            if (!value.WednesdayEnabled.HasValue) value.WednesdayEnabled = true;
            if (!value.ThursdayEnabled.HasValue) value.ThursdayEnabled = true;
            if (!value.FridayEnabled.HasValue) value.FridayEnabled = true;
            if (!value.SaturdayEnabled.HasValue) value.SaturdayEnabled = true;
            if (!value.SundayEnabled.HasValue) value.SundayEnabled = true;
        }

        private static bool IsScheduleEnabled(bool? enabled)
        {
            return !enabled.HasValue || enabled.Value;
        }

        private static bool GetIndividualEnabled(AppBlockerConfig value, int index)
        {
            switch (index)
            {
                case 0: return IsScheduleEnabled(value.MondayEnabled);
                case 1: return IsScheduleEnabled(value.TuesdayEnabled);
                case 2: return IsScheduleEnabled(value.WednesdayEnabled);
                case 3: return IsScheduleEnabled(value.ThursdayEnabled);
                case 4: return IsScheduleEnabled(value.FridayEnabled);
                case 5: return IsScheduleEnabled(value.SaturdayEnabled);
                default: return IsScheduleEnabled(value.SundayEnabled);
            }
        }

        private static void GetIndividualSchedule(AppBlockerConfig value, int index, out string start, out string end)
        {
            switch (index)
            {
                case 0: start = value.MondayBlockStart; end = value.MondayBlockEnd; break;
                case 1: start = value.TuesdayBlockStart; end = value.TuesdayBlockEnd; break;
                case 2: start = value.WednesdayBlockStart; end = value.WednesdayBlockEnd; break;
                case 3: start = value.ThursdayBlockStart; end = value.ThursdayBlockEnd; break;
                case 4: start = value.FridayBlockStart; end = value.FridayBlockEnd; break;
                case 5: start = value.SaturdayBlockStart; end = value.SaturdayBlockEnd; break;
                default: start = value.SundayBlockStart; end = value.SundayBlockEnd; break;
            }
        }

        private static void SetIndividualSchedule(AppBlockerConfig value, int index, string start, string end)
        {
            switch (index)
            {
                case 0: value.MondayBlockStart = start; value.MondayBlockEnd = end; break;
                case 1: value.TuesdayBlockStart = start; value.TuesdayBlockEnd = end; break;
                case 2: value.WednesdayBlockStart = start; value.WednesdayBlockEnd = end; break;
                case 3: value.ThursdayBlockStart = start; value.ThursdayBlockEnd = end; break;
                case 4: value.FridayBlockStart = start; value.FridayBlockEnd = end; break;
                case 5: value.SaturdayBlockStart = start; value.SaturdayBlockEnd = end; break;
                default: value.SundayBlockStart = start; value.SundayBlockEnd = end; break;
            }
        }

        private static void SetIndividualEnabled(AppBlockerConfig value, int index, bool enabled)
        {
            switch (index)
            {
                case 0: value.MondayEnabled = enabled; break;
                case 1: value.TuesdayEnabled = enabled; break;
                case 2: value.WednesdayEnabled = enabled; break;
                case 3: value.ThursdayEnabled = enabled; break;
                case 4: value.FridayEnabled = enabled; break;
                case 5: value.SaturdayEnabled = enabled; break;
                default: value.SundayEnabled = enabled; break;
            }
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

        private void RemoveSelectedExecutablesUnchecked()
        {
            while (executableList.SelectedIndices.Count > 0) executableList.Items.RemoveAt(executableList.SelectedIndices[0]);
        }

        private void RemoveSelectedExecutables()
        {
            if (!CanUsePolicy(config.RemoveExecutablesPolicy, DateTime.Now))
            {
                MessageBox.Show(this, PolicyUnavailableMessage(config.RemoveExecutablesPolicy, "Apps cannot be removed"), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            RemoveSelectedExecutablesUnchecked();
        }

        private string[] GetExecutableItems()
        {
            List<string> executables = new List<string>();
            foreach (object item in executableList.Items) executables.Add((string)item);
            return executables.ToArray();
        }

        private void ValidateSchedulePickers()
        {
            if (everyDayRadio.Checked) ValidateDifferent(everydayStartPicker, everydayEndPicker, "every-day");
            else if (splitScheduleRadio.Checked)
            {
                if (weekdayEnabledCheckBox.Checked) ValidateDifferent(weekdayStartPicker, weekdayEndPicker, "weekday");
                if (weekendEnabledCheckBox.Checked) ValidateDifferent(weekendStartPicker, weekendEndPicker, "weekend");
            }
            else
            {
                string[] names = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
                for (int index = 0; index < names.Length; index++)
                    if (individualEnabledCheckBoxes[index].Checked) ValidateDifferent(individualStartPickers[index], individualEndPickers[index], names[index]);
            }
        }

        private static void ValidateDifferent(DateTimePicker start, DateTimePicker end, string scheduleName)
        {
            if (start.Value.ToString("HH:mm", CultureInfo.InvariantCulture) == end.Value.ToString("HH:mm", CultureInfo.InvariantCulture))
                throw new InvalidOperationException("Choose different start and end times for the " + scheduleName + " schedule.");
        }

        private void ApplyScheduleFromControls(AppBlockerConfig target)
        {
            target.ScheduleMode = everyDayRadio.Checked ? EveryDay : (splitScheduleRadio.Checked ? WeekdayWeekend : IndividualDays);
            target.BlockStart = everydayStartPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            target.BlockEnd = everydayEndPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            target.WeekdayBlockStart = weekdayStartPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            target.WeekdayBlockEnd = weekdayEndPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            target.WeekendBlockStart = weekendStartPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            target.WeekendBlockEnd = weekendEndPicker.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            target.WeekdayEnabled = weekdayEnabledCheckBox.Checked;
            target.WeekendEnabled = weekendEnabledCheckBox.Checked;
            for (int index = 0; index < 7; index++)
            {
                SetIndividualSchedule(target, index,
                    individualStartPickers[index].Value.ToString("HH:mm", CultureInfo.InvariantCulture),
                    individualEndPickers[index].Value.ToString("HH:mm", CultureInfo.InvariantCulture));
                SetIndividualEnabled(target, index, individualEnabledCheckBoxes[index].Checked);
            }
        }

        private void SaveConfig(bool includeSchedule)
        {
            if (includeSchedule)
            {
                if (!CanUsePolicy(config.ChangeTimesPolicy, DateTime.Now))
                    throw new InvalidOperationException(PolicyUnavailableMessage(config.ChangeTimesPolicy, "Blocking hours cannot be changed"));
                ValidateSchedulePickers();
                ApplyScheduleFromControls(config);
            }
            config.Executables = GetExecutableItems();
            SaveConfigFile(config);
        }

        private void SaveConfigFile(AppBlockerConfig value)
        {
            string temporaryPath = Path.Combine(InstallDirectory, ".config-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporaryPath, serializer.Serialize(value), new UTF8Encoding(false));
                File.Replace(temporaryPath, configPath, null, true);
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        }

        private void SaveWithMessage()
        {
            try
            {
                bool canChangeTimes = CanUsePolicy(config.ChangeTimesPolicy, DateTime.Now);
                SaveConfig(canChangeTimes);
                RefreshStatus();
                MessageBox.Show(this, canChangeTimes ? "Your apps and blocking hours are saved." : "Your app list is saved. Blocking hours are locked right now.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "Could not save changes", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ToggleProtection()
        {
            dynamic task = null;
            try
            {
                SaveConfig(CanUsePolicy(config.ChangeTimesPolicy, DateTime.Now));
                if (config.Executables.Length == 0) throw new InvalidOperationException("Add at least one .exe before turning protection on.");
                task = GetTask();
                if (task == null) throw new InvalidOperationException("The background task is missing. Reinstall " + AppName + ".");
                if ((bool)task.Enabled)
                {
                    if (!CanUsePolicy(config.TurnOffProtectionPolicy, DateTime.Now))
                        throw new InvalidOperationException(PolicyUnavailableMessage(config.TurnOffProtectionPolicy, "Turning protection off is unavailable"));
                    task.Stop(0);
                    task.Enabled = false;
                }
                else { task.Enabled = true; task.Run(null); }
                RefreshStatus();
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "Could not change protection", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { ReleaseComObject(task); }
        }

        private void UninstallApplication()
        {
            if (!CanUsePolicy(config.UninstallPolicy, DateTime.Now))
            {
                MessageBox.Show(this, PolicyUnavailableMessage(config.UninstallPolicy, "Uninstall is unavailable"), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
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
                int count = config.Executables.Length;
                bool blockedNow = IsBlockedNow(config, DateTime.Now);
                if (enabled)
                {
                    statusPanel.BackColor = Color.FromArgb(215, 244, 226);
                    statusTitle.ForeColor = Color.FromArgb(20, 106, 62);
                    statusTitle.Text = blockedNow ? "BLOCKING IS ACTIVE" : "PROTECTION IS ON";
                    statusDetail.Text = blockedNow ? FormatCount(count) + " blocked right now." : FormatCount(count) + " currently allowed; the schedule is still running.";
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
                ApplyPermissionState(enabled);
            }
            catch (Exception exception)
            {
                statusPanel.BackColor = Color.FromArgb(255, 241, 214);
                statusTitle.ForeColor = Color.FromArgb(120, 75, 0);
                statusTitle.Text = "NEEDS ATTENTION";
                statusDetail.Text = exception.Message;
            }
        }

        private void ApplyPermissionState(bool protectionEnabled)
        {
            DateTime now = DateTime.Now;
            bool canChangeTimes = CanUsePolicy(config.ChangeTimesPolicy, now);
            bool canRemove = CanUsePolicy(config.RemoveExecutablesPolicy, now);
            bool canUninstall = CanUsePolicy(config.UninstallPolicy, now);
            bool canTurnOff = CanUsePolicy(config.TurnOffProtectionPolicy, now);
            everyDayRadio.Enabled = canChangeTimes;
            splitScheduleRadio.Enabled = canChangeTimes;
            individualDaysRadio.Enabled = canChangeTimes;
            everydayStartPicker.Enabled = canChangeTimes;
            everydayEndPicker.Enabled = canChangeTimes;
            weekdayStartPicker.Enabled = canChangeTimes && weekdayEnabledCheckBox.Checked;
            weekdayEndPicker.Enabled = canChangeTimes && weekdayEnabledCheckBox.Checked;
            weekendStartPicker.Enabled = canChangeTimes && weekendEnabledCheckBox.Checked;
            weekendEndPicker.Enabled = canChangeTimes && weekendEnabledCheckBox.Checked;
            weekdayEnabledCheckBox.Enabled = canChangeTimes;
            weekendEnabledCheckBox.Enabled = canChangeTimes;
            for (int index = 0; index < 7; index++)
            {
                individualStartPickers[index].Enabled = canChangeTimes && individualEnabledCheckBoxes[index].Checked;
                individualEndPickers[index].Enabled = canChangeTimes && individualEnabledCheckBoxes[index].Checked;
                individualEnabledCheckBoxes[index].Enabled = canChangeTimes;
            }
            removeButton.Enabled = canRemove;
            uninstallButton.Enabled = canUninstall;
            toggleButton.Enabled = !protectionEnabled || canTurnOff;
            toolTip.SetToolTip(removeButton, canRemove ? "Remove the selected apps from the block list." : PolicyUnavailableMessage(config.RemoveExecutablesPolicy, "Removing apps is unavailable"));
            toolTip.SetToolTip(uninstallButton, canUninstall ? "Remove Windows App Blocker from this computer." : PolicyUnavailableMessage(config.UninstallPolicy, "Uninstall is unavailable"));
            toolTip.SetToolTip(toggleButton, !protectionEnabled || canTurnOff ? "Change whether protection is running." : PolicyUnavailableMessage(config.TurnOffProtectionPolicy, "Turning protection off is unavailable"));
            permissionSummary.Text = "Setup choices — change hours: " + PolicyDisplay(config.ChangeTimesPolicy) + "; turn off: " + PolicyDisplay(config.TurnOffProtectionPolicy) + "; remove apps: " + PolicyDisplay(config.RemoveExecutablesPolicy) + "; uninstall: " + PolicyDisplay(config.UninstallPolicy) + ".";
        }

        private static string PolicyDisplay(string policy)
        {
            if (policy == Never) return "never";
            if (policy == AllowedHoursOnly) return "allowed hours only";
            return "anytime";
        }

        private static string PolicyUnavailableMessage(string policy, string subject)
        {
            return policy == Never ? subject + " because you chose ‘No — never’ during setup." : subject + " during blocked hours. Try again during allowed hours.";
        }

        private static bool CanUsePolicy(string policy, DateTime currentTime, AppBlockerConfig currentConfig)
        {
            if (policy == Never) return false;
            if (policy == AllowedHoursOnly) return !IsBlockedNow(currentConfig, currentTime);
            return true;
        }

        private bool CanUsePolicy(string policy, DateTime currentTime)
        {
            return CanUsePolicy(policy, currentTime, config);
        }

        private static string FormatCount(int count)
        {
            return count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " app is" : " apps are");
        }

        private static bool IsBlockedNow(AppBlockerConfig currentConfig, DateTime currentTime)
        {
            if (currentConfig.ScheduleMode == EveryDay)
                return IsWithinTime(currentTime.TimeOfDay, currentConfig.BlockStart, currentConfig.BlockEnd);

            string todayStart;
            string todayEnd;
            bool todayEnabled;
            GetScheduleForDay(currentConfig, currentTime.DayOfWeek, out todayStart, out todayEnd, out todayEnabled);
            TimeSpan todayStartTime = ParseTime(todayStart);
            TimeSpan todayEndTime = ParseTime(todayEnd);
            TimeSpan now = currentTime.TimeOfDay;
            if (todayEnabled && todayStartTime < todayEndTime && now >= todayStartTime && now < todayEndTime) return true;
            if (todayEnabled && todayStartTime > todayEndTime && now >= todayStartTime) return true;

            string yesterdayStart;
            string yesterdayEnd;
            bool yesterdayEnabled;
            GetScheduleForDay(currentConfig, currentTime.AddDays(-1).DayOfWeek, out yesterdayStart, out yesterdayEnd, out yesterdayEnabled);
            TimeSpan previousStartTime = ParseTime(yesterdayStart);
            TimeSpan previousEndTime = ParseTime(yesterdayEnd);
            return yesterdayEnabled && previousStartTime > previousEndTime && now < previousEndTime;
        }

        private static bool IsWithinTime(TimeSpan now, string startValue, string endValue)
        {
            TimeSpan start = ParseTime(startValue);
            TimeSpan end = ParseTime(endValue);
            if (start < end) return now >= start && now < end;
            return now >= start || now < end;
        }

        private static TimeSpan ParseTime(string value)
        {
            return DateTime.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture).TimeOfDay;
        }

        private static void GetScheduleForDay(AppBlockerConfig currentConfig, DayOfWeek day, out string start, out string end, out bool enabled)
        {
            if (currentConfig.ScheduleMode == IndividualDays)
            {
                int index;
                switch (day)
                {
                    case DayOfWeek.Monday: index = 0; break;
                    case DayOfWeek.Tuesday: index = 1; break;
                    case DayOfWeek.Wednesday: index = 2; break;
                    case DayOfWeek.Thursday: index = 3; break;
                    case DayOfWeek.Friday: index = 4; break;
                    case DayOfWeek.Saturday: index = 5; break;
                    default: index = 6; break;
                }
                GetIndividualSchedule(currentConfig, index, out start, out end);
                enabled = GetIndividualEnabled(currentConfig, index);
                return;
            }
            bool weekend = day == DayOfWeek.Saturday || day == DayOfWeek.Sunday;
            start = weekend ? currentConfig.WeekendBlockStart : currentConfig.WeekdayBlockStart;
            end = weekend ? currentConfig.WeekendBlockEnd : currentConfig.WeekdayBlockEnd;
            enabled = weekend ? IsScheduleEnabled(currentConfig.WeekendEnabled) : IsScheduleEnabled(currentConfig.WeekdayEnabled);
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
            catch (FileNotFoundException) { return null; }
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
