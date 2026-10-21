using System;
using System.Windows.Forms;
using SupportEdit.Models;
using MessageBox = SupportEdit.UI.SupportMessageBox;

namespace SupportEdit
{
    public partial class PractitionerEditForm : Form
    {
        private TextBox txtCategory;
        private TextBox txtCode;
        private RadioButton rdoMaBacSi;
        private RadioButton rdoReplacementCode;
        private TextBox txtDesc;
        private Button btnOk;
        private Label lbl1;
        private Label lbl2;
        private Label lbl3;
        private Button btnCancel;
        private PractitionerItem _sourceItem;

        public PractitionerItem Item { get; private set; }

        public PractitionerEditForm()
        {
            InitializeComponent();
        }

        public PractitionerEditForm(PractitionerItem item) : this()
        {
            LoadItem(item);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PractitionerEditForm));
            lbl1 = new Label();
            txtCategory = new TextBox();
            lbl2 = new Label();
            txtCode = new TextBox();
            rdoMaBacSi = new RadioButton();
            rdoReplacementCode = new RadioButton();
            lbl3 = new Label();
            txtDesc = new TextBox();
            btnOk = new Button();
            btnCancel = new Button();
            SuspendLayout();
            // 
            // lbl1
            // 
            lbl1.Location = new Point(18, 22);
            lbl1.Name = "lbl1";
            lbl1.Size = new Size(110, 23);
            lbl1.TabIndex = 0;
            lbl1.Text = "Nhóm:";
            lbl1.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtCategory
            // 
            txtCategory.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtCategory.Location = new Point(134, 22);
            txtCategory.Name = "txtCategory";
            txtCategory.Size = new Size(330, 23);
            txtCategory.TabIndex = 1;
            // 
            // lbl2
            // 
            lbl2.Location = new Point(18, 58);
            lbl2.Name = "lbl2";
            lbl2.Size = new Size(110, 23);
            lbl2.TabIndex = 2;
            lbl2.Text = "Mã:";
            lbl2.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtCode
            // 
            txtCode.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtCode.Location = new Point(244, 84);
            txtCode.Name = "txtCode";
            txtCode.Size = new Size(220, 23);
            txtCode.TabIndex = 5;
            // 
            // rdoMaBacSi
            // 
            rdoMaBacSi.Location = new Point(134, 56);
            rdoMaBacSi.Name = "rdoMaBacSi";
            rdoMaBacSi.Size = new Size(150, 24);
            rdoMaBacSi.TabIndex = 3;
            rdoMaBacSi.Text = "Lấy MA_BAC_SI";
            rdoMaBacSi.UseVisualStyleBackColor = true;
            rdoMaBacSi.CheckedChanged += CodeSource_CheckedChanged;
            // 
            // rdoReplacementCode
            // 
            rdoReplacementCode.Checked = true;
            rdoReplacementCode.Location = new Point(134, 84);
            rdoReplacementCode.Name = "rdoReplacementCode";
            rdoReplacementCode.Size = new Size(104, 24);
            rdoReplacementCode.TabIndex = 4;
            rdoReplacementCode.TabStop = true;
            rdoReplacementCode.Text = "Mã thay thế";
            rdoReplacementCode.UseVisualStyleBackColor = true;
            rdoReplacementCode.CheckedChanged += CodeSource_CheckedChanged;
            // 
            // lbl3
            // 
            lbl3.Location = new Point(18, 126);
            lbl3.Name = "lbl3";
            lbl3.Size = new Size(110, 23);
            lbl3.TabIndex = 4;
            lbl3.Text = "Mô tả:";
            lbl3.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtDesc
            // 
            txtDesc.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtDesc.Location = new Point(134, 126);
            txtDesc.Name = "txtDesc";
            txtDesc.Size = new Size(330, 23);
            txtDesc.TabIndex = 6;
            // 
            // btnOk
            // 
            btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOk.Location = new Point(298, 188);
            btnOk.Name = "btnOk";
            btnOk.Size = new Size(80, 28);
            btnOk.TabIndex = 7;
            btnOk.Text = "OK";
            btnOk.UseVisualStyleBackColor = true;
            btnOk.Click += BtnOk_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Location = new Point(384, 188);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(80, 28);
            btnCancel.TabIndex = 8;
            btnCancel.Text = "Hủy";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += BtnCancel_Click;
            // 
            // PractitionerEditForm
            // 
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            ClientSize = new Size(484, 233);
            Controls.Add(lbl1);
            Controls.Add(txtCategory);
            Controls.Add(lbl2);
            Controls.Add(rdoMaBacSi);
            Controls.Add(rdoReplacementCode);
            Controls.Add(txtCode);
            Controls.Add(lbl3);
            Controls.Add(txtDesc);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "PractitionerEditForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Thêm người thực hiện";
            TopMost = true;
            CodeSource_CheckedChanged(this, EventArgs.Empty);
            ResumeLayout(false);
            PerformLayout();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void LoadItem(PractitionerItem item)
        {
            if (item == null)
            {
                Text = "Thêm người thực hiện";
                return;
            }

            Text = "Sửa người thực hiện";
            _sourceItem = item;
            txtCategory.Text = item.CategoryName;
            txtCode.Text = item.Code;
            rdoMaBacSi.Checked = item.UsesMaBacSi();
            rdoReplacementCode.Checked = !item.UsesMaBacSi();
            txtDesc.Text = item.Description;
            Item = item;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCategory.Text) ||
                (rdoReplacementCode.Checked && string.IsNullOrWhiteSpace(txtCode.Text)))
            {
                MessageBox.Show("CategoryName và Code không được rỗng.");
                return;
            }

            Item = new PractitionerItem
            {
                CategoryName = txtCategory.Text.Trim(),
                Code = rdoReplacementCode.Checked ? txtCode.Text.Trim() : string.Empty,
                CodeSource = rdoMaBacSi.Checked ? PractitionerItem.CodeSourceMaBacSi : PractitionerItem.CodeSourceReplacement,
                Description = txtDesc.Text.Trim(),
                CheckAttendance = _sourceItem?.CheckAttendance ?? true,
                ServiceNames = _sourceItem?.ServiceNames == null
                    ? new System.Collections.Generic.List<string>()
                    : new System.Collections.Generic.List<string>(_sourceItem.ServiceNames)
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CodeSource_CheckedChanged(object sender, EventArgs e)
        {
            txtCode.Enabled = rdoReplacementCode.Checked;
        }
    }
}
