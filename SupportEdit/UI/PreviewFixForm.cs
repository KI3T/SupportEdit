using System;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using SupportEdit.Models;

namespace SupportEdit.UI
{
    public partial class PreviewFixForm : Form
    {
        private TextBox txtBefore;
        private TextBox txtAfter;
        private ListBox lstFields;
        private TextBox txtBlockInfo;
        private Button btnConfirm;
        private Button btnCancel;

        public bool Confirmed { get; private set; }

        public PreviewFixForm()
        {
            InitializeComponent();
        }

        public PreviewFixForm(string before, string after, IEnumerable<string> fixedFields, IEnumerable<string> messages, Dictionary<string, string> oldBlock = null, Dictionary<string, string> newBlock = null) : this()
        {
            LoadPreview(before, after, fixedFields, messages, oldBlock, newBlock);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PreviewFixForm));
            txtBefore = new TextBox();
            txtAfter = new TextBox();
            lstFields = new ListBox();
            txtBlockInfo = new TextBox();
            btnConfirm = new Button();
            btnCancel = new Button();
            SuspendLayout();
            // 
            // txtBefore
            // 
            txtBefore.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            txtBefore.Font = new Font("Consolas", 9F);
            txtBefore.Location = new Point(12, 12);
            txtBefore.Multiline = true;
            txtBefore.Name = "txtBefore";
            txtBefore.ReadOnly = true;
            txtBefore.ScrollBars = ScrollBars.Both;
            txtBefore.Size = new Size(315, 500);
            txtBefore.TabIndex = 0;
            txtBefore.WordWrap = false;
            // 
            // txtAfter
            // 
            txtAfter.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtAfter.Font = new Font("Consolas", 9F);
            txtAfter.Location = new Point(333, 12);
            txtAfter.Multiline = true;
            txtAfter.Name = "txtAfter";
            txtAfter.ReadOnly = true;
            txtAfter.ScrollBars = ScrollBars.Both;
            txtAfter.Size = new Size(639, 500);
            txtAfter.TabIndex = 1;
            txtAfter.WordWrap = false;
            // 
            // lstFields
            // 
            lstFields.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lstFields.ItemHeight = 15;
            lstFields.Location = new Point(12, 524);
            lstFields.Name = "lstFields";
            lstFields.Size = new Size(315, 88);
            lstFields.TabIndex = 2;
            // 
            // txtBlockInfo
            // 
            txtBlockInfo.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtBlockInfo.Location = new Point(333, 524);
            txtBlockInfo.Multiline = true;
            txtBlockInfo.Name = "txtBlockInfo";
            txtBlockInfo.ReadOnly = true;
            txtBlockInfo.ScrollBars = ScrollBars.Vertical;
            txtBlockInfo.Size = new Size(459, 88);
            txtBlockInfo.TabIndex = 5;
            // 
            // btnConfirm
            // 
            btnConfirm.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnConfirm.Location = new Point(798, 524);
            btnConfirm.Name = "btnConfirm";
            btnConfirm.Size = new Size(174, 34);
            btnConfirm.TabIndex = 3;
            btnConfirm.Text = "Xác nhận sửa";
            btnConfirm.UseVisualStyleBackColor = true;
            btnConfirm.Click += BtnConfirm_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Location = new Point(798, 578);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(174, 34);
            btnCancel.TabIndex = 4;
            btnCancel.Text = "Hủy";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += BtnCancel_Click;
            // 
            // PreviewFixForm
            // 
            AcceptButton = btnConfirm;
            CancelButton = btnCancel;
            ClientSize = new Size(984, 661);
            Controls.Add(txtBefore);
            Controls.Add(txtAfter);
            Controls.Add(lstFields);
            Controls.Add(btnConfirm);
            Controls.Add(btnCancel);
            Controls.Add(txtBlockInfo);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "PreviewFixForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Xem trước và xác nhận";
            TopMost = true;
            ResumeLayout(false);
            PerformLayout();
        }

        private void BtnConfirm_Click(object sender, EventArgs e)
        {
            Confirmed = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            Confirmed = false;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void LoadPreview(string before, string after, IEnumerable<string> fixedFields, IEnumerable<string> messages, Dictionary<string, string> oldBlock, Dictionary<string, string> newBlock)
        {
            txtBefore.Text = before ?? string.Empty;
            txtAfter.Text = after ?? string.Empty;

            lstFields.Items.Clear();
            foreach (var f in fixedFields ?? Enumerable.Empty<string>())
            {
                lstFields.Items.Add(f);
            }

            txtBlockInfo.Clear();
            if (messages != null)
            {
                var m = string.Join("; ", messages.Take(5));
                if (!string.IsNullOrEmpty(m)) this.Text += " - " + m;
            }

            if (oldBlock != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"STT: {GetVal(oldBlock, "STT")}");
                sb.AppendLine($"MA_NHOM: {GetVal(oldBlock, "MA_NHOM")}");
                var ten = GetVal(oldBlock, "TEN_DICH_VU") ?? GetVal(oldBlock, "TEN_VAT_TU");
                sb.AppendLine($"TEN: {ten}");
                sb.AppendLine($"NGAY_YL: {GetVal(oldBlock, "NGAY_YL")}");
                sb.AppendLine($"NGAY_TH_YL (old): {GetVal(oldBlock, "NGAY_TH_YL")}");
                sb.AppendLine($"NGUOI_THUC_HIEN (old): {GetVal(oldBlock, "NGUOI_THUC_HIEN")}");
                txtBlockInfo.Text = sb.ToString();
            }

            if (newBlock != null)
            {
                var sb2 = new System.Text.StringBuilder();
                sb2.AppendLine($"NGAY_TH_YL (new): {GetVal(newBlock, "NGAY_TH_YL")}");
                sb2.AppendLine($"NGUOI_THUC_HIEN (new): {GetVal(newBlock, "NGUOI_THUC_HIEN")}");
                txtBlockInfo.Text += "\r\n" + sb2.ToString();
            }
        }

        private string GetVal(Dictionary<string, string> d, string k)
        {
            if (d == null) return null;
            if (d.ContainsKey(k)) return d[k];
            return null;
        }
    }
}
