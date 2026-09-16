using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScriptPortal.Vegas;
using AkkeohsVegas.Core;

namespace AkkeohsVegas.Extension
{
    /// <summary>
    /// Dockable panel for Akkeoh's Subtitler.
    /// Absolute layout (VEGAS dock hosts collapse TableLayoutPanel).
    /// </summary>
    public class AkkeohsDockView : DockableControl
    {
        public const string ViewId = "AkkeohSubtitlerView";

        private const int Pad = 14;
        private const int LabelWidth = 100;
        private const int RowGap = 10;
        private const int MinContentWidth = 420;
        private const int CardRadius = 14;

        private readonly Vegas _vegas;
        private PluginSettings _settings;

        private Panel _card;
        private Label _titleLabel;
        private Label _modelLabel;
        private Label _languageLabel;
        private Label _threadsLabel;
        private Label _trackLabel;
        private Label _panCropLabel;
        private Label _fontLabel;
        private Label _fontStyleLabel;
        private Label _fontSizeLabel;
        private Label _textColorLabel;
        private Label _outlineLabel;
        private Label _outlineColorLabel;
        private Label _timingLabel;

        private RoundedComboHost _modelHost;
        private RoundedComboHost _languageHost;
        private RoundedComboHost _panCropHost;
        private RoundedComboHost _fontHost;
        private RoundedComboHost _fontStyleHost;
        private StyledNumericHost _threadsHost;
        private RoundedFieldHost _trackHost;
        private StyledNumericHost _fontSizeHost;
        private StyledNumericHost _outlineHost;
        private StyledNumericHost _timingHost;

        private FlatComboBox _modelCombo;
        private FlatComboBox _languageCombo;
        private CheckBox _translateCheck;
        private TextBox _trackNameBox;
        private FlatComboBox _panCropCombo;
        private FlatComboBox _fontCombo;
        private FlatComboBox _fontStyleCombo;
        private ColorSwatchButton _textColorSwatch;
        private ColorSwatchButton _outlineColorSwatch;

        private RoundedButton _generateButton;

        private volatile bool _busy;
        private string _languageBeforeEnglishLock = "auto";
        private bool _seededVegasOutlineDefaults;
        private bool _loadingUi;

        public AkkeohsDockView(Vegas vegas)
            : base(ViewId)
        {
            _vegas = vegas;
            _settings = PluginSettings.Load();

            DisplayName = ProductInfo.DisplayName;
            DefaultDockWindowStyle = DockWindowStyle.Floating;
            DefaultFloatingSize = new Size(480, 720);
            PersistDockWindowState = false;

            BuildUi();
            SeedOutlineDefaultsFromVegas();
            LoadSettingsIntoUi();

            Resize += (s, e) => LayoutControls();
            VisibleChanged += (s, e) =>
            {
                if (Visible)
                    EnsureUsableHostSize();
            };
            HandleCreated += (s, e) =>
            {
                BeginInvoke(new Action(() =>
                {
                    EnsureUsableHostSize();
                    LayoutControls();
                }));
            };
            Disposed += (s, e) => PersistSettings();
        }

        public override DockWindowStyle DefaultDockWindowStyle
        {
            get { return DockWindowStyle.Floating; }
            set { }
        }

        public override Size DefaultFloatingSize
        {
            get { return new Size(480, 720); }
            set { }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            PersistSettings();
            base.OnHandleDestroyed(e);
        }

        private void BuildUi()
        {
            SuspendLayout();

            AutoScaleMode = AutoScaleMode.None;
            AutoScroll = true;
            BackColor = UiColors.Bg;
            MinimumSize = new Size(360, 480);
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            _card = new Panel { BackColor = UiColors.Card };
            _card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = UiDraw.RoundedRect(new Rectangle(0, 0, _card.Width - 1, _card.Height - 1), CardRadius))
                using (var pen = new Pen(UiColors.Border, 1f))
                    e.Graphics.DrawPath(pen, path);
            };
            _card.Resize += (s, e) => UiDraw.ApplyRoundedRegion(_card, CardRadius);

            _titleLabel = new Label
            {
                Text = ProductInfo.DisplayName,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = UiColors.Text,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _modelLabel = MakeLabel("Model");
            _languageLabel = MakeLabel("Language");
            _threadsLabel = MakeLabel("Threads");
            _trackLabel = MakeLabel("Track");
            _panCropLabel = MakeLabel("Pan / Crop");
            _fontLabel = MakeLabel("Font");
            _fontStyleLabel = MakeLabel("Text style");
            _fontSizeLabel = MakeLabel("Text size");
            _textColorLabel = MakeLabel("Text color");
            _outlineLabel = MakeLabel("Outline");
            _outlineColorLabel = MakeLabel("Outline color");
            _timingLabel = MakeLabel("Delay ms");

            _modelCombo = new FlatComboBox();
            _modelCombo.SelectedIndexChanged += (s, e) =>
            {
                ApplyEnglishModelLanguageLock();
                PersistSettings();
            };
            _modelHost = new RoundedComboHost(_modelCombo);

            _languageCombo = new FlatComboBox();
            _languageCombo.Items.AddRange(LanguageCatalog.All);
            _languageCombo.SelectedIndexChanged += (s, e) => PersistSettings();
            _languageHost = new RoundedComboHost(_languageCombo);

            _threadsHost = new StyledNumericHost(1, 64, 1);
            _threadsHost.ValueChanged += (s, e) => PersistSettings();

            _fontCombo = new FlatComboBox();
            _fontCombo.Items.AddRange(VegasFontCatalog.GetNames());
            _fontCombo.SelectedIndexChanged += (s, e) => PersistSettings();
            _fontHost = new RoundedComboHost(_fontCombo);

            _fontStyleCombo = new FlatComboBox();
            _fontStyleCombo.Items.AddRange(new object[] { "Capitalized", "Normal" });
            _fontStyleCombo.SelectedIndexChanged += (s, e) => PersistSettings();
            _fontStyleHost = new RoundedComboHost(_fontStyleCombo);

            _fontSizeHost = new StyledNumericHost(8, 72, 14);
            _fontSizeHost.ValueChanged += (s, e) => PersistSettings();

            _textColorSwatch = new ColorSwatchButton();
            _textColorSwatch.ColorChanged += (s, e) => PersistSettings();

            _outlineHost = new StyledNumericHost(0, 10, 2);
            _outlineHost.ValueChanged += (s, e) => PersistSettings();

            _outlineColorSwatch = new ColorSwatchButton();
            _outlineColorSwatch.ColorChanged += (s, e) => PersistSettings();

            _timingHost = new StyledNumericHost(-5000, 5000, 0, 50);
            _timingHost.ValueChanged += (s, e) => PersistSettings();

            _translateCheck = new CheckBox
            {
                Text = "Translate to English",
                AutoSize = false,
                ForeColor = UiColors.Text,
                FlatStyle = FlatStyle.Flat,
                Height = 24
            };
            _translateCheck.CheckedChanged += (s, e) => PersistSettings();

            _trackNameBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = UiColors.InputBg,
                ForeColor = UiColors.Text,
                Font = new Font("Segoe UI", 9f)
            };
            _trackNameBox.Leave += (s, e) => PersistSettings();
            _trackHost = new RoundedFieldHost(_trackNameBox, 32);

            _panCropCombo = new FlatComboBox();
            _panCropCombo.Items.AddRange(PanCropPlacements.All);
            _panCropCombo.SelectedIndexChanged += (s, e) => PersistSettings();
            _panCropHost = new RoundedComboHost(_panCropCombo);

            _generateButton = new RoundedButton("Generate", 160, true);
            _generateButton.Click += GenerateButton_Click;

            _card.Controls.Add(_titleLabel);
            _card.Controls.Add(_modelLabel);
            _card.Controls.Add(_modelHost);
            _card.Controls.Add(_languageLabel);
            _card.Controls.Add(_languageHost);
            _card.Controls.Add(_threadsLabel);
            _card.Controls.Add(_threadsHost);
            _card.Controls.Add(_translateCheck);
            _card.Controls.Add(_trackLabel);
            _card.Controls.Add(_trackHost);
            _card.Controls.Add(_panCropLabel);
            _card.Controls.Add(_panCropHost);
            _card.Controls.Add(_fontLabel);
            _card.Controls.Add(_fontHost);
            _card.Controls.Add(_fontStyleLabel);
            _card.Controls.Add(_fontStyleHost);
            _card.Controls.Add(_fontSizeLabel);
            _card.Controls.Add(_fontSizeHost);
            _card.Controls.Add(_textColorLabel);
            _card.Controls.Add(_textColorSwatch);
            _card.Controls.Add(_outlineLabel);
            _card.Controls.Add(_outlineHost);
            _card.Controls.Add(_outlineColorLabel);
            _card.Controls.Add(_outlineColorSwatch);
            _card.Controls.Add(_timingLabel);
            _card.Controls.Add(_timingHost);
            _card.Controls.Add(_generateButton);

            Controls.Add(_card);
            ResumeLayout(false);
            LayoutControls();
        }

        private void SeedOutlineDefaultsFromVegas()
        {
            if (_seededVegasOutlineDefaults || _vegas == null)
                return;
            _seededVegasOutlineDefaults = true;

            bool looksLikeStock =
                Math.Abs(_settings.OutlineWidth - 2.0) < 0.001
                && _settings.OutlineColorArgb == PluginSettings.DefaultOutlineColorArgb;

            if (!looksLikeStock)
                return;

            double width;
            Color outlineColor;
            if (VegasSubtitlePlacer.TryReadVegasTextDefaults(_vegas, _settings.TextGeneratorName, out width, out outlineColor))
            {
                _settings.OutlineWidth = width;
                _settings.OutlineColorArgb = outlineColor.ToArgb();
            }
        }

        private void EnsureUsableHostSize()
        {
            try
            {
                Form host = FindForm();
                if (host != null)
                {
                    if (host.Width < MinContentWidth)
                        host.Width = 480;
                    if (host.Height < 480)
                        host.Height = 720;
                }
            }
            catch { }

            if (Width < 200 || Height < 200)
                Size = new Size(Math.Max(Width, 480), Math.Max(Height, 720));

            LayoutControls();
        }

        private void LayoutControls()
        {
            if (_modelHost == null || _card == null)
                return;

            int viewW = Math.Max(ClientSize.Width, MinContentWidth);
            int cardPad = 16;
            int cardW = Math.Max(320, viewW - Pad * 2);
            int innerW = cardW - cardPad * 2;
            int fieldLeft = cardPad + LabelWidth + 10;
            int fieldWidth = Math.Max(140, innerW - LabelWidth - 10);
            int y = cardPad;
            const int fieldH = 32;

            _titleLabel.SetBounds(cardPad, y, innerW, 30);
            y += 38;

            PlaceLabeled(_modelLabel, _modelHost, ref y, fieldLeft, fieldWidth, fieldH, cardPad);
            PlaceLabeled(_languageLabel, _languageHost, ref y, fieldLeft, fieldWidth, fieldH, cardPad);

            _threadsLabel.SetBounds(cardPad, y + 6, LabelWidth, 20);
            _threadsHost.SetBounds(fieldLeft, y, 100, fieldH);
            y += fieldH + RowGap;

            _translateCheck.SetBounds(fieldLeft, y, Math.Min(fieldWidth, 240), 24);
            y += 28 + RowGap;

            PlaceLabeled(_trackLabel, _trackHost, ref y, fieldLeft, fieldWidth, fieldH, cardPad);
            PlaceLabeled(_panCropLabel, _panCropHost, ref y, fieldLeft, fieldWidth, fieldH, cardPad);
            PlaceLabeled(_fontLabel, _fontHost, ref y, fieldLeft, fieldWidth, fieldH, cardPad);
            PlaceLabeled(_fontStyleLabel, _fontStyleHost, ref y, fieldLeft, fieldWidth, fieldH, cardPad);

            _fontSizeLabel.SetBounds(cardPad, y + 6, LabelWidth, 20);
            _fontSizeHost.SetBounds(fieldLeft, y, 100, fieldH);
            y += fieldH + RowGap;

            _textColorLabel.SetBounds(cardPad, y + 6, LabelWidth, 20);
            _textColorSwatch.SetBounds(fieldLeft, y, 72, fieldH);
            y += fieldH + RowGap;

            _outlineLabel.SetBounds(cardPad, y + 6, LabelWidth, 20);
            _outlineHost.SetBounds(fieldLeft, y, 100, fieldH);
            y += fieldH + RowGap;

            _outlineColorLabel.SetBounds(cardPad, y + 6, LabelWidth, 20);
            _outlineColorSwatch.SetBounds(fieldLeft, y, 72, fieldH);
            y += fieldH + RowGap;

            _timingLabel.SetBounds(cardPad, y + 6, LabelWidth, 20);
            _timingHost.SetBounds(fieldLeft, y, 100, fieldH);
            y += fieldH + RowGap + 6;

            int btnX = cardPad + Math.Max(0, (innerW - _generateButton.Width) / 2);
            _generateButton.SetBounds(btnX, y, _generateButton.Width, 36);
            y += 48;

            int cardH = y + cardPad;
            _card.SetBounds(Pad, Pad, cardW, cardH);
            UiDraw.ApplyRoundedRegion(_card, CardRadius);
            AutoScrollMinSize = new Size(MinContentWidth, cardH + Pad * 2);
        }

        private static void PlaceLabeled(Label label, Control field, ref int y, int fieldLeft, int fieldWidth, int height, int labelLeft)
        {
            label.SetBounds(labelLeft, y + 6, LabelWidth, 20);
            field.SetBounds(fieldLeft, y, fieldWidth, height);
            y += height + RowGap;
        }

        private static Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                ForeColor = UiColors.Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f)
            };
        }

        private void LoadSettingsIntoUi()
        {
            _loadingUi = true;
            try
            {
                RefreshInstalledModels();

                NamedOption lang = LanguageCatalog.FindByCode(_settings.Language);
                SelectOption(_languageCombo, lang.Id);

                _threadsHost.Value = ClampDecimal(_threadsHost, _settings.Threads);
                _translateCheck.Checked = _settings.TranslateToEnglish;
                _trackNameBox.Text = string.IsNullOrWhiteSpace(_settings.TrackName)
                    ? ProductInfo.DefaultTrackName
                    : _settings.TrackName;

                string pan = PanCropPlacements.Normalize(_settings.PanCropPlacement);
                int panIndex = _panCropCombo.Items.IndexOf(pan);
                _panCropCombo.SelectedIndex = panIndex >= 0 ? panIndex : 0;

                string font = VegasFontCatalog.Resolve(_settings.FontFamily);
                int fontIndex = _fontCombo.FindStringExact(font);
                if (fontIndex < 0)
                    fontIndex = _fontCombo.FindStringExact("Arial");
                _fontCombo.SelectedIndex = fontIndex >= 0 ? fontIndex : 0;

                _fontStyleCombo.SelectedIndex =
                    string.Equals(_settings.TextStyle, "Normal", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                _fontSizeHost.Value = ClampDecimal(_fontSizeHost, (decimal)_settings.FontSize);
                _textColorSwatch.SelectedColor = Color.FromArgb(_settings.TextColorArgb);
                _outlineHost.Value = ClampDecimal(_outlineHost, (decimal)_settings.OutlineWidth);
                _outlineColorSwatch.SelectedColor = Color.FromArgb(_settings.OutlineColorArgb);
                _timingHost.Value = ClampDecimal(_timingHost, (decimal)_settings.TimingOffsetMs);

                ApplyEnglishModelLanguageLock();
            }
            finally
            {
                _loadingUi = false;
            }
        }

        private void RefreshInstalledModels()
        {
            string modelsDir = !string.IsNullOrWhiteSpace(_settings.ModelsDirectory)
                ? _settings.ModelsDirectory
                : AppPaths.DefaultModelsDirectory;

            NamedOption[] installed = ModelCatalog.GetInstalled(modelsDir);
            string preferred = _settings.ModelFileName;

            _modelCombo.Items.Clear();
            foreach (NamedOption opt in installed)
                _modelCombo.Items.Add(opt);

            if (installed.Length == 0)
            {
                _modelCombo.Enabled = false;
                return;
            }

            _modelCombo.Enabled = true;
            NamedOption pick = ModelCatalog.FindInstalledOrFirst(preferred, installed);
            SelectOption(_modelCombo, pick.Id);
            _settings.ModelFileName = pick.Id;
        }

        private void ReadUiIntoSettings()
        {
            var model = _modelCombo.SelectedItem as NamedOption;
            _settings.ModelFileName = model != null ? model.Id : "ggml-tiny.bin";

            if (ModelCatalog.IsEnglishOnly(_settings.ModelFileName))
                _settings.Language = "en";
            else
                _settings.Language = LanguageCatalog.ToCode(_languageCombo.SelectedItem);

            _settings.Threads = (int)_threadsHost.Value;
            _settings.TranslateToEnglish = _translateCheck.Checked;
            _settings.TrackName = string.IsNullOrWhiteSpace(_trackNameBox.Text)
                ? ProductInfo.DefaultTrackName
                : _trackNameBox.Text.Trim();
            _settings.PanCropPlacement = _panCropCombo.SelectedItem != null
                ? PanCropPlacements.Normalize(_panCropCombo.SelectedItem.ToString())
                : PanCropPlacements.LowerThird;
            _settings.FontFamily = _fontCombo.SelectedItem != null
                ? _fontCombo.SelectedItem.ToString()
                : "Arial";
            _settings.TextStyle = _fontStyleCombo.SelectedIndex == 1 ? "Normal" : "Capitalized";
            _settings.FontSize = (float)_fontSizeHost.Value;
            _settings.TextColorArgb = _textColorSwatch.SelectedColor.ToArgb();
            _settings.OutlineWidth = (double)_outlineHost.Value;
            _settings.OutlineColorArgb = _outlineColorSwatch.SelectedColor.ToArgb();
            _settings.TimingOffsetMs = (double)_timingHost.Value;
        }

        private void PersistSettings()
        {
            if (_loadingUi || _settings == null)
                return;
            try
            {
                ReadUiIntoSettings();
                _settings.Save();
            }
            catch { }
        }

        private void ApplyEnglishModelLanguageLock()
        {
            var model = _modelCombo.SelectedItem as NamedOption;
            bool englishOnly = model != null && ModelCatalog.IsEnglishOnly(model.Id);

            if (englishOnly)
            {
                if (_languageCombo.Enabled)
                    _languageBeforeEnglishLock = LanguageCatalog.ToCode(_languageCombo.SelectedItem);

                SelectOption(_languageCombo, "en");
                _languageCombo.Enabled = false;
                _languageHost.Enabled = false;
                _languageLabel.ForeColor = Color.FromArgb(148, 163, 184);
            }
            else
            {
                _languageCombo.Enabled = true;
                _languageHost.Enabled = true;
                _languageLabel.ForeColor = UiColors.Muted;
                if (!string.IsNullOrEmpty(_languageBeforeEnglishLock))
                    SelectOption(_languageCombo, _languageBeforeEnglishLock);
            }
        }

        private static void SelectOption(ComboBox combo, string id)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var opt = combo.Items[i] as NamedOption;
                if (opt != null && string.Equals(opt.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private static decimal ClampDecimal(StyledNumericHost box, decimal value)
        {
            if (value < box.Minimum) return box.Minimum;
            if (value > box.Maximum) return box.Maximum;
            return value;
        }

        private void ReportProgress(string message, int? percent)
        {
            // Progress is shown only via the Generate button spinner.
        }

        private void SetBusy(bool busy)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(SetBusy), busy);
                return;
            }

            _busy = busy;
            _generateButton.ShowLoading = busy;
            _modelHost.Enabled = !busy && _modelCombo.Items.Count > 0;
            _languageHost.Enabled = !busy;
            _threadsHost.Enabled = !busy;
            _translateCheck.Enabled = !busy;
            _trackHost.Enabled = !busy;
            _panCropHost.Enabled = !busy;
            _fontHost.Enabled = !busy;
            _fontStyleHost.Enabled = !busy;
            _fontSizeHost.Enabled = !busy;
            _textColorSwatch.Enabled = !busy;
            _outlineHost.Enabled = !busy;
            _outlineColorSwatch.Enabled = !busy;
            _timingHost.Enabled = !busy;

            if (!busy)
                ApplyEnglishModelLanguageLock();
        }

        private void GenerateButton_Click(object sender, EventArgs e)
        {
            if (_busy)
                return;

            if (_modelCombo.Items.Count == 0)
            {
                MessageBox.Show(
                    "No speech models are installed.\n\nRe-run the installer and select at least Fast / Fast English.",
                    ProductInfo.DisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            PersistSettings();
            SetBusy(true);

            PluginSettings settingsSnapshot = CloneSettings(_settings);
            List<ClipAudioRequest> clips;

            try
            {
                clips = VegasClipCollector.CollectSelected(_vegas.Project);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ProductInfo.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetBusy(false);
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    var pipeline = new TranscriptionPipeline(settingsSnapshot);
                    return (object)pipeline.TranscribeClips(clips, (msg, pct) => ReportProgress(msg, pct));
                }
                catch (Exception ex)
                {
                    return (object)ex;
                }
            }).ContinueWith(t =>
            {
                RunOnUi(() => FinishTranscription(t.Result, settingsSnapshot));
            });
        }

        private void FinishTranscription(object result, PluginSettings settingsSnapshot)
        {
            try
            {
                var workerError = result as Exception;
                if (workerError != null)
                {
                    MessageBox.Show(workerError.Message, ProductInfo.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var subtitles = result as IList<TimedSubtitle>;
                if (subtitles == null || subtitles.Count == 0)
                {
                    MessageBox.Show(
                        "Transcription finished but produced no subtitle segments.",
                        ProductInfo.DisplayName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                int placed = VegasSubtitlePlacer.Place(_vegas, subtitles, settingsSnapshot);

                try { _settings.Save(); }
                catch { }

                MessageBox.Show(
                    "Created " + placed + " subtitle block(s) on a new track.",
                    ProductInfo.DisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ProductInfo.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RunOnUi(Action action)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
                Invoke(action);
            else
                action();
        }

        private static PluginSettings CloneSettings(PluginSettings s)
        {
            return new PluginSettings
            {
                AkkeohsCliPath = s.AkkeohsCliPath,
                FfmpegPath = s.FfmpegPath,
                ModelsDirectory = s.ModelsDirectory,
                ModelFileName = s.ModelFileName,
                Language = s.Language,
                Threads = s.Threads,
                TranslateToEnglish = s.TranslateToEnglish,
                MaxSegmentChars = s.MaxSegmentChars,
                TrackName = s.TrackName,
                TextGeneratorName = s.TextGeneratorName,
                FontFamily = s.FontFamily,
                FontSize = s.FontSize,
                TextStyle = s.TextStyle,
                TextColorArgb = s.TextColorArgb,
                OutlineWidth = s.OutlineWidth,
                OutlineColorArgb = s.OutlineColorArgb,
                PanCropPlacement = s.PanCropPlacement,
                TimingOffsetMs = s.TimingOffsetMs
            };
        }
    }
}
