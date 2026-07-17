using System;
using System.Windows.Forms;
using SupportEdit.Services;

namespace SupportEdit
{
    public partial class AppSettingsForm : Form
    {
        private TextBox txtKeyword;
        private TextBox txtProcess;
        private NumericUpDown nudFindDelay;
        private NumericUpDown nudClipRetryCount;
        private NumericUpDown nudClipRetryDelay;
        private Button btnSave;
        private Button btnCancel;
        private Label lbl1;
        private Label lbl2;
        private Label lbl3;
        private Label lbl4;
        private Label lbl5;
        private ConfigService _config;

        public AppSettingsForm()
        {
            InitializeComponent();
        }

        public AppSettingsForm(ConfigService config) : this()
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            LoadFromConfig();
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AppSettingsForm));
            lbl1 = new Label();
            txtKeyword = new TextBox();
            lbl2 = new Label();
            txtProcess = new TextBox();
            lbl3 = new Label();
            nudFindDelay = new NumericUpDown();
            lbl4 = new Label();
            nudClipRetryCount = new NumericUpDown();
            lbl5 = new Label();
            nudClipRetryDelay = new NumericUpDown();
            btnSave = new Button();
            btnCancel = new Button();
            ((System.ComponentModel.ISupportInitialize)nudFindDelay).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudClipRetryCount).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudClipRetryDelay).BeginInit();
            SuspendLayout();
            // 
            // lbl1
            // 
            lbl1.Location = new Point(18, 22);
            lbl1.Name = "lbl1";
            lbl1.Size = new Size(190, 23);
            lbl1.TabIndex = 0;
            lbl1.Text = "Từ khóa tiêu đề cửa sổ:";
            lbl1.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtKeyword
            // 
            txtKeyword.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtKeyword.Location = new Point(214, 22);
            txtKeyword.Name = "txtKeyword";
            txtKeyword.Size = new Size(250, 23);
            txtKeyword.TabIndex = 1;
            // 
            // lbl2
            // 
            lbl2.Location = new Point(18, 58);
            lbl2.Name = "lbl2";
            lbl2.Size = new Size(190, 23);
            lbl2.TabIndex = 2;
            lbl2.Text = "Tên process:";
            lbl2.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtProcess
            // 
            txtProcess.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtProcess.Location = new Point(214, 58);
            txtProcess.Name = "txtProcess";
            txtProcess.Size = new Size(250, 23);
            txtProcess.TabIndex = 3;
            // 
            // lbl3
            // 
            lbl3.Location = new Point(18, 94);
            lbl3.Name = "lbl3";
            lbl3.Size = new Size(190, 23);
            lbl3.TabIndex = 4;
            lbl3.Text = "Độ trễ tìm kiếm (ms):";
            lbl3.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // nudFindDelay
            // 
            nudFindDelay.Location = new Point(214, 94);
            nudFindDelay.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            nudFindDelay.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
            nudFindDelay.Name = "nudFindDelay";
            nudFindDelay.Size = new Size(120, 23);
            nudFindDelay.TabIndex = 5;
            nudFindDelay.Value = new decimal(new int[] { 300, 0, 0, 0 });
            // 
            // lbl4
            // 
            lbl4.Location = new Point(18, 130);
            lbl4.Name = "lbl4";
            lbl4.Size = new Size(190, 23);
            lbl4.TabIndex = 6;
            lbl4.Text = "Số lần thử clipboard:";
            lbl4.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // nudClipRetryCount
            // 
            nudClipRetryCount.Location = new Point(214, 130);
            nudClipRetryCount.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
            nudClipRetryCount.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            nudClipRetryCount.Name = "nudClipRetryCount";
            nudClipRetryCount.Size = new Size(120, 23);
            nudClipRetryCount.TabIndex = 7;
            nudClipRetryCount.Value = new decimal(new int[] { 5, 0, 0, 0 });
            // 
            // lbl5
            // 
            lbl5.Location = new Point(18, 166);
            lbl5.Name = "lbl5";
            lbl5.Size = new Size(190, 23);
            lbl5.TabIndex = 8;
            lbl5.Text = "Độ trễ clipboard (ms):";
            lbl5.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // nudClipRetryDelay
            // 
            nudClipRetryDelay.Location = new Point(214, 166);
            nudClipRetryDelay.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            nudClipRetryDelay.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
            nudClipRetryDelay.Name = "nudClipRetryDelay";
            nudClipRetryDelay.Size = new Size(120, 23);
            nudClipRetryDelay.TabIndex = 9;
            nudClipRetryDelay.Value = new decimal(new int[] { 300, 0, 0, 0 });
            // 
            // btnSave
            // 
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Location = new Point(298, 218);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(80, 28);
            btnSave.TabIndex = 10;
            btnSave.Text = "Lưu";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += BtnSave_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Location = new Point(384, 218);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(80, 28);
            btnCancel.TabIndex = 11;
            btnCancel.Text = "Hủy";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += BtnCancel_Click;
            // 
            // AppSettingsForm
            // 
            ClientSize = new Size(484, 261);
            Controls.Add(lbl1);
            Controls.Add(txtKeyword);
            Controls.Add(lbl2);
            Controls.Add(txtProcess);
            Controls.Add(lbl3);
            Controls.Add(nudFindDelay);
            Controls.Add(lbl4);
            Controls.Add(nudClipRetryCount);
            Controls.Add(lbl5);
            Controls.Add(nudClipRetryDelay);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "AppSettingsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Cấu hình phần mềm XML";
            TopMost = true;
            ((System.ComponentModel.ISupportInitialize)nudFindDelay).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudClipRetryCount).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudClipRetryDelay).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void LoadFromConfig()
        {
            if (_config == null)
            {
                return;
            }

            txtKeyword.Text = _config.XmlAppWindowTitleKeyword ?? string.Empty;
            txtProcess.Text = _config.XmlAppProcessName ?? string.Empty;
            nudFindDelay.Value = Math.Max(nudFindDelay.Minimum, Math.Min(nudFindDelay.Maximum, _config.FindDelayMs));
            nudClipRetryCount.Value = Math.Max(nudClipRetryCount.Minimum, Math.Min(nudClipRetryCount.Maximum, _config.ClipboardRetryCount));
            nudClipRetryDelay.Value = Math.Max(nudClipRetryDelay.Minimum, Math.Min(nudClipRetryDelay.Maximum, _config.ClipboardRetryDelayMs));
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_config == null)
            {
                return;
            }

            // Validate
            if (string.IsNullOrWhiteSpace(txtKeyword.Text))
            {
                MessageBox.Show("XmlAppWindowTitleKeyword không được rỗng.");
                return;
            }
            if (nudFindDelay.Value < 100)
            {
                MessageBox.Show("FindDelayMs phải >= 100.");
                return;
            }
            if (nudClipRetryCount.Value < 1)
            {
                MessageBox.Show("ClipboardRetryCount phải >= 1.");
                return;
            }
            if (nudClipRetryDelay.Value < 100)
            {
                MessageBox.Show("ClipboardRetryDelayMs phải >= 100.");
                return;
            }

            _config.XmlAppWindowTitleKeyword = txtKeyword.Text.Trim();
            _config.XmlAppProcessName = txtProcess.Text.Trim();
            _config.FindDelayMs = (int)nudFindDelay.Value;
            _config.ClipboardRetryCount = (int)nudClipRetryCount.Value;
            _config.ClipboardRetryDelayMs = (int)nudClipRetryDelay.Value;

            _config.Save();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
