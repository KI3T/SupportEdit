using System;
using System.Linq;
using System.Windows.Forms;
using SupportEdit.Models;
using SupportEdit.Services;

namespace SupportEdit.UI
{
    public partial class ErrorCard : UserControl
    {
        private Label lblTitle;
        private Label lblFields;
        private FlowLayoutPanel pnlButtons;
        private Button btnGoto;
        private Button btnFixNgay;
        private Button btnFixNguoi;
        private Button btnFixTBncct;
        private Button btnFixMucHuong;
        private Button btnFixNgayTToan;
        private Button btnFixSoCccd;
        private Button btnFixAll;

        private XmlErrorGroup _group;
        private AutomationService _automation;
        private ConfigService _config;

        public event Action<XmlErrorGroup> OnGoto;
        public event Action<XmlErrorGroup> OnFixNgay;
        public event Action<XmlErrorGroup> OnFixNguoi;
        public event Action<XmlErrorGroup> OnFixTBncct;
        public event Action<XmlErrorGroup> OnFixMucHuong;
        public event Action<XmlErrorGroup> OnFixNgayTToan;
        public event Action<XmlErrorGroup> OnFixSoCccd;
        public event Action<XmlErrorGroup> OnFixAll;

        public ErrorCard()
        {
            InitializeComponent();
            ApplyGroup(null);
        }

        public ErrorCard(XmlErrorGroup group, AutomationService automation, ConfigService config) : this()
        {
            _group = group;
            _automation = automation;
            _config = config;
            ApplyGroup(group);
        }

        private void InitializeComponent()
        {
            lblTitle = new Label { AutoSize = true, Text = "Sửa lỗi XML" };
            lblFields = new Label { AutoSize = true, Top = lblTitle.Bottom + 4, Text = string.Empty };
            pnlButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Top = lblFields.Bottom + 4,
                Left = 0,
                Width = 680,
                MaximumSize = new System.Drawing.Size(680, 0)
            };

            btnGoto = CreateButton("Đi đến");
            btnFixNgay = CreateButton("Sửa NGAY_TH_YL");
            btnFixNguoi = CreateButton("Sửa NGUOI_THUC_HIEN");
            btnFixTBncct = CreateButton("Sửa T_BNCCT");
            btnFixMucHuong = CreateButton("Sửa MUC_HUONG");
            btnFixNgayTToan = CreateButton("Sửa NGAY_TTOAN");
            btnFixSoCccd = CreateButton("Xóa SO_CCCD");
            btnFixAll = CreateButton("Sửa tất cả lỗi hỗ trợ");

            btnGoto.Click += BtnGoto_Click;
            btnFixNgay.Click += BtnFixNgay_Click;
            btnFixNguoi.Click += BtnFixNguoi_Click;
            btnFixTBncct.Click += BtnFixTBncct_Click;
            btnFixMucHuong.Click += BtnFixMucHuong_Click;
            btnFixNgayTToan.Click += BtnFixNgayTToan_Click;
            btnFixSoCccd.Click += BtnFixSoCccd_Click;
            btnFixAll.Click += BtnFixAll_Click;

            Controls.Add(lblTitle);
            Controls.Add(lblFields);
            pnlButtons.Controls.Add(btnGoto);
            pnlButtons.Controls.Add(btnFixNgay);
            pnlButtons.Controls.Add(btnFixNguoi);
            pnlButtons.Controls.Add(btnFixTBncct);
            pnlButtons.Controls.Add(btnFixMucHuong);
            pnlButtons.Controls.Add(btnFixNgayTToan);
            pnlButtons.Controls.Add(btnFixSoCccd);
            pnlButtons.Controls.Add(btnFixAll);
            Controls.Add(pnlButtons);
        }

        private static Button CreateButton(string text)
        {
            return new Button
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 6, 6),
                MinimumSize = new System.Drawing.Size(78, 30),
                Padding = new Padding(8, 2, 8, 2),
                Text = text,
                UseVisualStyleBackColor = true
            };
        }

        private void BtnGoto_Click(object sender, EventArgs e)
        {
            OnGoto?.Invoke(_group);
        }

        private void BtnFixNgay_Click(object sender, EventArgs e)
        {
            OnFixNgay?.Invoke(_group);
        }

        private void BtnFixNguoi_Click(object sender, EventArgs e)
        {
            OnFixNguoi?.Invoke(_group);
        }

        private void BtnFixTBncct_Click(object sender, EventArgs e)
        {
            OnFixTBncct?.Invoke(_group);
        }

        private void BtnFixMucHuong_Click(object sender, EventArgs e)
        {
            OnFixMucHuong?.Invoke(_group);
        }

        private void BtnFixNgayTToan_Click(object sender, EventArgs e)
        {
            OnFixNgayTToan?.Invoke(_group);
        }

        private void BtnFixSoCccd_Click(object sender, EventArgs e)
        {
            OnFixSoCccd?.Invoke(_group);
        }

        private void BtnFixAll_Click(object sender, EventArgs e)
        {
            OnFixAll?.Invoke(_group);
        }

        private void ApplyGroup(XmlErrorGroup group)
        {
            if (group == null)
            {
                lblTitle.Text = "Sửa lỗi XML";
                lblFields.Text = string.Empty;
                btnFixNgay.Visible = false;
                btnFixNguoi.Visible = false;
                btnFixTBncct.Visible = false;
                btnFixMucHuong.Visible = false;
                btnFixNgayTToan.Visible = false;
                btnFixSoCccd.Visible = false;
                btnFixAll.Visible = false;
                Width = 400;
                Height = 80;
                return;
            }

            _group = group;

            lblTitle.Text = group.Stt > 0
                ? $"Sửa lỗi XML{group.Xml}: <STT>{group.Stt}</STT>"
                : $"Sửa lỗi XML{group.Xml}";
            lblFields.Text = string.Join(", ", group.Items.Select(i => i.Field));
            pnlButtons.Top = lblFields.Bottom + 4;

            var hasNgay = group.Items.Any(i => i.Field == "NGAY_TH_YL");
            var hasNguoi = group.Items.Any(i => i.Field == "NGUOI_THUC_HIEN");
            var hasTBncct = group.Items.Any(i => i.Field == "T_BNCCT");
            var hasMucHuong = group.Items.Any(i => i.Field == "MUC_HUONG");
            var hasNgayTToan = group.Items.Any(i => i.Field == "NGAY_TTOAN");
            var hasSoCccd = group.Items.Any(i => i.Field == "SO_CCCD");
            var hasSupportedFix = hasNgay || hasNguoi || hasTBncct || hasMucHuong || hasNgayTToan || hasSoCccd;

            btnGoto.Visible = true;
            btnFixNgay.Visible = hasNgay;
            btnFixNguoi.Visible = hasNguoi;
            btnFixTBncct.Visible = hasTBncct;
            btnFixMucHuong.Visible = hasMucHuong;
            btnFixNgayTToan.Visible = hasNgayTToan;
            btnFixSoCccd.Visible = hasSoCccd;
            btnFixAll.Visible = hasSupportedFix;

            Width = 700;
            Height = pnlButtons.Bottom + 8;
        }
    }
}
