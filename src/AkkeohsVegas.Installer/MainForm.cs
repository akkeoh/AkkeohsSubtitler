using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AkkeohsVegas.Core;

namespace AkkeohsVegas.Installer
{
    public class MainForm : Form
    {
        private readonly Panel _pageSelect;
        private readonly Panel _pageTerms;
        private readonly Panel _pageProgress;
        private readonly Panel _pageResult;

        private ComboBox _versionCombo;
        private Panel _modelRowsHost;
        private readonly List<ModelRow> _modelRows = new List<ModelRow>();
        private Label _installSizeLabel;
        private TextBox _termsBox;
        private CheckBox _acceptCheck;
        private TextBox _logBox;
        private Label _stepLabel;
        private Label _resultTitle;
        private Label _resultBody;

        private Button _backButton;
        private Button _nextButton;
        private Button _installButton;
        private Button _closeButton;
        private Button _cancelInstallButton;

        private int _pageIndex;
        private bool _busy;
        private CancellationTokenSource _installCts;
        private bool _resultIsSuccess;

        private sealed class ModelRow
        {
            public ModelDownloadOption Model;
            public CheckBox Check;
        }

        public MainForm()
        {
            Text = ProductInfo.DisplayName + " — Setup";
            Width = 680;
            Height = 560;
            MinimumSize = new Size(560, 480);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(241, 245, 249);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                    ?? SystemIcons.Application;
            }
            catch
            {
                Icon = SystemIcons.Application;
            }

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.White
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 10)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var logo = new PictureBox
            {
                Size = new Size(40, 40),
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0, 0, 10, 0),
                Image = LoadInstallerLogo()
            };
            header.Controls.Add(logo, 0, 0);

            _stepLabel = new Label
            {
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 10, 0, 0),
                Text = "Step 1 of 2 — Choose components"
            };
            header.Controls.Add(_stepLabel, 1, 0);
            root.Controls.Add(header);

            var pagesHost = new Panel { Dock = DockStyle.Fill };
            _pageSelect = BuildSelectPage();
            _pageTerms = BuildTermsPage();
            _pageProgress = BuildProgressPage();
            _pageResult = BuildResultPage();
            _pageSelect.Dock = DockStyle.Fill;
            _pageTerms.Dock = DockStyle.Fill;
            _pageProgress.Dock = DockStyle.Fill;
            _pageResult.Dock = DockStyle.Fill;
            pagesHost.Controls.Add(_pageResult);
            pagesHost.Controls.Add(_pageProgress);
            pagesHost.Controls.Add(_pageTerms);
            pagesHost.Controls.Add(_pageSelect);
            root.Controls.Add(pagesHost);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0)
            };

            _cancelInstallButton = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                Visible = false
            };
            _cancelInstallButton.Click += CancelInstallButton_Click;

            _closeButton = new Button { Text = "Cancel", AutoSize = true };
            _closeButton.Click += (s, e) =>
            {
                if (!_busy)
                    Close();
            };
            _installButton = new Button { Text = "Install", AutoSize = true, Visible = false };
            _installButton.Click += InstallButton_Click;
            _nextButton = new Button { Text = "Next", AutoSize = true };
            _nextButton.Click += NextButton_Click;
            _backButton = new Button { Text = "Back", AutoSize = true, Visible = false };
            _backButton.Click += (s, e) => ShowPage(0);

            buttons.Controls.Add(_cancelInstallButton);
            buttons.Controls.Add(_closeButton);
            buttons.Controls.Add(_installButton);
            buttons.Controls.Add(_nextButton);
            buttons.Controls.Add(_backButton);
            root.Controls.Add(buttons);

            Controls.Add(root);
            CancelButton = _closeButton;

            LoadDetectedVersions();
            PopulateModels();
            UpdateInstallSizeLabel();
            ShowPage(0);
        }

        private static Image LoadInstallerLogo()
        {
            try
            {
                var asm = typeof(MainForm).Assembly;
                string[] names = asm.GetManifestResourceNames();
                string resource = null;
                foreach (string name in names)
                {
                    if (name.EndsWith("akkeoh_64x64.png", StringComparison.OrdinalIgnoreCase))
                    {
                        resource = name;
                        break;
                    }
                }
                if (resource == null)
                    return null;
                using (Stream stream = asm.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                        return null;
                    return Image.FromStream(stream);
                }
            }
            catch
            {
                return null;
            }
        }

        private Panel BuildSelectPage()
        {
            var page = new Panel { Visible = true };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(new Label
            {
                Text = "VEGAS Pro version:",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            }, 0, 0);

            _versionCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Top,
                IntegralHeight = false,
                DisplayMember = "DisplayName",
                Margin = new Padding(0, 0, 0, 12)
            };
            layout.Controls.Add(_versionCombo, 0, 1);

            layout.Controls.Add(new Label
            {
                Text = "Speech models (Fast and Fast English are required):",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            }, 0, 2);

            var modelsOuter = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            _modelRowsHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12, 8, 12, 8)
            };
            modelsOuter.Controls.Add(_modelRowsHost);
            layout.Controls.Add(modelsOuter, 0, 3);

            _installSizeLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0),
                Font = new Font(Font, FontStyle.Bold)
            };
            layout.Controls.Add(_installSizeLabel, 0, 4);

            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildTermsPage()
        {
            var page = new Panel { Visible = false };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(new Label
            {
                Text = "Please review what will be installed and the applicable licenses:",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            }, 0, 0);

            _termsBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 8.25f),
                WordWrap = true,
                TabStop = false,
                HideSelection = true,
                ShortcutsEnabled = false
            };
            layout.Controls.Add(_termsBox, 0, 1);

            _acceptCheck = new CheckBox
            {
                Text = "I accept the terms and licenses above",
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };
            _acceptCheck.CheckedChanged += (s, e) => UpdateNavEnabled();
            layout.Controls.Add(_acceptCheck, 0, 2);

            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildProgressPage()
        {
            var page = new Panel { Visible = false };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label
            {
                Text = "Downloading and installing… This may take several minutes depending on selected models.",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            }, 0, 0);

            _logBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 8.25f)
            };
            layout.Controls.Add(_logBox, 0, 1);

            page.Controls.Add(layout);
            return page;
        }

        private Panel BuildResultPage()
        {
            var page = new Panel { Visible = false };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8, 24, 8, 8)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _resultTitle = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12)
            };
            layout.Controls.Add(_resultTitle, 0, 0);

            _resultBody = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f)
            };
            layout.Controls.Add(_resultBody, 0, 1);

            page.Controls.Add(layout);
            return page;
        }

        private void ShowPage(int index)
        {
            _pageIndex = index;
            _pageSelect.Visible = index == 0;
            _pageTerms.Visible = index == 1;
            _pageProgress.Visible = index == 2;
            _pageResult.Visible = index == 3;

            _backButton.Visible = false;
            _nextButton.Visible = false;
            _installButton.Visible = false;
            _cancelInstallButton.Visible = false;
            _closeButton.Visible = true;

            if (index == 0)
            {
                _stepLabel.Text = "Step 1 of 2 — Choose components";
                _nextButton.Visible = true;
                _closeButton.Text = "Cancel";
            }
            else if (index == 1)
            {
                _stepLabel.Text = "Step 2 of 2 — Accept terms";
                _termsBox.Text = DependencyCatalog.BuildTermsText(GetSelectedModels());
                ClearTermsSelection();
                _backButton.Visible = true;
                _installButton.Visible = true;
                _closeButton.Text = "Cancel";
                BeginInvoke(new Action(ClearTermsSelection));
            }
            else if (index == 2)
            {
                _stepLabel.Text = "Installing";
                _cancelInstallButton.Visible = _busy;
                _closeButton.Visible = !_busy;
                _closeButton.Text = "Close";
            }
            else
            {
                _stepLabel.Text = _resultIsSuccess ? "Finished" : "Install failed";
                _closeButton.Text = _resultIsSuccess ? "Close" : "Cancel";
                _closeButton.Visible = true;
            }

            UpdateNavEnabled();
        }

        private void ClearTermsSelection()
        {
            try
            {
                _termsBox.SelectionStart = 0;
                _termsBox.SelectionLength = 0;
                _acceptCheck.Focus();
            }
            catch { }
        }

        private void ShowResultPage(bool success, string reason)
        {
            _resultIsSuccess = success;
            if (success)
            {
                _resultTitle.Text = "Program is installed";
                _resultBody.Text =
                    "Restart VEGAS, then open View → Extensions → " + ProductInfo.DisplayName + "." +
                    Environment.NewLine + Environment.NewLine +
                    "You can uninstall later with akkeohs_subtitler_uninstall.exe or via Apps & Features.";
            }
            else
            {
                _resultTitle.Text = "There was an issue installing:";
                _resultBody.Text = string.IsNullOrWhiteSpace(reason) ? "Unknown error." : reason;
            }
            ShowPage(3);
        }

        private void UpdateNavEnabled()
        {
            bool hasVegas = SelectedInstance != null;
            _nextButton.Enabled = !_busy && hasVegas && _pageIndex == 0;
            _installButton.Enabled = !_busy && hasVegas && _pageIndex == 1 && _acceptCheck.Checked;
            _backButton.Enabled = !_busy && _pageIndex == 1;
            _closeButton.Enabled = !_busy;
            _cancelInstallButton.Enabled = _busy && _pageIndex == 2
                && _installCts != null && !_installCts.IsCancellationRequested;
        }

        private void LoadDetectedVersions()
        {
            _versionCombo.Items.Clear();
            IList<VegasExtensionTarget> installed = AppPaths.DiscoverInstalledVegasInstances();
            foreach (VegasExtensionTarget instance in installed)
                _versionCombo.Items.Add(instance);

            if (_versionCombo.Items.Count > 0)
            {
                int preferred = -1;
                for (int i = 0; i < _versionCombo.Items.Count; i++)
                {
                    var t = _versionCombo.Items[i] as VegasExtensionTarget;
                    if (t != null && string.Equals(t.VersionFolder, "15.0", StringComparison.OrdinalIgnoreCase))
                    {
                        preferred = i;
                        break;
                    }
                }
                _versionCombo.SelectedIndex = preferred >= 0 ? preferred : 0;
            }

            _versionCombo.SelectedIndexChanged += (s, e) => UpdateNavEnabled();
            UpdateNavEnabled();
        }

        private void PopulateModels()
        {
            _modelRowsHost.Controls.Clear();
            _modelRows.Clear();

            const int leftPad = 10;
            const int rightPad = 10;
            const int sizeWidth = 86;
            int y = 4;
            foreach (ModelDownloadOption model in DependencyCatalog.Models)
            {
                int rowWidth = Math.Max(200, _modelRowsHost.ClientSize.Width - leftPad - rightPad - 8);
                var row = new Panel
                {
                    Location = new Point(leftPad, y),
                    Height = 28,
                    Width = rowWidth,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                };

                var check = new CheckBox
                {
                    Text = model.DisplayName + (model.Required ? " (required)" : ""),
                    AutoSize = false,
                    Location = new Point(0, 2),
                    Height = 24,
                    Width = Math.Max(120, row.Width - sizeWidth - 8),
                    Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                    Checked = model.Required,
                    Enabled = !model.Required,
                    ForeColor = model.Required
                        ? Color.FromArgb(100, 116, 139)
                        : SystemColors.ControlText
                };
                if (!model.Required)
                    check.CheckedChanged += (s, e) => UpdateInstallSizeLabel();

                var sizeLabel = new Label
                {
                    Text = model.ApproxSize,
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleRight,
                    Location = new Point(row.Width - sizeWidth, 2),
                    Width = sizeWidth,
                    Height = 24,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    ForeColor = model.Required
                        ? Color.FromArgb(148, 163, 184)
                        : Color.FromArgb(71, 85, 105)
                };

                row.Controls.Add(check);
                row.Controls.Add(sizeLabel);
                _modelRowsHost.Controls.Add(row);
                _modelRows.Add(new ModelRow { Model = model, Check = check });
                y += 30;
            }

            _modelRowsHost.Resize += (s, e) =>
            {
                foreach (Control c in _modelRowsHost.Controls)
                {
                    c.Width = Math.Max(200, _modelRowsHost.ClientSize.Width - leftPad - rightPad - 8);
                    c.Left = leftPad;
                }
            };
        }

        private void UpdateInstallSizeLabel()
        {
            long bytes = DependencyCatalog.EstimateInstallSizeBytes(GetSelectedModels());
            _installSizeLabel.Text = "Install size: " + DependencyCatalog.FormatSize(bytes);
        }

        private VegasExtensionTarget SelectedInstance
        {
            get { return _versionCombo.SelectedItem as VegasExtensionTarget; }
        }

        private List<ModelDownloadOption> GetSelectedModels()
        {
            var list = new List<ModelDownloadOption>();
            foreach (ModelRow row in _modelRows)
            {
                if (row.Model == null)
                    continue;
                if (row.Model.Required || (row.Check != null && row.Check.Checked))
                    list.Add(row.Model);
            }
            return list;
        }

        private static string ResolvePayloadRoot()
        {
            if (EmbeddedPayload.HasEmbeddedAssemblies())
                return null;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "payload"),
                baseDir,
                Path.GetFullPath(Path.Combine(baseDir, "..", "payload")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "dist", "payload"))
            };

            foreach (string candidate in candidates)
            {
                if (!Directory.Exists(candidate))
                    continue;
                if (File.Exists(Path.Combine(candidate, "AkkeohsVegas.Extension.dll"))
                    || File.Exists(Path.Combine(candidate, "AkkeohsVegas.Core.dll")))
                {
                    return candidate;
                }
            }

            string beside = Path.Combine(baseDir, "payload");
            return Directory.Exists(beside) ? beside : null;
        }

        private static bool HasInstallPayload()
        {
            return EmbeddedPayload.HasEmbeddedAssemblies() || ResolvePayloadRoot() != null;
        }

        private void Log(string message)
        {
            if (_logBox.InvokeRequired)
            {
                _logBox.BeginInvoke(new Action<string>(Log), message);
                return;
            }

            if (_logBox.TextLength > 0)
                _logBox.AppendText(Environment.NewLine);
            _logBox.AppendText(message);
        }

        private void NextButton_Click(object sender, EventArgs e)
        {
            if (SelectedInstance == null)
            {
                MessageBox.Show(this, "Select a detected VEGAS Pro version.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!HasInstallPayload())
            {
                MessageBox.Show(
                    this,
                    "This installer is missing the bundled plugin files.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            _acceptCheck.Checked = false;
            ShowPage(1);
        }

        private async void InstallButton_Click(object sender, EventArgs e)
        {
            if (!_acceptCheck.Checked)
                return;

            VegasExtensionTarget selected = SelectedInstance;
            if (selected == null)
                return;

            if (!HasInstallPayload())
            {
                ShowResultPage(false, "This installer is missing the bundled plugin files.");
                return;
            }

            string payload = ResolvePayloadRoot();

            List<ModelDownloadOption> models = GetSelectedModels();
            if (_installCts != null)
            {
                _installCts.Dispose();
                _installCts = null;
            }
            _installCts = new CancellationTokenSource();
            CancellationToken token = _installCts.Token;

            _busy = true;
            _cancelInstallButton.Text = "Cancel";
            ShowPage(2);
            _logBox.Clear();
            Log("Starting installation…");
            UpdateNavEnabled();

            try
            {
                await Task.Run(() =>
                {
                    var service = new InstallService(msg => Log(msg));
                    service.Install(payload, selected, models, token);
                }, token);

                Log("");
                Log("Done.");
                _busy = false;
                ShowResultPage(true, null);
            }
            catch (OperationCanceledException)
            {
                Log("Installation cancelled. Partial files were removed.");
                _busy = false;
                ShowResultPage(false, "Installation was cancelled.");
            }
            catch (Exception ex)
            {
                _busy = false;
                if (token.IsCancellationRequested)
                {
                    Log("Installation cancelled. Partial files were removed.");
                    ShowResultPage(false, "Installation was cancelled.");
                }
                else
                {
                    Log("ERROR: " + ex.Message);

                    try
                    {
                        var cleanup = new InstallService(msg => Log(msg));
                        cleanup.CleanupAfterFailedInstall();
                    }
                    catch { }
                    ShowResultPage(false, ex.Message);
                }
            }
            finally
            {
                _busy = false;
                if (_installCts != null)
                {
                    _installCts.Dispose();
                    _installCts = null;
                }
                UpdateNavEnabled();
            }
        }

        private void CancelInstallButton_Click(object sender, EventArgs e)
        {
            if (!_busy || _installCts == null || _installCts.IsCancellationRequested)
                return;

            _cancelInstallButton.Enabled = false;
            _cancelInstallButton.Text = "Cancelling…";
            Log("Cancel requested — stopping downloads and cleaning up…");
            try
            {
                _installCts.Cancel();
            }
            catch { }
        }
    }
}
