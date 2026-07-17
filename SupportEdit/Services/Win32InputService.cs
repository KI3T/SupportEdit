using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace SupportEdit.Services
{
    public class Win32InputService
    {
        private readonly ConfigService _config;
        public string LastSendInputDebug { get; private set; } = string.Empty;

        public Win32InputService(ConfigService config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool SendCtrlHome() => SendKeyboardShortcut(VirtualKey.Control, VirtualKey.Home);
        public bool SendCtrlShiftEnd() => SendKeyboardShortcut(VirtualKey.Control, VirtualKey.Shift, VirtualKey.End);
        public bool SendCtrlC() => SendKeyboardShortcut(VirtualKey.Control, VirtualKey.C);
        public bool SendEsc() => SendKeyboardShortcut(VirtualKey.Escape);
        public bool SendKeyDown(VirtualKey key) => SendSingleKey(key, true);
        public bool SendKeyUp(VirtualKey key) => SendSingleKey(key, false);

        private bool SendSingleKey(VirtualKey key, bool keyDown)
        {
            try
            {
                LastSendInputDebug = string.Empty;
                var input = keyDown
                    ? NativeMethods.INPUT.KeyDown((ushort)key)
                    : NativeMethods.INPUT.KeyUp((ushort)key);
                uint sent = NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
                LastSendInputDebug = BuildSendInputDebug("single-vk", 1, sent);
                return sent == 1;
            }
            catch (Exception ex)
            {
                LastSendInputDebug = "single-vk exception: " + ex;
                return false;
            }
        }

        public bool SendKeyboardShortcut(params VirtualKey[] keys)
        {
            try
            {
                LastSendInputDebug = string.Empty;
                if (keys == null || keys.Length == 0)
                {
                    return false;
                }

                var inputs = BuildKeyboardInputs(keys);
                uint sent = NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(NativeMethods.INPUT)));
                LastSendInputDebug = BuildSendInputDebug("batch-vk", inputs.Count, sent);
                if (sent == inputs.Count)
                {
                    return true;
                }

                var scanInputs = BuildKeyboardInputsByScanCode(keys);
                uint scanSent = NativeMethods.SendInput((uint)scanInputs.Count, scanInputs.ToArray(), Marshal.SizeOf(typeof(NativeMethods.INPUT)));
                LastSendInputDebug += Environment.NewLine + BuildSendInputDebug("batch-scancode", scanInputs.Count, scanSent);
                if (scanSent == scanInputs.Count)
                {
                    return true;
                }

                return SendKeyboardShortcutStepwise(keys);
            }
            catch (Exception ex)
            {
                LastSendInputDebug = "shortcut exception: " + ex;
                return false;
            }
        }

        private List<NativeMethods.INPUT> BuildKeyboardInputs(params VirtualKey[] keys)
        {
            var list = new List<NativeMethods.INPUT>();
            foreach (var key in keys)
            {
                list.Add(NativeMethods.INPUT.KeyDown((ushort)key));
                Thread.Sleep(10);
            }
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                list.Add(NativeMethods.INPUT.KeyUp((ushort)keys[i]));
                Thread.Sleep(10);
            }
            return list;
        }

        private List<NativeMethods.INPUT> BuildKeyboardInputsByScanCode(params VirtualKey[] keys)
        {
            var list = new List<NativeMethods.INPUT>();
            foreach (var key in keys)
            {
                list.Add(NativeMethods.INPUT.ScanDown((ushort)key, GetScanCode(key), IsExtendedKey(key)));
            }
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                list.Add(NativeMethods.INPUT.ScanUp((ushort)keys[i], GetScanCode(keys[i]), IsExtendedKey(keys[i])));
            }
            return list;
        }

        private bool SendKeyboardShortcutStepwise(params VirtualKey[] keys)
        {
            var debug = new List<string>();
            var ok = true;

            foreach (var key in keys)
            {
                ok &= SendOneInput(NativeMethods.INPUT.KeyDown((ushort)key), "step-vk-down-" + key, debug);
                Thread.Sleep(20);
            }
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                ok &= SendOneInput(NativeMethods.INPUT.KeyUp((ushort)keys[i]), "step-vk-up-" + keys[i], debug);
                Thread.Sleep(20);
            }

            if (ok)
            {
                LastSendInputDebug += Environment.NewLine + string.Join(Environment.NewLine, debug);
                return true;
            }

            debug.Add("step-vk failed; trying step-scancode");
            ok = true;
            foreach (var key in keys)
            {
                ok &= SendOneInput(NativeMethods.INPUT.ScanDown((ushort)key, GetScanCode(key), IsExtendedKey(key)), "step-scan-down-" + key, debug);
                Thread.Sleep(20);
            }
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                ok &= SendOneInput(NativeMethods.INPUT.ScanUp((ushort)keys[i], GetScanCode(keys[i]), IsExtendedKey(keys[i])), "step-scan-up-" + keys[i], debug);
                Thread.Sleep(20);
            }

            LastSendInputDebug += Environment.NewLine + string.Join(Environment.NewLine, debug);
            return ok;
        }

        private bool SendOneInput(NativeMethods.INPUT input, string label, List<string> debug)
        {
            NativeMethods.SetLastError(0);
            uint sent = NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            debug.Add(BuildSendInputDebug(label, 1, sent));
            return sent == 1;
        }

        private ushort GetScanCode(VirtualKey key)
        {
            return (ushort)NativeMethods.MapVirtualKey((uint)key, 0);
        }

        private bool IsExtendedKey(VirtualKey key)
        {
            return key == VirtualKey.Home || key == VirtualKey.End;
        }

        private string BuildSendInputDebug(string label, int expected, uint sent)
        {
            var lastError = Marshal.GetLastWin32Error();
            return $"{label}: expected={expected}, sent={sent}, cbSize={Marshal.SizeOf(typeof(NativeMethods.INPUT))}, lastError={lastError}, currentProcessElevated={IsCurrentProcessElevated()}";
        }

        private bool IsCurrentProcessElevated()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public enum VirtualKey : ushort
        {
            Control = 0x11,
            Shift = 0x10,
            Home = 0x24,
            End = 0x23,
            C = 0x43,
            Escape = 0x1B
        }

        private static class NativeMethods
        {
            [StructLayout(LayoutKind.Explicit, Size = 40)]
            public struct INPUT
            {
                [FieldOffset(0)] public uint type;
                [FieldOffset(8)] public KEYBDINPUT ki;

                public static INPUT KeyDown(ushort key)
                {
                    return new INPUT
                    {
                        type = 1,
                        ki = new KEYBDINPUT
                        {
                            wVk = key,
                            wScan = 0,
                            dwFlags = 0,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    };
                }

                public static INPUT KeyUp(ushort key)
                {
                    return new INPUT
                    {
                        type = 1,
                        ki = new KEYBDINPUT
                        {
                            wVk = key,
                            wScan = 0,
                            dwFlags = 0x0002,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    };
                }

                public static INPUT ScanDown(ushort key, ushort scanCode, bool extended)
                {
                    return new INPUT
                    {
                        type = 1,
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = scanCode,
                            dwFlags = KEYEVENTF_SCANCODE | (extended ? KEYEVENTF_EXTENDEDKEY : 0),
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    };
                }

                public static INPUT ScanUp(ushort key, ushort scanCode, bool extended)
                {
                    return new INPUT
                    {
                        type = 1,
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = scanCode,
                            dwFlags = KEYEVENTF_KEYUP | KEYEVENTF_SCANCODE | (extended ? KEYEVENTF_EXTENDEDKEY : 0),
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    };
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct KEYBDINPUT
            {
                public ushort wVk;
                public ushort wScan;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            [DllImport("user32.dll", SetLastError = true)]
            public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

            [DllImport("user32.dll")]
            public static extern uint MapVirtualKey(uint uCode, uint uMapType);

            [DllImport("kernel32.dll")]
            public static extern void SetLastError(uint dwErrCode);

            private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
            private const uint KEYEVENTF_KEYUP = 0x0002;
            private const uint KEYEVENTF_SCANCODE = 0x0008;
        }
    }
}
