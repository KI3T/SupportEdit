using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SupportEdit.Services
{
    public static class AutomationCancellation
    {
        private const int VkEscape = 0x1B;
        private const int VkShift = 0x10;
        private static int _session;
        private static int _active;
        private static int _cancelRequested;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKeyCode);

        public static bool IsCancellationRequested => Volatile.Read(ref _cancelRequested) != 0;

        public static void Begin()
        {
            var session = Interlocked.Increment(ref _session);
            Volatile.Write(ref _cancelRequested, 0);
            Volatile.Write(ref _active, 1);
            var escapeInitiallyDown = IsKeyDown(VkEscape);

            var monitor = new Thread(() => MonitorCancelShortcut(session, escapeInitiallyDown))
            {
                IsBackground = true,
                Name = "SupportEdit-ShiftEscapeMonitor"
            };
            monitor.Start();
        }

        public static void End()
        {
            Volatile.Write(ref _active, 0);
            Interlocked.Increment(ref _session);
        }

        public static void ThrowIfCancellationRequested()
        {
            if (IsCancellationRequested)
            {
                throw new OperationCanceledException("Người dùng đã nhấn Shift + Esc để ngừng sửa lỗi.");
            }
        }

        private static void MonitorCancelShortcut(int session, bool escapePreviouslyDown)
        {
            var armed = !escapePreviouslyDown;
            while (Volatile.Read(ref _active) != 0 && Volatile.Read(ref _session) == session)
            {
                var escapeDown = IsKeyDown(VkEscape);
                var shiftDown = IsKeyDown(VkShift);

                if (!escapeDown)
                {
                    armed = true;
                }
                else if (armed && !escapePreviouslyDown && shiftDown)
                {
                    Volatile.Write(ref _cancelRequested, 1);
                    return;
                }

                escapePreviouslyDown = escapeDown;
                Thread.Sleep(10);
            }
        }

        private static bool IsKeyDown(int virtualKeyCode)
        {
            return (GetAsyncKeyState(virtualKeyCode) & 0x8000) != 0;
        }
    }
}
