using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Xml.Linq;
using SupportEdit.Models;
using MessageBox = SupportEdit.UI.SupportMessageBox;

namespace SupportEdit
{
    public class ServiceNamesForm : Form
    {
        private readonly PractitionerItem _item;
        private readonly List<string> _services;
        private DataGridView dgv;
        private TextBox txtSearch;
        private Button btnClearSearch;
        private TextBox txtService;
        private Button btnAdd;
        private Button btnDelete;
        private Button btnImport;
        private Label lblTotal;
        private Button btnOk;
        private Button btnCancel;

        public ServiceNamesForm(PractitionerItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _services = (_item.ServiceNames ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .OrderBy(s => s)
                .ToList();
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            Text = "Thêm dịch vụ - " + _item.CategoryName;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 500);
            TopMost = true;

            var lblSearch = new Label
            {
                AutoSize = true,
                Location = new Point(12, 16),
                Text = "Tìm kiếm:"
            };

            txtSearch = new TextBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(82, 12),
                PlaceholderText = "Nhập tên dịch vụ cần tìm...",
                Size = new Size(566, 23)
            };

            btnClearSearch = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(654, 10),
                Size = new Size(94, 28),
                Text = "Xóa tìm kiếm"
            };

            dgv = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Location = new Point(12, 46),
                MultiSelect = true,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Size = new Size(736, 326)
            };
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tên dịch vụ", DataPropertyName = "Name", Name = "Name" });

            txtService = new TextBox { Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, Location = new Point(12, 386), Size = new Size(456, 23) };
            btnAdd = new Button { Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Text = "Thêm", Location = new Point(476, 384), Size = new Size(80, 28) };
            btnDelete = new Button { Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Text = "Xóa", Location = new Point(562, 384), Size = new Size(80, 28) };
            btnImport = new Button { Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Text = "Nhập file", Location = new Point(648, 384), Size = new Size(100, 28) };
            lblTotal = new Label { Anchor = AnchorStyles.Bottom | AnchorStyles.Left, AutoSize = true, Location = new Point(12, 428) };
            btnOk = new Button { Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Text = "Lưu", Location = new Point(582, 454), Size = new Size(80, 28) };
            btnCancel = new Button { Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Text = "Hủy", Location = new Point(668, 454), Size = new Size(80, 28) };

            btnAdd.Click += BtnAdd_Click;
            btnDelete.Click += BtnDelete_Click;
            btnImport.Click += BtnImport_Click;
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            txtSearch.TextChanged += (s, e) => LoadData();
            txtSearch.KeyDown += TxtSearch_KeyDown;
            btnClearSearch.Click += (s, e) =>
            {
                txtSearch.Clear();
                txtSearch.Focus();
            };

            Controls.Add(lblSearch);
            Controls.Add(txtSearch);
            Controls.Add(btnClearSearch);
            Controls.Add(dgv);
            Controls.Add(txtService);
            Controls.Add(btnAdd);
            Controls.Add(btnDelete);
            Controls.Add(btnImport);
            Controls.Add(lblTotal);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }

        private void LoadData()
        {
            var searchKey = NormalizeSearchKey(txtSearch?.Text);
            var visibleServices = string.IsNullOrWhiteSpace(searchKey)
                ? _services
                : _services.Where(service => NormalizeSearchKey(service).Contains(searchKey, StringComparison.Ordinal)).ToList();

            dgv.DataSource = null;
            dgv.DataSource = visibleServices.Select(s => new ServiceRow { Name = s }).ToList();
            lblTotal.Text = string.IsNullOrWhiteSpace(searchKey)
                ? $"Tổng cộng: {_services.Count} dòng"
                : $"Đang hiển thị: {visibleServices.Count}/{_services.Count} dòng";
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape) return;

            txtSearch.Clear();
            e.SuppressKeyPress = true;
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            AddServices(new[] { txtService.Text });
            txtService.Clear();
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            var selectedNames = dgv.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(row => row.DataBoundItem as ServiceRow)
                .Where(row => row != null)
                .Select(row => row.Name)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            if (selectedNames.Count == 0) return;

            _services.RemoveAll(selectedNames.Contains);
            LoadData();
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Supported files (*.json;*.csv;*.txt;*.xlsx)|*.json;*.csv;*.txt;*.xlsx|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var imported = ImportServices(dlg.FileName);
                AddServices(imported);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không nhập được file: " + ex.Message, "Nhập file", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            _item.ServiceNames = _services
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .OrderBy(s => s)
                .ToList();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void AddServices(IEnumerable<string> values)
        {
            var added = false;
            foreach (var value in values ?? Enumerable.Empty<string>())
            {
                var text = NormalizeServiceName(value);
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (_services.Any(s => string.Equals(s, text, StringComparison.InvariantCultureIgnoreCase))) continue;
                _services.Add(text);
                added = true;
            }
            if (!added) return;
            _services.Sort(StringComparer.InvariantCultureIgnoreCase);
            LoadData();
        }

        private static IEnumerable<string> ImportServices(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".json") return ImportJson(path);
            if (ext == ".xlsx") return ImportXlsx(path);
            return File.ReadLines(path).SelectMany(SplitDelimitedLine);
        }

        private static IEnumerable<string> ImportJson(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Enumerable.Empty<string>();

            var list = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    list.Add(item.GetString());
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    foreach (var key in new[] { "Tên dịch vụ", "TenDichVu", "TEN_DICH_VU", "ServiceName", "Name", "name" })
                    {
                        if (item.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                        {
                            list.Add(prop.GetString());
                            break;
                        }
                    }
                }
            }
            return list;
        }

        private static IEnumerable<string> SplitDelimitedLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) yield break;
            var first = line.Split(new[] { ',', ';', '\t' }, 2)[0];
            yield return first;
        }

        private static IEnumerable<string> ImportXlsx(string path)
        {
            using var zip = ZipFile.OpenRead(path);
            var shared = ReadSharedStrings(zip);
            var sheet = zip.GetEntry("xl/worksheets/sheet1.xml") ?? zip.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase));
            if (sheet == null) return Enumerable.Empty<string>();

            using var stream = sheet.Open();
            var doc = XDocument.Load(stream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var list = new List<string>();
            foreach (var row in doc.Descendants(ns + "row"))
            {
                var cell = row.Elements(ns + "c").FirstOrDefault(c => ((string)c.Attribute("r"))?.StartsWith("A", StringComparison.OrdinalIgnoreCase) == true)
                    ?? row.Elements(ns + "c").FirstOrDefault();
                var text = ReadCellText(cell, ns, shared);
                if (!string.IsNullOrWhiteSpace(text)) list.Add(text);
            }
            return list;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return new List<string>();
            using var stream = entry.Open();
            var doc = XDocument.Load(stream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            return doc.Descendants(ns + "si")
                .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value)))
                .ToList();
        }

        private static string ReadCellText(XElement cell, XNamespace ns, List<string> shared)
        {
            if (cell == null) return string.Empty;
            var value = cell.Element(ns + "v")?.Value ?? cell.Descendants(ns + "t").FirstOrDefault()?.Value ?? string.Empty;
            if ((string)cell.Attribute("t") == "s" && int.TryParse(value, out var idx) && idx >= 0 && idx < shared.Count)
                return shared[idx];
            return value;
        }

        private static string NormalizeServiceName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : System.Text.RegularExpressions.Regex.Replace(value.Trim(), "\\s+", " ");
        }

        private static string NormalizeSearchKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var decomposed = NormalizeServiceName(value).Normalize(NormalizationForm.FormD);
            var result = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                result.Append(char.ToLowerInvariant(character == 'đ' || character == 'Đ' ? 'd' : character));
            }

            return result.ToString().Normalize(NormalizationForm.FormC);
        }

        private class ServiceRow
        {
            public string Name { get; set; }
        }
    }
}
