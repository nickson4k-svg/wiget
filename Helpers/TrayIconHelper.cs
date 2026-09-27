using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace CalWidget.Helpers
{
    public class TrayIconHelper : IDisposable
    {
        private const int TRAY_ID = 4000;
        private IntPtr _hWnd;
        private HwndSource? _source;
        private bool _isCreated;
        private NativeMethods.NOTIFYICONDATA _nid;

        public event Action? TrayLeftClicked;
        public event Action? TrayRightClicked;

        public void Initialize(Window window, string tooltip)
        {
            var helper = new WindowInteropHelper(window);
            _hWnd = helper.EnsureHandle();

            _source = HwndSource.FromHwnd(_hWnd);
            _source?.AddHook(HwndHook);

            IntPtr hIcon = NativeMethods.LoadIcon(IntPtr.Zero, NativeMethods.IDI_APPLICATION);

            _nid = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = TRAY_ID,
                uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                uCallbackMessage = NativeMethods.WM_TRAYICON,
                hIcon = hIcon,
                szTip = tooltip.Length > 120 ? tooltip.Substring(0, 120) : tooltip
            };

            _isCreated = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _nid);
        }

        public void UpdateTooltip(string tooltip)
        {
            if (!_isCreated) return;
            _nid.szTip = tooltip.Length > 120 ? tooltip.Substring(0, 120) : tooltip;
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref _nid);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_TRAYICON && wParam.ToInt32() == TRAY_ID)
            {
                int mouseMsg = (int)(lParam.ToInt64() & 0xFFFF);
                if (mouseMsg == NativeMethods.WM_LBUTTONUP)
                {
                    TrayLeftClicked?.Invoke();
                    handled = true;
                }
                else if (mouseMsg == NativeMethods.WM_RBUTTONUP)
                {
                    TrayRightClicked?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_isCreated)
            {
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _nid);
                _isCreated = false;
            }
            _source?.RemoveHook(HwndHook);
            _source = null;
            GC.SuppressFinalize(this);
        }
    }
}
