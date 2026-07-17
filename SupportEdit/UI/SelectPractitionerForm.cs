using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SupportEdit.Models;

namespace SupportEdit.UI
{
    public partial class SelectPractitionerForm : Form
    {
        private ListBox lst;
        private Button btnOk;
        public PractitionerItem Selected { get; private set; }

        public SelectPractitionerForm()
        {
            InitializeComponent();
        }

        public SelectPractitionerForm(List<PractitionerItem> items) : this()
        {
            LoadItems(items);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SelectPractitionerForm));
            lst = new ListBox();
            btnOk = new Button();
            SuspendLayout();
            // 
            // lst
            // 
            lst.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lst.ItemHeight = 15;
            lst.Location = new Point(12, 12);
            lst.Name = "lst";
            lst.Size = new Size(360, 292);
            lst.TabIndex = 0;
            // 
            // btnOk
            // 
            btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOk.Location = new Point(292, 320);
            btnOk.Name = "btnOk";
            btnOk.Size = new Size(80, 28);
            btnOk.TabIndex = 1;
            btnOk.Text = "Chọn";
            btnOk.UseVisualStyleBackColor = true;
            btnOk.Click += BtnOk_Click;
            // 
            // SelectPractitionerForm
            // 
            AcceptButton = btnOk;
            ClientSize = new Size(384, 361);
            Controls.Add(lst);
            Controls.Add(btnOk);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "SelectPractitionerForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Chọn người thực hiện";
            TopMost = true;
            ResumeLayout(false);
        }

        private void LoadItems(IEnumerable<PractitionerItem> items)
        {
            lst.Items.Clear();
            if (items != null)
            {
                foreach (var it in items)
                {
                    lst.Items.Add(it);
                }
            }

            lst.DisplayMember = "Description";
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            Selected = lst.SelectedItem as PractitionerItem;
            DialogResult = Selected != null ? DialogResult.OK : DialogResult.Cancel;
            Close();
        }
    }
}
