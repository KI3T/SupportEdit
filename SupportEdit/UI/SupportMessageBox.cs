using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SupportEdit.UI
{
    /// <summary>
    /// Keeps SupportEdit notifications above the external XML tool that was
    /// activated immediately before an automation result is reported.
    /// </summary>
    internal static class SupportMessageBox
    {
        public static DialogResult Show(string text)
        {
            return ShowCore(null, text, "SupportEdit", MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        public static DialogResult Show(string text, string caption)
        {
            return ShowCore(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons)
        {
            return ShowCore(null, text, caption, buttons, MessageBoxIcon.None);
        }

        public static DialogResult Show(
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon)
        {
            return ShowCore(null, text, caption, buttons, icon);
        }

        public static DialogResult Show(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon)
        {
            return ShowCore(owner, text, caption, buttons, icon);
        }

        private static DialogResult ShowCore(
            IWin32Window? requestedOwner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon)
        {
            var owner = ResolveOwner(requestedOwner);
            if (owner == null || owner.IsDisposed)
            {
                return System.Windows.Forms.MessageBox.Show(text, caption, buttons, icon);
            }

            if (owner.InvokeRequired)
            {
                return (DialogResult)owner.Invoke(new Func<DialogResult>(() =>
                    ShowCore(owner, text, caption, buttons, icon)));
            }

            var previousTopMost = owner.TopMost;
            try
            {
                if (owner.WindowState == FormWindowState.Minimized)
                {
                    owner.WindowState = FormWindowState.Normal;
                }

                owner.Show();
                owner.TopMost = true;
                NativeMethods.ShowWindow(owner.Handle, NativeMethods.SW_RESTORE);
                NativeMethods.BringWindowToTop(owner.Handle);
                NativeMethods.SetForegroundWindow(owner.Handle);
                owner.BringToFront();
                owner.Activate();

                return System.Windows.Forms.MessageBox.Show(owner, text, caption, buttons, icon);
            }
            finally
            {
                if (!owner.IsDisposed)
                {
                    owner.TopMost = previousTopMost;
                }
            }
        }

        private static Form? ResolveOwner(IWin32Window? requestedOwner)
        {
            if (requestedOwner is Form requestedForm)
            {
                return requestedForm;
            }

            if (requestedOwner is Control requestedControl)
            {
                return requestedControl.FindForm();
            }

            return Application.OpenForms
                .Cast<Form>()
                .FirstOrDefault(form => form is Form1 && !form.IsDisposed)
                ?? Form.ActiveForm;
        }

        private static class NativeMethods
        {
            public const int SW_RESTORE = 9;

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int command);

            [DllImport("user32.dll")]
            public static extern bool BringWindowToTop(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);
        }
    }
}
