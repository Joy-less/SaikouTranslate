using Godot;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

// Credit to KitzuGG:
// https://forum.godotengine.org/t/mousepassthrough-for-non-same-applications/49284/3

[SupportedOSPlatform("Windows")]
public static partial class WindowPassthroughUtility {
	private const int GWL_EXSTYLE = -20;
	private const long WS_EX_TRANSPARENT = 0x00000020L;
	private const long WS_EX_LAYERED = 0x00080000L;

	[LibraryImport("user32.dll")]
	private static partial long GetWindowLongPtrA(nint hWnd, int nIndex);
	[LibraryImport("user32.dll")]
	private static partial nint SetWindowLongPtrA(nint hWnd, int nIndex, long dwNewLong);

	public static void SetWindowPassthrough(nint HWND, bool Enable) {
		long CurrentWindowStyle = GetWindowLongPtrA(HWND, GWL_EXSTYLE);
		if (Enable) {
			CurrentWindowStyle &= ~WS_EX_TRANSPARENT;
			CurrentWindowStyle &= ~WS_EX_LAYERED;
		}
		else {
			CurrentWindowStyle |= WS_EX_TRANSPARENT;
			CurrentWindowStyle |= WS_EX_LAYERED;
		}
	    SetWindowLongPtrA(HWND, GWL_EXSTYLE, CurrentWindowStyle);
	}
	public static void SetWindowPassthrough(Window Window, bool Enable) {
		SetWindowPassthrough(GetWindowHandle(Window.GetWindowId()), Enable);
	}
	public static nint GetWindowHandle(int WindowId) {
		return (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, WindowId);
	}
}