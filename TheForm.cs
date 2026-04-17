using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Guna.UI2.WinForms;

namespace MediaTransferUtility;

public sealed class TheForm : Form
{
    private const int BUTTON_WIDTH = 130;
    private const int BUTTON_HEIGHT= 36;

    private const string APP_TITLE = "Media Transfer Utility";

    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isTransferInProgress;

    private readonly Guna2TextBox _txtSource = CreateTextBox("Select source folder...");
    private readonly Guna2TextBox _txtDestination = CreateTextBox("Select destination folder...");
    private readonly Guna2TextBox _txtDestinationFolder = CreateTextBox("Original", 280);

    private readonly Guna2Button _btnBrowseSource = CreateSecondaryButton("Browse...");
    private readonly Guna2Button _btnBrowseDestination = CreateSecondaryButton("Browse...");
    private readonly Guna2Button _btnStart = CreatePrimaryButton("Start Transfer");
    private readonly Guna2Button _btnCancel = CreateDangerButton("Cancel");
    private readonly Guna2Button _btnClose = CreateSecondaryButton("Close");
    private readonly Guna2Button _btnClearLog = CreateSecondaryButton("Clear Log");

    private readonly Guna2CheckBox _chkRemoveSource = CreateCheckBox("Remove source file after successful copy");
    private readonly Guna2CheckBox _chkCreateEdits = CreateCheckBox("Create 'Edits' folder");
    private readonly Guna2CheckBox _chkCreateFinal = CreateCheckBox("Create 'Final' folder");
    private readonly Guna2CheckBox _chkSaveLog = CreateCheckBox("Save log file after run");
    private readonly Guna2CheckBox _chkDarkTheme = CreateCheckBox("Theme: Dark mode");

    private readonly Guna2ProgressBar _progressBar = new();
    private readonly Guna2HtmlLabel _lblStatus = new();
    private readonly Guna2DataGridView _logGrid = new();
    private Guna2Panel? _foldersCard;
    private Guna2Panel? _optionsCard;
    private Guna2Panel? _progressCard;
    private Guna2Panel? _logCard;
    private readonly TableLayoutPanel _footerLayout = new();
    private static readonly string StateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MediaOrganizerApp",
        "appstate.json");

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".heic", ".webp"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v", ".3gp", ".mts", ".m2ts"
    };

    private static readonly Regex FilenameDateRegex = new(@"(?<!\d)(19\d{2}|20\d{2})(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])(?!\d)", RegexOptions.Compiled);

    public TheForm()
    {
        InitializeUi();
        WireEvents();
        var loaded = TryLoadState();
        if (!loaded)
        {
            _chkDarkTheme.Checked = true;
        }

        ApplyTheme(_chkDarkTheme.Checked);
        UpdateLogUiState();
    }

    private void InitializeUi()
    {
        Text = APP_TITLE;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = true;
        MinimumSize = new Size(1000, 760);
        ClientSize = new Size(1320, 980);
        Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 6
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 280));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var header = new Guna2HtmlLabel
        {
            Text = APP_TITLE,
            Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            AutoSize = true
        };

        _foldersCard = CreateCard("Folders", CreateFoldersLayout());
        _foldersCard.Margin = new Padding(0, 12, 0, 12);

        _optionsCard = CreateCard("Options", CreateOptionsLayout());

        _progressCard = CreateCard("Progress", CreateProgressLayout());

        _logCard = CreateCard("Detailed Log", CreateLogLayout());

        _footerLayout.ColumnCount = 2;
        _footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        _footerLayout.Dock = DockStyle.Fill;
        //_footerLayout.Padding = new Padding(0, 4, 0, 0);

        _btnClose.Dock = DockStyle.Right;
        _btnClose.Size = new Size(100, BUTTON_HEIGHT);
        _footerLayout.Controls.Add(_btnClose, 1, 0);

        mainLayout.Controls.Add(header, 0, 0);
        mainLayout.Controls.Add(_foldersCard, 0, 1);
        mainLayout.Controls.Add(_optionsCard, 0, 2);
        mainLayout.Controls.Add(_progressCard, 0, 3);
        mainLayout.Controls.Add(_logCard, 0, 4);
        mainLayout.Controls.Add(_footerLayout, 0, 5);

        Controls.Add(mainLayout);
    }

    private Control CreateFoldersLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 10, 0, 0),
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        var lblSource = CreateLabel("Source");
        lblSource.AutoSize = false;
        lblSource.TextAlignment = ContentAlignment.MiddleLeft;
        lblSource.Margin = new Padding(0, 0, 8, 0);
        lblSource.Dock = DockStyle.Fill;

        var lblDestination = CreateLabel("Destination");
        lblDestination.AutoSize = false;
        lblDestination.TextAlignment = ContentAlignment.MiddleLeft;
        lblDestination.Margin = new Padding(0, 0, 8, 0);
        lblDestination.Dock = DockStyle.Fill;

        _txtSource.Dock = DockStyle.None;
        _txtDestination.Dock = DockStyle.None;
        _txtSource.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _txtDestination.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _txtSource.AutoSize = false;
        _txtDestination.AutoSize = false;
        _txtSource.Multiline = false;
        _txtDestination.Multiline = false;
        _txtSource.Height = 34;
        _txtDestination.Height = 34;
        _txtSource.MinimumSize = new Size(0, 34);
        _txtDestination.MinimumSize = new Size(0, 34);
        _txtSource.MaximumSize = new Size(int.MaxValue, 34);
        _txtDestination.MaximumSize = new Size(int.MaxValue, 34);
        _txtSource.Margin = new Padding(0, 6, 8, 6);
        _txtDestination.Margin = new Padding(0, 6, 8, 6);

        _btnBrowseSource.Size = new Size(120, 34);
        _btnBrowseDestination.Size = new Size(120, 34);
        _btnBrowseSource.AutoSize = false;
        _btnBrowseDestination.AutoSize = false;
        _btnBrowseSource.MinimumSize = new Size(120, 34);
        _btnBrowseDestination.MinimumSize = new Size(120, 34);
        _btnBrowseSource.MaximumSize = new Size(120, 34);
        _btnBrowseDestination.MaximumSize = new Size(120, 34);
        _btnBrowseSource.Dock = DockStyle.None;
        _btnBrowseDestination.Dock = DockStyle.None;
        _btnBrowseSource.Anchor = AnchorStyles.Left;
        _btnBrowseDestination.Anchor = AnchorStyles.Left;
        _btnBrowseSource.Margin = new Padding(0, 8, 0, 8);
        _btnBrowseDestination.Margin = new Padding(0, 8, 0, 8);

        layout.Controls.Add(lblSource, 0, 0);
        layout.Controls.Add(_txtSource, 1, 0);
        layout.Controls.Add(_btnBrowseSource, 2, 0);

        layout.Controls.Add(lblDestination, 0, 1);
        layout.Controls.Add(_txtDestination, 1, 1);
        layout.Controls.Add(_btnBrowseDestination, 2, 1);

        return layout;
    }

    private Control CreateOptionsLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 10, 0, 0)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        var checksPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(6, 2, 0, 0),
            AutoScroll = true
        };
        checksPanel.Controls.Add(_chkDarkTheme);
        checksPanel.Controls.Add(_chkRemoveSource);
        checksPanel.Controls.Add(_chkCreateEdits);
        checksPanel.Controls.Add(_chkCreateFinal);
        checksPanel.Controls.Add(_chkSaveLog);

        var destinationPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(6, 2, 0, 0)
        };
        destinationPanel.Controls.Add(CreateLabel("Destination media folder name"));
        destinationPanel.Controls.Add(_txtDestinationFolder);

        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(6, 2, 0, 0)
        };

        _btnCancel.Enabled = false;
        _btnStart.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnStart.AutoSize = false;
        _btnCancel.AutoSize = false;
        _btnStart.Size = new Size(BUTTON_WIDTH, BUTTON_HEIGHT);
        _btnCancel.Size = new Size(BUTTON_WIDTH, BUTTON_HEIGHT);
        _btnStart.MinimumSize = new Size(BUTTON_WIDTH, BUTTON_HEIGHT);
        _btnCancel.MinimumSize = new Size(BUTTON_WIDTH, BUTTON_HEIGHT);
        _btnStart.MaximumSize = new Size(BUTTON_WIDTH, BUTTON_HEIGHT);
        _btnCancel.MaximumSize = new Size(BUTTON_WIDTH, BUTTON_HEIGHT);
        _btnStart.Margin = new Padding(0, 0, 0, 8);
        _btnCancel.Margin = new Padding(0, 0, 0, 8);
        actionsPanel.Controls.Add(_btnStart);
        actionsPanel.Controls.Add(_btnCancel);
        layout.Controls.Add(checksPanel, 0, 0);
        layout.Controls.Add(destinationPanel, 1, 0);
        layout.Controls.Add(actionsPanel, 2, 0);

        return layout;
    }

    private Control CreateProgressLayout()
    {
        _progressBar.Dock = DockStyle.Top;
        _progressBar.BorderRadius = 0;
        _progressBar.Height = 20;
        _progressBar.Value = 0;
        _progressBar.ProgressColor2 = _progressBar.ProgressColor;

        _lblStatus.Text = "Ready.";
        _lblStatus.BackColor = Color.Transparent;
        _lblStatus.Dock = DockStyle.Fill;
        _lblStatus.AutoSize = false;
        _lblStatus.Margin = new Padding(0, 2, 0, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 12, 0, 0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(_progressBar, 0, 0);
        layout.Controls.Add(_lblStatus, 0, 1);
        return layout;
    }

    private Control CreateLogLayout()
    {
        ConfigureLogGrid();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 12, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _btnClearLog.Dock = DockStyle.Fill;
        _logGrid.Dock = DockStyle.Fill;

        layout.Controls.Add(_btnClearLog, 1, 0);
        layout.Controls.Add(_logGrid, 0, 1);
        layout.SetColumnSpan(_logGrid, 2);

        return layout;
    }

    private void ConfigureLogGrid()
    {
        _logGrid.BackgroundColor = Color.White;
        _logGrid.BorderStyle = BorderStyle.FixedSingle;
        _logGrid.RowHeadersVisible = false;
        _logGrid.AllowUserToAddRows = false;
        _logGrid.AllowUserToDeleteRows = false;
        _logGrid.ReadOnly = true;
        _logGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _logGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        _logGrid.Columns.Clear();
        _logGrid.Columns.Add("Time", "Time");
        _logGrid.Columns.Add("Level", "Level");
        _logGrid.Columns.Add("Message", "Message");
        _logGrid.Columns[0].FillWeight = 18;
        _logGrid.Columns[1].FillWeight = 18;
        _logGrid.Columns[2].FillWeight = 64;
    }

    private void WireEvents()
    {
        _btnBrowseSource.Click += (_, _) => BrowseFolder(_txtSource);
        _btnBrowseDestination.Click += (_, _) => BrowseFolder(_txtDestination);
        _btnStart.Click += btnStart_Click;
        _btnCancel.Click += btnCancel_Click;
        _btnClose.Click += btnClose_Click;
        _btnClearLog.Click += (_, _) =>
        {
            _logGrid.Rows.Clear();
            UpdateLogUiState();
        };
        _chkDarkTheme.CheckedChanged += (_, _) => ApplyTheme(_chkDarkTheme.Checked);
        FormClosing += GunaPrototypeForm_FormClosing;
        FormClosed += (_, _) => SaveState();
    }

    private void btnClose_Click(object? sender, EventArgs e)
    {
        if (_cancellationTokenSource is not null && !_cancellationTokenSource.IsCancellationRequested)
        {
            var confirm = MessageBox.Show(
                "A transfer is currently running. Cancel it now?",
                "Confirm",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
            {
                _cancellationTokenSource.Cancel();
                _lblStatus.Text = "Canceling...";
                AppendLog("Warning", "Cancel requested before closing.");
            }

            return;
        }

        Close();
    }

    private void GunaPrototypeForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_cancellationTokenSource is null || _cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        e.Cancel = true;
        var confirm = MessageBox.Show(
            "A transfer is currently running. Cancel it now?",
            "Confirm",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            _cancellationTokenSource.Cancel();
            _lblStatus.Text = "Canceling...";
            AppendLog("Warning", "Cancel requested before closing.");
        }
    }

    private bool TryLoadState()
    {
        try
        {
            if (!File.Exists(StateFilePath))
            {
                return false;
            }

            var json = File.ReadAllText(StateFilePath);
            var state = JsonSerializer.Deserialize<AppState>(json);
            if (state is null)
            {
                return false;
            }

            _txtSource.Text = state.SourcePath ?? string.Empty;
            _txtDestination.Text = state.DestinationPath ?? string.Empty;
            _txtDestinationFolder.Text = state.DestinationFolderName ?? string.Empty;
            _chkDarkTheme.Checked = state.DarkTheme;
            _chkRemoveSource.Checked = state.RemoveSource;
            _chkCreateEdits.Checked = state.CreateEdits;
            _chkCreateFinal.Checked = state.CreateFinal;
            _chkSaveLog.Checked = state.SaveLog;

            if (state.WindowWidth > 0 && state.WindowHeight > 0)
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = new Rectangle(state.WindowX, state.WindowY, state.WindowWidth, state.WindowHeight);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SaveState()
    {
        try
        {
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var state = new AppState
            {
                SourcePath = _txtSource.Text,
                DestinationPath = _txtDestination.Text,
                DestinationFolderName = _txtDestinationFolder.Text,
                DarkTheme = _chkDarkTheme.Checked,
                RemoveSource = _chkRemoveSource.Checked,
                CreateEdits = _chkCreateEdits.Checked,
                CreateFinal = _chkCreateFinal.Checked,
                SaveLog = _chkSaveLog.Checked,
                WindowX = bounds.X,
                WindowY = bounds.Y,
                WindowWidth = bounds.Width,
                WindowHeight = bounds.Height
            };

            var dir = Path.GetDirectoryName(StateFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StateFilePath, json);
        }
        catch
        {
            // Ignore state persistence errors.
        }
    }

    private void BrowseFolder(Guna2TextBox target)
    {
        using var dialog = new FolderBrowserDialog { ShowNewFolderButton = true };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            target.Text = dialog.SelectedPath;
        }
    }

    private async void btnStart_Click(object? sender, EventArgs e)
    {
        var sourcePath = _txtSource.Text.Trim();
        var destinationPath = _txtDestination.Text.Trim();
        var customFolderName = string.IsNullOrWhiteSpace(_txtDestinationFolder.Text) ? "Original" : _txtDestinationFolder.Text.Trim();

        if (!Directory.Exists(sourcePath))
        {
            MessageBox.Show("Please select a valid source folder.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            MessageBox.Show("Please select a destination folder.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Directory.CreateDirectory(destinationPath);

        var options = new TransferOptions(
            sourcePath,
            destinationPath,
            customFolderName,
            _chkCreateEdits.Checked,
            _chkCreateFinal.Checked,
            _chkRemoveSource.Checked);

        ResetTransferUi();
        _cancellationTokenSource = new CancellationTokenSource();
        _isTransferInProgress = true;
        SetUiEnabled(false);
        _btnCancel.Enabled = true;
        _lblStatus.Text = "Scanning files...";
        _progressBar.Refresh();
        _lblStatus.Refresh();
        await Task.Yield();

        AppendLog("Info", $"Started transfer at {DateTime.Now:G}");
        AppendLog("Info", $"Source: {sourcePath}");
        AppendLog("Info", $"Destination: {destinationPath}");

        var progress = new Progress<TransferProgress>(UpdateProgressUi);

        var logProgress = new Progress<TransferLogEntry>(x => AppendLog(x.Level, x.Message));

        bool showCompletionMessage = false;
        string completionTitle = string.Empty;
        string completionMessage = string.Empty;

        try
        {
            var result = await Task.Run(() => ProcessFiles(options, progress, logProgress, _cancellationTokenSource.Token));

            var statusText = result.WasCanceled
                ? $"Canceled. Copied: {result.CopiedCount}, Failed: {result.FailedCount}, Remaining: {result.RemainingCount}"
                : $"Completed. Copied: {result.CopiedCount}, Failed: {result.FailedCount}";

            _lblStatus.Text = statusText;

            completionMessage = result.WasCanceled
                ? $"Transfer canceled.\nCopied: {result.CopiedCount}\nFailed: {result.FailedCount}\nRemaining: {result.RemainingCount}"
                : $"Transfer complete.\nCopied: {result.CopiedCount}\nFailed: {result.FailedCount}";
            completionTitle = result.WasCanceled ? "Canceled" : "Finished";
            showCompletionMessage = true;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "Transfer failed.";
            AppendLog("Error", $"Fatal error: {ex.Message}");
            MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (_chkSaveLog.Checked)
            {
                SaveLogFile(destinationPath);
            }

            if (showCompletionMessage)
            {
                MessageBox.Show(
                    completionMessage,
                    completionTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _isTransferInProgress = false;
            _btnCancel.Enabled = false;
            SetUiEnabled(true);
        }
    }

    private void btnCancel_Click(object? sender, EventArgs e)
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        _btnCancel.Enabled = false;
        _lblStatus.Text = "Canceling...";
        AppendLog("Warning", "Cancel requested.");
        _cancellationTokenSource.Cancel();
    }

    private static TransferResult ProcessFiles(TransferOptions options, IProgress<TransferProgress> progress, IProgress<TransferLogEntry> log, CancellationToken cancellationToken)
    {
        var allFiles = Directory
            .EnumerateFiles(options.SourcePath, "*", SearchOption.AllDirectories)
            .Where(IsMediaFile)
            .ToList();

        int copied = 0;
        int failed = 0;
        int processed = 0;

        progress.Report(new TransferProgress(processed, allFiles.Count, $"Found {allFiles.Count} media files."));
        log.Report(new TransferLogEntry("Info", $"Found {allFiles.Count} media files."));

        foreach (var filePath in allFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                var remaining = Math.Max(0, allFiles.Count - processed);
                log.Report(new TransferLogEntry("Warning", "Transfer canceled by user."));
                return new TransferResult(copied, failed, true, remaining);
            }

            try
            {
                var fileDate = GetPreferredFileDate(filePath);

                var yearFolder = Path.Combine(options.DestinationPath, fileDate.ToString("yyyy", CultureInfo.InvariantCulture));
                var dayFolder = Path.Combine(yearFolder, fileDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                var mediaFolder = Path.Combine(dayFolder, options.CustomFolderName);

                Directory.CreateDirectory(dayFolder);
                Directory.CreateDirectory(mediaFolder);

                if (options.CreateEdits)
                {
                    Directory.CreateDirectory(Path.Combine(dayFolder, "Edits"));
                }

                if (options.CreateFinal)
                {
                    Directory.CreateDirectory(Path.Combine(dayFolder, "Final"));
                }

                var destinationFilePath = GetUniqueDestinationPath(mediaFolder, Path.GetFileName(filePath));
                File.Copy(filePath, destinationFilePath, overwrite: false);

                if (options.RemoveSourceAfterCopy)
                {
                    File.Delete(filePath);
                }

                copied++;
                log.Report(new TransferLogEntry("Info", $"Copied: {filePath} -> {destinationFilePath}"));
            }
            catch (Exception ex)
            {
                failed++;
                log.Report(new TransferLogEntry("Error", $"Failed: {filePath} ({ex.Message})"));
            }

            processed++;
            progress.Report(new TransferProgress(processed, allFiles.Count, $"Processing {processed}/{allFiles.Count}: {Path.GetFileName(filePath)}"));
        }

        log.Report(new TransferLogEntry("Info", $"Finished. Copied: {copied}, Failed: {failed}"));

        return new TransferResult(copied, failed, false, 0);
    }

    private static bool IsMediaFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ImageExtensions.Contains(ext) || VideoExtensions.Contains(ext);
    }

    private static DateTime GetPreferredFileDate(string filePath)
    {
        if (TryGetDateFromFilename(filePath, out var filenameDate))
        {
            return filenameDate;
        }

        if (TryGetDateTakenFromMetadata(filePath, out var metadataDate))
        {
            return metadataDate;
        }

        return File.GetLastWriteTime(filePath);
    }

    private static bool TryGetDateFromFilename(string filePath, out DateTime date)
    {
        date = default;
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var match = FilenameDateRegex.Match(fileName);
        if (!match.Success)
        {
            return false;
        }

        return DateTime.TryParseExact(match.Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryGetDateTakenFromMetadata(string filePath, out DateTime date)
    {
        date = default;
        if (!ImageExtensions.Contains(Path.GetExtension(filePath)))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var image = Image.FromStream(stream, false, false);

            var exifIds = new[] { 0x9003, 0x9004, 0x0132 };
            foreach (var exifId in exifIds)
            {
                if (!image.PropertyIdList.Contains(exifId))
                {
                    continue;
                }

                var propertyItem = image.GetPropertyItem(exifId);
                var rawValue = propertyItem?.Value;
                if (rawValue is null || rawValue.Length == 0)
                {
                    continue;
                }

                var value = Encoding.ASCII.GetString(rawValue).Trim('\0', ' ');
                if (DateTime.TryParseExact(value, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static string GetUniqueDestinationPath(string directory, string fileName)
    {
        var destinationPath = Path.Combine(directory, fileName);
        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var index = 1;

        while (true)
        {
            var candidate = Path.Combine(directory, $"{baseName}_{index}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private void AppendLog(string level, string message)
    {
        var rowIndex = _logGrid.Rows.Add(
            DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            level,
            message);

        var dark = _chkDarkTheme.Checked;
        _logGrid.Rows[rowIndex].DefaultCellStyle.ForeColor = GetLogRowForeColor(level, dark);

        if (_logGrid.Rows.Count > 0 && _logGrid.IsHandleCreated && _logGrid.DisplayedRowCount(false) > 0)
        {
            try
            {
                _logGrid.FirstDisplayedScrollingRowIndex = _logGrid.Rows.Count - 1;
            }
            catch (InvalidOperationException)
            {
                // Grid can be temporarily too small to display rows during layout/resizing.
            }
        }

        _logGrid.Invalidate();
        _logGrid.Update();
        UpdateLogUiState();
    }

    private void SaveLogFile(string destinationPath)
    {
        try
        {
            Directory.CreateDirectory(destinationPath);
            var logFilePath = Path.Combine(destinationPath, $"MediaTransferLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            var sb = new StringBuilder();
            foreach (DataGridViewRow row in _logGrid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var time = row.Cells[0].Value?.ToString() ?? string.Empty;
                var level = row.Cells[1].Value?.ToString() ?? string.Empty;
                var msg = row.Cells[2].Value?.ToString() ?? string.Empty;
                sb.AppendLine($"[{time}] [{level}] {msg}");
            }

            File.WriteAllText(logFilePath, sb.ToString());
            AppendLog("Info", $"Log saved: {logFilePath}");
        }
        catch (Exception ex)
        {
            AppendLog("Error", $"Failed to save log file: {ex.Message}");
        }
    }

    private void SetUiEnabled(bool enabled)
    {
        _txtSource.Enabled = enabled;
        _txtDestination.Enabled = enabled;
        _txtDestinationFolder.Enabled = enabled;
        _btnBrowseSource.Enabled = enabled;
        _btnBrowseDestination.Enabled = enabled;
        _chkRemoveSource.Enabled = enabled;
        _chkCreateEdits.Enabled = enabled;
        _chkCreateFinal.Enabled = enabled;
        _chkSaveLog.Enabled = enabled;
        _chkDarkTheme.Enabled = enabled;
        _btnStart.Enabled = enabled;
        _btnClearLog.Enabled = enabled && _logGrid.Rows.Count > 0;
        _btnClose.Enabled = enabled;
    }

    private void UpdateProgressUi(TransferProgress progress)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateProgressUi(progress));
            return;
        }

        _progressBar.Maximum = progress.TotalCount <= 0 ? 1 : progress.TotalCount;
        _progressBar.Value = Math.Min(_progressBar.Maximum, Math.Max(0, progress.ProcessedCount));
        _lblStatus.Text = progress.Status;
        _progressBar.Invalidate();
        _progressBar.Update();
        _lblStatus.Refresh();
    }

    private void UpdateLogUiState()
    {
        var hasLogRows = _logGrid.Rows.Count > 0;
        _btnClearLog.Visible = hasLogRows;
        _btnClearLog.Enabled = hasLogRows && !_isTransferInProgress;
    }

    private void ResetTransferUi()
    {
        _progressBar.Maximum = 1;
        _progressBar.Value = 0;
        _lblStatus.Text = "Ready.";
        _logGrid.Rows.Clear();
        _logGrid.ClearSelection();
        UpdateLogUiState();
        _logGrid.Refresh();
        _progressBar.Refresh();
        _lblStatus.Refresh();
    }

    private void ApplyTheme(bool dark)
    {
        // Primary application background
        var back = dark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(230, 230, 230);

        // Panel background color
        var surface = dark ? Color.FromArgb(32, 36, 42) : Color.FromArgb(220, 220, 220);

        var fore = dark ? Color.Gainsboro : Color.FromArgb(30, 41, 59);

        var muted = dark ? Color.FromArgb(158, 168, 182) : Color.FromArgb(71, 85, 105);

        var border = dark ? Color.FromArgb(67, 122, 204) : Color.FromArgb(150, 150, 150);

        var accent = Color.FromArgb(37, 99, 235);

        BackColor = back;

        foreach (var panel in Controls.OfType<TableLayoutPanel>())
        {
            panel.BackColor = back;
        }

        foreach (var card in GetAllControls(this).OfType<Guna2Panel>())
        {
            card.FillColor = surface;
            card.BorderColor = border;
        }

        foreach (var label in GetAllControls(this).OfType<Guna2HtmlLabel>())
        {
            label.ForeColor = label.Text.Contains(APP_TITLE, StringComparison.OrdinalIgnoreCase) ? fore : muted;
            label.BackColor = Color.Transparent;
        }

        _lblStatus.ForeColor = dark ? Color.FromArgb(232, 236, 243) : Color.FromArgb(51, 65, 85);

        foreach (var tb in GetAllControls(this).OfType<Guna2TextBox>())
        {
            tb.FillColor = dark ? Color.FromArgb(26, 29, 34) : Color.White;
            tb.ForeColor = fore;
            tb.BorderColor = border;
            tb.FocusedState.BorderColor = accent;
            tb.DisabledState.FillColor = dark ? Color.FromArgb(26, 29, 34) : Color.FromArgb(245, 245, 245);
            tb.DisabledState.ForeColor = dark ? Color.FromArgb(210, 216, 224) : Color.FromArgb(90, 90, 90);
            tb.DisabledState.BorderColor = dark ? Color.FromArgb(67, 122, 204) : Color.FromArgb(170, 170, 170);
            tb.DisabledState.PlaceholderForeColor = dark ? Color.FromArgb(140, 150, 165) : Color.FromArgb(140, 140, 140);
        }

        foreach (var cb in GetAllControls(this).OfType<Guna2CheckBox>())
        {
            cb.ForeColor = muted;
            cb.CheckedState.FillColor = accent;
            cb.UncheckedState.BorderColor = dark ? Color.FromArgb(110, 120, 133) : Color.FromArgb(148, 163, 184);
        }

        foreach (var button in GetAllControls(this).OfType<Guna2Button>())
        {
            if (ReferenceEquals(button, _btnStart))
            {
                button.FillColor = accent;
                button.ForeColor = Color.White;
                button.DisabledState.FillColor = dark ? Color.FromArgb(26, 58, 128) : Color.FromArgb(112, 155, 232);
                button.DisabledState.ForeColor = Color.FromArgb(230, 235, 245);
                button.DisabledState.BorderColor = button.DisabledState.FillColor;
            }
            else if (ReferenceEquals(button, _btnCancel))
            {
                button.FillColor = Color.FromArgb(239, 68, 68);
                button.ForeColor = Color.White;
                button.DisabledState.FillColor = dark ? Color.FromArgb(120, 58, 58) : Color.FromArgb(222, 154, 154);
                button.DisabledState.ForeColor = Color.FromArgb(235, 235, 235);
                button.DisabledState.BorderColor = button.DisabledState.FillColor;
            }
            else if (ReferenceEquals(button, _btnClose))
            {
                button.FillColor = dark ? Color.FromArgb(70, 78, 90) : Color.FromArgb(203, 213, 225);
                button.ForeColor = dark ? Color.White : Color.FromArgb(30, 41, 59);
                button.BorderThickness = 1;
                button.BorderColor = dark ? Color.FromArgb(120, 130, 146) : Color.FromArgb(148, 163, 184);
                button.DisabledState.FillColor = dark ? Color.FromArgb(70, 78, 90) : Color.FromArgb(203, 213, 225);
                button.DisabledState.ForeColor = dark ? Color.FromArgb(210, 216, 224) : Color.FromArgb(80, 90, 105);
                button.DisabledState.BorderColor = dark ? Color.FromArgb(120, 130, 146) : Color.FromArgb(148, 163, 184);
            }
            else if (ReferenceEquals(button, _btnBrowseSource) || ReferenceEquals(button, _btnBrowseDestination))
            {
                button.FillColor = dark ? Color.FromArgb(70, 78, 90) : Color.FromArgb(203, 213, 225);
                button.ForeColor = dark ? Color.White : Color.FromArgb(30, 41, 59);
                button.BorderThickness = 1;
                button.BorderColor = dark ? Color.FromArgb(120, 130, 146) : Color.FromArgb(148, 163, 184);
                button.DisabledState.FillColor = dark ? Color.FromArgb(70, 78, 90) : Color.FromArgb(203, 213, 225);
                button.DisabledState.ForeColor = dark ? Color.FromArgb(210, 216, 224) : Color.FromArgb(80, 90, 105);
                button.DisabledState.BorderColor = dark ? Color.FromArgb(120, 130, 146) : Color.FromArgb(148, 163, 184);
            }
            else
            {
                button.FillColor = dark ? Color.FromArgb(49, 55, 63) : Color.FromArgb(241, 245, 249);
                button.ForeColor = dark ? Color.Gainsboro : Color.FromArgb(51, 65, 85);
                button.DisabledState.FillColor = dark ? Color.FromArgb(49, 55, 63) : Color.FromArgb(230, 234, 239);
                button.DisabledState.ForeColor = dark ? Color.FromArgb(195, 203, 214) : Color.FromArgb(95, 105, 118);
                button.DisabledState.BorderColor = dark ? Color.FromArgb(86, 95, 107) : Color.FromArgb(196, 203, 212);
            }
        }

        _progressBar.FillColor = dark ? Color.FromArgb(56, 61, 68) : Color.FromArgb(226, 232, 240);
        _progressBar.ProgressColor = accent;
        _progressBar.ProgressColor2 = accent;

        _logGrid.BackgroundColor = dark ? Color.FromArgb(56, 59, 64) : Color.White;
        _logGrid.GridColor = dark ? Color.FromArgb(37, 99, 235) : Color.FromArgb(226, 232, 240);
        _logGrid.ThemeStyle.GridColor = dark ? Color.FromArgb(37, 99, 235) : Color.FromArgb(226, 232, 240);

        _logGrid.DefaultCellStyle.BackColor = dark ? Color.FromArgb(26, 29, 34) : Color.White;
        _logGrid.DefaultCellStyle.ForeColor = dark ? Color.White: Color.Black;
        _logGrid.AlternatingRowsDefaultCellStyle.BackColor = dark ? Color.FromArgb(26, 29, 34) : Color.FromArgb(248, 250, 252);
        _logGrid.AlternatingRowsDefaultCellStyle.ForeColor = dark ? Color.White : Color.Black;

        _logGrid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(52, 70, 94) : Color.FromArgb(219, 234, 254);
        _logGrid.DefaultCellStyle.SelectionForeColor = fore;
        _logGrid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(35, 40, 46) : Color.FromArgb(248, 250, 252);
        _logGrid.ColumnHeadersDefaultCellStyle.ForeColor = dark ? Color.FromArgb(220, 226, 234) : muted;
        _logGrid.EnableHeadersVisualStyles = false;

        ApplyLogRowTheme(dark);
    }

    private void ApplyLogRowTheme(bool dark)
    {
        foreach (DataGridViewRow row in _logGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var level = row.Cells.Count > 1 ? row.Cells[1].Value?.ToString() : null;
            row.DefaultCellStyle.ForeColor = GetLogRowForeColor(level, dark);
        }
    }

    private static Color GetLogRowForeColor(string? level, bool dark)
    {
        return level switch
        {
            "Error" => dark ? Color.FromArgb(255, 153, 153) : Color.Firebrick,
            "Warning" => dark ? Color.FromArgb(255, 214, 102) : Color.DarkOrange,
            _ => dark ? Color.FromArgb(230, 230, 230) : Color.FromArgb(30, 41, 59)
        };
    }

    private static IEnumerable<Control> GetAllControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;

            foreach (var descendent in GetAllControls(child))
            {
                yield return descendent;
            }
        }
    }

    private static Guna2Panel CreateCard(string title, Control content)
    {
        var card = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            BorderRadius = 12,
            BorderThickness = 1,
            FillColor = Color.White,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 12)
        };

        var cardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        cardLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var label = new Guna2HtmlLabel
        {
            Text = title,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
            AutoSize = true
        };

        content.Dock = DockStyle.Fill;

        cardLayout.Controls.Add(label, 0, 0);
        cardLayout.Controls.Add(content, 0, 1);

        card.Controls.Add(cardLayout);
        return card;
    }

    private static Guna2HtmlLabel CreateLabel(string text)
    {
        return new Guna2HtmlLabel
        {
            Text = text,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9F),
            Dock = DockStyle.Fill,
            AutoSize = true
        };
    }

    private static Guna2TextBox CreateTextBox(string placeholder, int width = 0)
    {
        return new Guna2TextBox
        {
            BorderRadius = 8,
            PlaceholderText = placeholder,
            Font = new Font("Segoe UI", 9F),
            Margin = new Padding(0, 2, 8, 8),
            Dock = width > 0 ? DockStyle.None : DockStyle.Fill,
            Width = width
        };
    }

    private static Guna2Button CreatePrimaryButton(string text)
    {
        return new Guna2Button
        {
            Text = text,
            BorderRadius = 8,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Size = new Size(170, 36),
            Margin = new Padding(0, 0, 0, 8)
        };
    }

    private static Guna2Button CreateSecondaryButton(string text)
    {
        return new Guna2Button
        {
            Text = text,
            BorderRadius = 8,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9F),
            Size = new Size(110, 28)
        };
    }

    private static Guna2Button CreateDangerButton(string text)
    {
        return new Guna2Button
        {
            Text = text,
            BorderRadius = 8,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Size = new Size(170, 36)
        };
    }

    private static Guna2CheckBox CreateCheckBox(string text)
    {
        return new Guna2CheckBox
        {
            Text = text,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 10),
            Font = new Font("Segoe UI", 9F)
        };
    }

    private sealed record TransferOptions(
        string SourcePath,
        string DestinationPath,
        string CustomFolderName,
        bool CreateEdits,
        bool CreateFinal,
        bool RemoveSourceAfterCopy);

    private sealed record TransferProgress(int ProcessedCount, int TotalCount, string Status);

    private sealed record TransferLogEntry(string Level, string Message);

    private sealed record TransferResult(int CopiedCount, int FailedCount, bool WasCanceled, int RemainingCount);
}
