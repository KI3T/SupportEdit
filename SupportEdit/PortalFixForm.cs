using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SupportEdit
{
    public class PortalFixForm : Form
    {
        private readonly List<FixItem> _fixItems = new List<FixItem>();
        private readonly TextBox _txtSearch;
        private readonly Button _btnSearch;
        private readonly FlowLayoutPanel _pnlFixes;
        private readonly Label _lblNoResults;

        public PortalFixForm(Action fixMaBenhKt)
        {
            if (fixMaBenhKt == null) throw new ArgumentNullException(nameof(fixMaBenhKt));

            Text = "Sửa lỗi trên Cổng";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(680, 330);
            MinimumSize = new Size(560, 300);

            _txtSearch = new TextBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 14),
                PlaceholderText = "Nhập tên hoặc mô tả lỗi cần sửa...",
                Size = new Size(548, 27)
            };

            _btnSearch = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(568, 12),
                Size = new Size(100, 31),
                Text = "Tìm kiếm",
                UseVisualStyleBackColor = true
            };

            _pnlFixes = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                Location = new Point(12, 55),
                Padding = new Padding(0, 0, 8, 0),
                Size = new Size(656, 263),
                WrapContents = false
            };

            _lblNoResults = new Label
            {
                AutoSize = true,
                ForeColor = Color.DimGray,
                Margin = new Padding(8, 12, 0, 0),
                Text = "Không tìm thấy lỗi phù hợp.",
                Visible = false
            };

            Controls.Add(_txtSearch);
            Controls.Add(_btnSearch);
            Controls.Add(_pnlFixes);

            AddFixItem(
                "XML1: MA_BENH_KT đi 1 mình",
                "XML1: MA_BENH_KT xxx là mã bệnh khám sức khỏe không được thanh toán BHYT",
                fixMaBenhKt);
            _pnlFixes.Controls.Add(_lblNoResults);

            _btnSearch.Click += (sender, args) => ApplySearch();
            _txtSearch.KeyDown += TxtSearch_KeyDown;
        }

        private void AddFixItem(string title, string description, Action action)
        {
            var panel = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 10),
                Size = new Size(625, 100)
            };

            var button = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                Location = new Point(10, 9),
                Size = new Size(603, 34),
                Text = title,
                UseVisualStyleBackColor = true
            };
            button.Click += (sender, args) => action();

            var label = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
                Location = new Point(11, 53),
                Size = new Size(602, 38),
                Text = description
            };

            panel.Controls.Add(button);
            panel.Controls.Add(label);
            _pnlFixes.Controls.Add(panel);
            _fixItems.Add(new FixItem(panel, title, description));
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            ApplySearch();
            e.SuppressKeyPress = true;
        }

        private void ApplySearch()
        {
            var keyword = _txtSearch.Text.Trim();
            var visibleCount = 0;

            foreach (var item in _fixItems)
            {
                var visible = string.IsNullOrEmpty(keyword) ||
                    item.Title.IndexOf(keyword, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                    item.Description.IndexOf(keyword, StringComparison.InvariantCultureIgnoreCase) >= 0;
                item.Container.Visible = visible;
                if (visible) visibleCount++;
            }

            _lblNoResults.Visible = visibleCount == 0;
        }

        private sealed class FixItem
        {
            public FixItem(Control container, string title, string description)
            {
                Container = container;
                Title = title;
                Description = description;
            }

            public Control Container { get; }
            public string Title { get; }
            public string Description { get; }
        }
    }
}
