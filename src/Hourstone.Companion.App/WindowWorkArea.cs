using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Hourstone.Companion.App;

public sealed class WindowWorkArea : IDisposable
{
    private readonly HwndSource source;

    public WindowWorkArea(Window window)
    {
        source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)
            ?? throw new InvalidOperationException("Window handle is not initialized.");
        source.AddHook(HandleMessage);
    }

    // Monitor APIs and MINMAXINFO use physical pixels; do not apply WPF's DPI scale a second time.
    public static Rectangle RelativeWorkArea(Rectangle monitor, Rectangle workArea) =>
        new(workArea.Left - monitor.Left, workArea.Top - monitor.Top, workArea.Width, workArea.Height);

    private IntPtr HandleMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x0024 || lParam == IntPtr.Zero) return IntPtr.Zero; // WM_GETMINMAXINFO
        var monitor = MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;
        var bounds = RelativeWorkArea(info.Monitor.ToRectangle(), info.Work.ToRectangle());
        if (bounds.Width <= 0 || bounds.Height <= 0) return IntPtr.Zero;
        var limits = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        limits.MaxPosition = new NativePoint(bounds.X, bounds.Y);
        limits.MaxSize = new NativePoint(bounds.Width, bounds.Height);
        limits.MaxTrackSize = limits.MaxSize;
        limits.MinTrackSize = new NativePoint(Math.Min(limits.MinTrackSize.X, bounds.Width), Math.Min(limits.MinTrackSize.Y, bounds.Height));
        Marshal.StructureToPtr(limits, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    public void Dispose() => source.RemoveHook(HandleMessage);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y) { public int X = x; public int Y = y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo { public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}