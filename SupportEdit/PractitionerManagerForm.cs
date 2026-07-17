using System;
using System.Linq;
using System.Windows.Forms;
using SupportEdit.Services;
using SupportEdit.Models;

namespace SupportEdit
{
    public partial class PractitionerManagerForm : Form
    {
        private DataGridView dgv;
        private Button btnAdd;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnServices;
        private CheckBox chkAutoFix;
        private Button btnSave;
        private Button btnClose;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn4;
        private ConfigService _config;

        public PractitionerManagerForm()
        {
            InitializeComponent();
        }

        public PractitionerManagerForm(ConfigService config) : this()
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            LoadData();
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PractitionerManagerForm));
            dgv = new DataGridView();
            btnAdd = new Button();
            btnEdit = new Button();
            btnDelete = new Button();
            btnServices = new Button();
            chkAutoFix = new CheckBox();
            btnSave = new Button();
            btnClose = new Button();
            ((System.ComponentModel.ISupportInitialize)dgv).BeginInit();
            SuspendLayout();
            // 
            // dgv
            // 
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.ColumnHeadersHeight = 29;
            dgv.Location = new Point(12, 12);
            dgv.MultiSelect = false;
            dgv.Name = "dgv";
            dgv.ReadOnly = true;
            dgv.RowHeadersWidth = 51;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.Size = new Size(760, 362);
            dgv.TabIndex = 0;
            // 
            // btnAdd
            // 
            btnAdd.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnAdd.Location = new Point(12, 420);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(86, 28);
            btnAdd.TabIndex = 1;
            btnAdd.Text = "Thêm";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += BtnAdd_Click;
            // 
            // btnEdit
            // 
            btnEdit.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnEdit.Location = new Point(104, 420);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(86, 28);
            btnEdit.TabIndex = 2;
            btnEdit.Text = "Sửa";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click += BtnEdit_Click;
            // 
            // btnDelete
            // 
            btnDelete.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDelete.Location = new Point(196, 420);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(86, 28);
            btnDelete.TabIndex = 3;
            btnDelete.Text = "Xóa";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += BtnDelete_Click;
            // 
            // btnServices
            // 
            btnServices.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnServices.Location = new Point(288, 420);
            btnServices.Name = "btnServices";
            btnServices.Size = new Size(118, 28);
            btnServices.TabIndex = 4;
            btnServices.Text = "Thêm dịch vụ";
            btnServices.UseVisualStyleBackColor = true;
            btnServices.Click += BtnServices_Click;
            // 
            // chkAutoFix
            // 
            chkAutoFix.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            chkAutoFix.AutoSize = true;
            chkAutoFix.Location = new Point(12, 381);
            chkAutoFix.Name = "chkAutoFix";
            chkAutoFix.Size = new Size(380, 24);
            chkAutoFix.TabIndex = 6;
            chkAutoFix.Text = "Tự động sửa NGUOI_THUC_HIEN    <---    <---    <---";
            chkAutoFix.UseVisualStyleBackColor = true;
            chkAutoFix.CheckedChanged += ChkAutoFix_CheckedChanged;
            // 
            // btnSave
            // 
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Location = new Point(594, 420);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(86, 28);
            btnSave.TabIndex = 7;
            btnSave.Text = "Lưu";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += BtnSave_Click;
            // 
            // btnClose
            // 
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(686, 420);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(86, 28);
            btnClose.TabIndex = 8;
            btnClose.Text = "Đóng";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += BtnClose_Click;
            // 
            // PractitionerManagerForm
            // 
            ClientSize = new Size(784, 461);
            Controls.Add(dgv);
            Controls.Add(btnAdd);
            Controls.Add(btnEdit);
            Controls.Add(btnDelete);
            Controls.Add(btnServices);
            Controls.Add(chkAutoFix);
            Controls.Add(btnSave);
            Controls.Add(btnClose);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "PractitionerManagerForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Quản lý người thực hiện";
            TopMost = true;
            ((System.ComponentModel.ISupportInitialize)dgv).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void LoadData()
        {
            if (_config == null)
            {
                return;
            }

            dgv.DataSource = null;
            dgv.DataSource = _config.Practitioners.Select(p => new PractitionerRow
            {
                CategoryName = p.CategoryName,
                Code = p.UsesMaBacSi() ? "Lấy MA_BAC_SI" : p.Code,
                Description = p.Description,
                ServiceCount = p.ServiceNames?.Count ?? 0
            }).ToList();
            chkAutoFix.CheckedChanged -= ChkAutoFix_CheckedChanged;
            chkAutoFix.Checked = _config.AutoFixNguoiThucHien;
            chkAutoFix.CheckedChanged += ChkAutoFix_CheckedChanged;
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            if (_config == null)
            {
                return;
            }

            var form = new PractitionerEditForm { TopMost = true };
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                var list = _config.Practitioners; list.Add(form.Item); _config.Save(); LoadData();
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (_config == null || dgv.CurrentRow == null) return;
            var idx = dgv.CurrentRow.Index;
            var item = _config.Practitioners[idx];
            var form = new PractitionerEditForm(item) { TopMost = true };
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                _config.Practitioners[idx] = form.Item; LoadData();
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_config == null || dgv.CurrentRow == null) return;
            var idx = dgv.CurrentRow.Index;
            if (MessageBox.Show("Xác nhận xóa?", "Xóa", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            _config.Practitioners.RemoveAt(idx); LoadData();
        }

        private void BtnServices_Click(object sender, EventArgs e)
        {
            if (_config == null || dgv.CurrentRow == null) return;
            var idx = dgv.CurrentRow.Index;
            var item = _config.Practitioners[idx];
            using var form = new ServiceNamesForm(item);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                _config.Save();
                LoadData();
            }
        }

        private void ChkAutoFix_CheckedChanged(object sender, EventArgs e)
        {
            if (_config == null) return;
            _config.AutoFixNguoiThucHien = chkAutoFix.Checked;
            _config.Save();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_config == null)
            {
                return;
            }

            foreach (var p in _config.Practitioners)
            {
                if (string.IsNullOrWhiteSpace(p.CategoryName) ||
                    (!p.UsesMaBacSi() && string.IsNullOrWhiteSpace(p.Code)))
                {
                    MessageBox.Show("CategoryName và Code không được rỗng.");
                    return;
                }

                if (!p.UsesMaBacSi() && !p.Code.Contains("/HCM-CCHN"))
                {
                    if (MessageBox.Show($"Code '{p.Code}' không đúng dạng khuyến nghị. Tiếp tục?", "Xác nhận", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                }
            }

            _config.Save();
            MessageBox.Show("Saved.");
        }

        private class PractitionerRow
        {
            public string CategoryName { get; set; }
            public string Code { get; set; }
            public string Description { get; set; }
            public int ServiceCount { get; set; }
        }
    }
}
