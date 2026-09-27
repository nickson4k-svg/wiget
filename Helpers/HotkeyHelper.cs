using System;
using System.Windows;
using System.Windows.Interop;

namespace CalWidget.Helpers
{
    public class HotkeyHelper : IDisposable
    {
        private const int HOTKEY_ID = 9000;
        private IntPtr _hWnd;
        private HwndSource? _source;
        public event Action? HotkeyPressed;

        public bool Register(Window window, uint modifiers, uint virtualKey)
        {
            var helper = new WindowInteropHelper(window);
            _hWnd = helper.EnsureHandle();

            _source = HwndSource.FromHwnd(_hWnd);
            _source?.AddHook(HwndHook);

            return NativeMethods.RegisterHotKey(_hWnd, HOTKEY_ID, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_hWnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_hWnd, HOTKEY_ID);
                _source?.RemoveHook(HwndHook);
                _source = null;
                _hWnd = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }
    }
}
