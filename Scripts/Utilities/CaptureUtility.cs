using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#nullable enable

// Credit to Maurice Flanagan and Jargon
// https://stackoverflow.com/a/911225

[SupportedOSPlatform("windows")]
public static partial class CaptureUtility {
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hWnd, out RECT lpRect);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PrintWindow(nint hWnd, nint hdcBlt, int nFlags);
    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    private const int PW_CLIENTONLY = 0x1;
    private const int PW_RENDERFULLCONTENT = 0x2;

    public static Bitmap CaptureWindow(nint Handle) {
        GetWindowRect(Handle, out RECT Rect);

        Bitmap Bitmap = new(Rect.Width, Rect.Height);
        using Graphics Graphics = Graphics.FromImage(Bitmap);
        nint HdcBitmap = Graphics.GetHdc();

        // Pass flags to capture hardware accelerated windows
        PrintWindow(Handle, HdcBitmap, PW_CLIENTONLY | PW_RENDERFULLCONTENT);

        return Bitmap;
    }
    public static byte[] CaptureWindow(nint Handle, System.Drawing.Imaging.ImageFormat Format) {
        using Bitmap Bitmap = CaptureWindow(Handle);
    
        using MemoryStream MemoryStream = new();
        Bitmap.Save(MemoryStream, Format);
        return MemoryStream.ToArray();
    }
    public static nint GetForegroundWindowHandle() {
        return GetForegroundWindow();
    }
    public static Godot.Rect2I GetWindowRect2I(nint Handle) {
        GetWindowRect(Handle, out RECT Rect);
        return new Godot.Rect2I(Rect.Left, Rect.Top, Rect.Width, Rect.Height);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT(int Left, int Top, int Right, int Bottom) {
        public int Left = Left;
        public int Top = Top;
        public int Right = Right;
        public int Bottom = Bottom;

        public int Width {
            readonly get => Right - Left;
            set => Right = value + Left;
        }
        public int Height {
            readonly get => Bottom - Top;
            set => Bottom = value + Top;
        }
    }
}