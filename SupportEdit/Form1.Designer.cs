namespace SupportEdit
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            txtErrorRaw = new TextBox();
            btnReadError = new Button();
            btnDongBoVAS = new Button();
            btnPortalFix = new Button();
            pnlErrors = new FlowLayoutPanel();
            txtLog = new TextBox();
            menu = new MenuStrip();
            menuConfig = new ToolStripMenuItem();
            miPract = new ToolStripMenuItem();
            miTitle = new ToolStripMenuItem();
            menuSavePosition = new ToolStripMenuItem();
            menuSearchRecords = new ToolStripMenuItem();
            miFindDischargeDate = new ToolStripMenuItem();
            menuDebug = new ToolStripMenuItem();
            miTestXml3 = new ToolStripMenuItem();
            miOpenDebug = new ToolStripMenuItem();
            label1 = new Label();
            labelCancelShortcut = new Label();
            menu.SuspendLayout();
            SuspendLayout();
            // 
            // txtErrorRaw
            // 
            txtErrorRaw.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtErrorRaw.Location = new Point(14, 48);
            txtErrorRaw.Margin = new Padding(3, 4, 3, 4);
            txtErrorRaw.Multiline = true;
            txtErrorRaw.Name = "txtErrorRaw";
            txtErrorRaw.PlaceholderText = "Dán nội dung lỗi XML vào đây nếu không đọc trực tiếp được từ phần mềm XML";
            txtErrorRaw.ScrollBars = ScrollBars.Vertical;
            txtErrorRaw.Size = new Size(1028, 159);
            txtErrorRaw.TabIndex = 1;
            // 
            // btnReadError
            // 
            btnReadError.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnReadError.Location = new Point(1061, 48);
            btnReadError.Margin = new Padding(3, 4, 3, 4);
            btnReadError.Name = "btnReadError";
            btnReadError.Size = new Size(171, 51);
            btnReadError.TabIndex = 2;
            btnReadError.Text = "Đọc lỗi";
            btnReadError.UseVisualStyleBackColor = true;
            // 
            // btnDongBoVAS
            // 
            btnDongBoVAS.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDongBoVAS.Location = new Point(1061, 105);
            btnDongBoVAS.Margin = new Padding(3, 4, 3, 4);
            btnDongBoVAS.Name = "btnDongBoVAS";
            btnDongBoVAS.Size = new Size(171, 51);
            btnDongBoVAS.TabIndex = 3;
            btnDongBoVAS.Text = "Đồng bộ VAS";
            btnDongBoVAS.UseVisualStyleBackColor = true;
            // 
            // btnPortalFix
            // 
            btnPortalFix.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnPortalFix.Location = new Point(1061, 162);
            btnPortalFix.Margin = new Padding(3, 4, 3, 4);
            btnPortalFix.Name = "btnPortalFix";
            btnPortalFix.Size = new Size(171, 45);
            btnPortalFix.TabIndex = 4;
            btnPortalFix.Text = "Sửa lỗi trên Cổng";
            btnPortalFix.UseVisualStyleBackColor = true;
            // 
            // pnlErrors
            // 
            pnlErrors.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlErrors.AutoScroll = true;
            pnlErrors.FlowDirection = FlowDirection.TopDown;
            pnlErrors.Location = new Point(14, 264);
            pnlErrors.Margin = new Padding(3, 4, 3, 4);
            pnlErrors.Name = "pnlErrors";
            pnlErrors.Size = new Size(1218, 403);
            pnlErrors.TabIndex = 4;
            pnlErrors.WrapContents = false;
            // 
            // txtLog
            // 
            txtLog.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtLog.BackColor = Color.FromArgb(20, 20, 20);
            txtLog.ForeColor = Color.Gainsboro;
            txtLog.Location = new Point(14, 683);
            txtLog.Margin = new Padding(3, 4, 3, 4);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(1218, 233);
            txtLog.TabIndex = 4;
            // 
            // menu
            // 
            menu.ImageScalingSize = new Size(20, 20);
            menu.Items.AddRange(new ToolStripItem[] { menuConfig, menuSavePosition, menuSearchRecords, menuDebug });
            menu.Location = new Point(0, 0);
            menu.Name = "menu";
            menu.Padding = new Padding(7, 3, 0, 3);
            menu.Size = new Size(1257, 30);
            menu.TabIndex = 0;
            // 
            // menuConfig
            // 
            menuConfig.DropDownItems.AddRange(new ToolStripItem[] { miPract, miTitle });
            menuConfig.Name = "menuConfig";
            menuConfig.Size = new Size(80, 24);
            menuConfig.Text = "Cấu hình";
            // 
            // miPract
            // 
            miPract.Name = "miPract";
            miPract.Size = new Size(354, 26);
            miPract.Text = "Quản lý người thực hiện";
            // 
            // miTitle
            // 
            miTitle.Name = "miTitle";
            miTitle.Size = new Size(354, 26);
            miTitle.Text = "Cấu hình tiêu đề cửa sổ phần mềm XML";
            // 
            // menuSavePosition
            // 
            menuSavePosition.Name = "menuSavePosition";
            menuSavePosition.Size = new Size(91, 24);
            menuSavePosition.Text = "Lưu vị trí";
            // 
            // menuSearchRecords
            // 
            menuSearchRecords.DropDownItems.AddRange(new ToolStripItem[] { miFindDischargeDate });
            menuSearchRecords.Name = "menuSearchRecords";
            menuSearchRecords.Size = new Size(128, 24);
            menuSearchRecords.Text = "Tìm kiếm hồ sơ";
            // 
            // miFindDischargeDate
            // 
            miFindDischargeDate.Name = "miFindDischargeDate";
            miFindDischargeDate.Size = new Size(224, 26);
            miFindDischargeDate.Text = "Tìm ngày ra viện";
            // 
            // menuDebug
            // 
            menuDebug.DropDownItems.AddRange(new ToolStripItem[] { miTestXml3, miOpenDebug });
            menuDebug.Name = "menuDebug";
            menuDebug.Size = new Size(68, 24);
            menuDebug.Text = "Debug";
            // 
            // miTestXml3
            // 
            miTestXml3.Name = "miTestXml3";
            miTestXml3.Size = new Size(203, 26);
            miTestXml3.Text = "Test đọc XML3";
            // 
            // miOpenDebug
            // 
            miOpenDebug.Name = "miOpenDebug";
            miOpenDebug.Size = new Size(203, 26);
            miOpenDebug.Text = "Mở DebugReads";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.ForeColor = Color.Brown;
            label1.Location = new Point(634, 211);
            label1.Name = "label1";
            label1.Size = new Size(408, 20);
            label1.TabIndex = 5;
            label1.Text = "Lưu ý: Khi tool chạy thì không đụng vào chuột hay bàn phím ";
            label1.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // labelCancelShortcut
            // 
            labelCancelShortcut.AutoSize = true;
            labelCancelShortcut.ForeColor = Color.Brown;
            labelCancelShortcut.Location = new Point(634, 235);
            labelCancelShortcut.Name = "labelCancelShortcut";
            labelCancelShortcut.Size = new Size(321, 20);
            labelCancelShortcut.TabIndex = 6;
            labelCancelShortcut.Text = "Nhấn tổ hợp Shift + Esc để dừng thao tác sửa";
            labelCancelShortcut.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1257, 933);
            Controls.Add(labelCancelShortcut);
            Controls.Add(label1);
            Controls.Add(menu);
            Controls.Add(txtErrorRaw);
            Controls.Add(btnReadError);
            Controls.Add(btnDongBoVAS);
            Controls.Add(btnPortalFix);
            Controls.Add(pnlErrors);
            Controls.Add(txtLog);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menu;
            Margin = new Padding(3, 4, 3, 4);
            Name = "Form1";
            Tag = new object[]
    {
    miPract,
    miTitle,
    miTestXml3,
    miOpenDebug
    };
            Text = "SupportEdit";
            menu.ResumeLayout(false);
            menu.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menu;
        private ToolStripMenuItem menuConfig;
        private ToolStripMenuItem miPract;
        private ToolStripMenuItem miTitle;
        private ToolStripMenuItem menuSavePosition;
        private ToolStripMenuItem menuSearchRecords;
        private ToolStripMenuItem miFindDischargeDate;
        private ToolStripMenuItem menuDebug;
        private ToolStripMenuItem miTestXml3;
        private ToolStripMenuItem miOpenDebug;
        private Label label1;
        private Label labelCancelShortcut;
        private Button btnDongBoVAS;
        private Button btnPortalFix;
    }
}
