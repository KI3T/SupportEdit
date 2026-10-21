using System.ComponentModel;
using System.IO;
using SupportEdit.Models;
using SupportEdit.Services;
using MessageBox = SupportEdit.UI.SupportMessageBox;

namespace SupportEdit
{
    public sealed class DischargeDateSearchForm : Form
    {
        private readonly TextBox _txtFolder;
        private readonly Button _btnChooseFolder;
        private readonly Button _btnRead;
        private readonly DateTimePicker _dtFromDate;
        private readonly DateTimePicker _dtToDate;
        private readonly Button _btnFilter;
        private readonly Button _btnShowAll;
        private readonly Label _lblStatus;
        private readonly ProgressBar _progress;
        private readonly DataGridView _gridResults;
        private readonly Button _btnCopy;
        private readonly Button _btnClose;
        private CancellationTokenSource? _cancellation;
        private bool _isReading;
        private List<HoSoXml1> _allRecords = new();
        private List<HoSoXml1> _displayedRecords = new();

        public DischargeDateSearchForm()
        {
            Text = "Tìm ngày ra viện từ thư mục XML";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1000, 650);
            MinimumSize = new Size(850, 560);
            TopMost = false;

            var folderGroup = new GroupBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 12),
                Size = new Size(976, 70),
                Text = "Thư mục chứa file XML"
            };
            var folderLabel = new Label
            {
                AutoSize = true,
                Location = new Point(15, 31),
                Text = "Thư mục:"
            };
            _txtFolder = new TextBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(82, 27),
                Size = new Size(664, 27)
            };
            _btnChooseFolder = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(755, 25),
                Size = new Size(96, 31),
                Text = "Chọn thư mục"
            };
            _btnRead = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(857, 25),
                Size = new Size(103, 31),
                Text = "Đọc dữ liệu"
            };
            folderGroup.Controls.AddRange(new Control[]
            {
                folderLabel,
                _txtFolder,
                _btnChooseFolder,
                _btnRead
            });

            var filterGroup = new GroupBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 90),
                Size = new Size(976, 75),
                Text = "Lọc theo ngày ra viện"
            };
            var fromLabel = new Label { AutoSize = true, Location = new Point(15, 34), Text = "Từ ngày:" };
            _dtFromDate = CreateDatePicker(78, 29);
            var toLabel = new Label { AutoSize = true, Location = new Point(225, 34), Text = "Đến ngày:" };
            _dtToDate = CreateDatePicker(298, 29);
            _btnFilter = new Button
            {
                Location = new Point(450, 27),
                Size = new Size(95, 31),
                Text = "Lọc"
            };
            _btnShowAll = new Button
            {
                Location = new Point(551, 27),
                Size = new Size(110, 31),
                Text = "Hiện tất cả"
            };
            filterGroup.Controls.AddRange(new Control[]
            {
                fromLabel,
                _dtFromDate,
                toLabel,
                _dtToDate,
                _btnFilter,
                _btnShowAll
            });

            _lblStatus = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
                Location = new Point(12, 176),
                Size = new Size(976, 24),
                Text = "Chưa đọc dữ liệu."
            };
            _progress = new ProgressBar
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 203),
                MarqueeAnimationSpeed = 30,
                Size = new Size(976, 18),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            _gridResults = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Location = new Point(12, 231),
                MultiSelect = true,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Size = new Size(976, 359)
            };
            ConfigureGridColumns();

            _btnCopy = new Button
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Enabled = false,
                Location = new Point(12, 605),
                Size = new Size(150, 31),
                Text = "Sao chép Mã BN"
            };
            _btnClose = new Button
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(888, 605),
                Size = new Size(100, 31),
                Text = "Đóng"
            };

            Controls.AddRange(new Control[]
            {
                folderGroup,
                filterGroup,
                _lblStatus,
                _progress,
                _gridResults,
                _btnCopy,
                _btnClose
            });

            _txtFolder.TextChanged += (_, _) => UpdateActionState();
            _btnChooseFolder.Click += BtnChooseFolder_Click;
            _btnRead.Click += BtnRead_Click;
            _btnFilter.Click += BtnFilter_Click;
            _btnShowAll.Click += BtnShowAll_Click;
            _btnCopy.Click += BtnCopy_Click;
            _btnClose.Click += BtnClose_Click;
            FormClosing += DischargeDateSearchForm_FormClosing;
            UpdateActionState();
        }

        private static DateTimePicker CreateDatePicker(int left, int top)
        {
            return new DateTimePicker
            {
                CustomFormat = "dd/MM/yyyy",
                Format = DateTimePickerFormat.Custom,
                Location = new Point(left, top),
                Size = new Size(130, 27),
                Value = DateTime.Today
            };
        }

        private void ConfigureGridColumns()
        {
            _gridResults.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(HoSoXml1.FileName),
                FillWeight = 155,
                HeaderText = "FileName",
                Name = nameof(HoSoXml1.FileName)
            });
            _gridResults.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(HoSoXml1.MA_LK),
                FillWeight = 115,
                HeaderText = "MA_LK",
                Name = nameof(HoSoXml1.MA_LK)
            });
            _gridResults.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(HoSoXml1.MA_BN),
                FillWeight = 70,
                HeaderText = "MA_BN",
                Name = nameof(HoSoXml1.MA_BN)
            });
            _gridResults.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(HoSoXml1.NGAY_RA),
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "dd/MM/yyyy HH:mm",
                    NullValue = string.Empty
                },
                FillWeight = 85,
                HeaderText = "NGAY_RA",
                Name = nameof(HoSoXml1.NGAY_RA)
            });
            _gridResults.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(HoSoXml1.Loi),
                FillWeight = 190,
                HeaderText = "Lỗi",
                Name = nameof(HoSoXml1.Loi)
            });
        }

        private void BtnChooseFolder_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Chọn thư mục chứa các file XML",
                ShowNewFolderButton = false,
                UseDescriptionForTitle = true
            };
            if (Directory.Exists(_txtFolder.Text.Trim()))
            {
                dialog.InitialDirectory = _txtFolder.Text.Trim();
            }

            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _txtFolder.Text = dialog.SelectedPath;
            _lblStatus.Text = "Đã chọn thư mục. Bấm Đọc dữ liệu để bắt đầu.";
        }

        private async void BtnRead_Click(object? sender, EventArgs e)
        {
            if (_isReading) return;
            var folderPath = _txtFolder.Text.Trim();
            if (!Directory.Exists(folderPath))
            {
                MessageBox.Show(this, "Thư mục XML không tồn tại.", "Tìm ngày ra viện", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetReadingState(true);
            _allRecords = new List<HoSoXml1>();
            BindRecords(_allRecords);
            _lblStatus.Text = "Đang đọc dữ liệu XML...";
            _cancellation = new CancellationTokenSource();
            var progress = new Progress<Xml1ReadProgress>(value =>
            {
                if (!IsDisposed && !Disposing)
                {
                    _lblStatus.Text = $"Đang đọc file {value.Processed}: {value.FileName}";
                }
            });

            try
            {
                var records = await Task.Run(() =>
                    Xml1Reader.ReadDirectory(folderPath, progress, _cancellation.Token));
                if (IsDisposed || Disposing) return;

                _allRecords = records;
                BindRecords(_allRecords);
                InitializeFilterDates();
                var errorCount = _allRecords.Count(item => !string.IsNullOrWhiteSpace(item.Loi));
                _lblStatus.Text = $"Đã đọc {_allRecords.Count} file; lỗi: {errorCount}.";
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed && !Disposing) _lblStatus.Text = "Đã dừng đọc dữ liệu.";
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;
                _lblStatus.Text = "Đọc dữ liệu thất bại.";
                MessageBox.Show(this, ex.Message, "Tìm ngày ra viện", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cancellation?.Dispose();
                _cancellation = null;
                if (!IsDisposed && !Disposing) SetReadingState(false);
            }
        }

        private void BtnFilter_Click(object? sender, EventArgs e)
        {
            var from = _dtFromDate.Value.Date;
            var to = _dtToDate.Value.Date;
            if (from > to)
            {
                MessageBox.Show(this, "Từ ngày phải nhỏ hơn hoặc bằng Đến ngày.", "Lọc ngày ra viện", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var toExclusive = to.AddDays(1);
            var filtered = _allRecords
                .Where(item => item.NGAY_RA.HasValue
                    && item.NGAY_RA.Value >= from
                    && item.NGAY_RA.Value < toExclusive)
                .OrderBy(item => item.NGAY_RA)
                .ToList();
            BindRecords(filtered);
            _lblStatus.Text = $"Đang hiển thị {filtered.Count}/{_allRecords.Count} hồ sơ từ {from:dd/MM/yyyy} đến {to:dd/MM/yyyy}.";
        }

        private void BtnShowAll_Click(object? sender, EventArgs e)
        {
            BindRecords(_allRecords);
            var errorCount = _allRecords.Count(item => !string.IsNullOrWhiteSpace(item.Loi));
            _lblStatus.Text = $"Đang hiển thị toàn bộ {_allRecords.Count} file; lỗi: {errorCount}.";
        }

        private void InitializeFilterDates()
        {
            var validDates = _allRecords
                .Where(item => item.NGAY_RA.HasValue)
                .Select(item => item.NGAY_RA!.Value.Date)
                .ToList();
            if (validDates.Count == 0) return;
            _dtFromDate.Value = validDates.Min();
            _dtToDate.Value = validDates.Max();
        }

        private void BindRecords(IEnumerable<HoSoXml1> records)
        {
            _displayedRecords = records.ToList();
            _gridResults.DataSource = new BindingList<HoSoXml1>(_displayedRecords);
            UpdateActionState();
        }

        private void BtnCopy_Click(object? sender, EventArgs e)
        {
            var patientCodes = _displayedRecords
                .Select(item => item.MA_BN)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (patientCodes.Count == 0) return;
            Clipboard.SetText(string.Join(Environment.NewLine, patientCodes));
            _lblStatus.Text = $"Đã sao chép {patientCodes.Count} Mã BN đang hiển thị.";
        }

        private void BtnClose_Click(object? sender, EventArgs e)
        {
            if (_isReading && _cancellation?.IsCancellationRequested != true)
            {
                _cancellation?.Cancel();
                _lblStatus.Text = "Đang yêu cầu dừng... Bạn có thể bấm Đóng ngay nếu cần.";
                _btnClose.Text = "Đóng ngay";
                return;
            }

            Close();
        }

        private void DischargeDateSearchForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _cancellation?.Cancel();
        }

        private void SetReadingState(bool reading)
        {
            _isReading = reading;
            _txtFolder.Enabled = !reading;
            _btnChooseFolder.Enabled = !reading;
            _progress.Visible = reading;
            _progress.MarqueeAnimationSpeed = reading ? 30 : 0;
            _btnClose.Text = reading ? "Dừng" : "Đóng";
            UpdateActionState();
        }

        private void UpdateActionState()
        {
            var hasData = _allRecords.Count > 0;
            _btnRead.Enabled = !_isReading && Directory.Exists(_txtFolder.Text.Trim());
            _dtFromDate.Enabled = !_isReading && hasData;
            _dtToDate.Enabled = !_isReading && hasData;
            _btnFilter.Enabled = !_isReading && hasData;
            _btnShowAll.Enabled = !_isReading && hasData;
            _btnCopy.Enabled = !_isReading && _displayedRecords.Any(item => !string.IsNullOrWhiteSpace(item.MA_BN));
        }
    }
}
