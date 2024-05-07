using Godot;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#pragma warning disable CA1806
#pragma warning disable SYSLIB1054

// Credit to KitzuGG:
// https://forum.godotengine.org/t/mousepassthrough-for-non-same-applications/49284/3

[SupportedOSPlatform("windows")]
public static class WindowPassthroughUtility {
	private const int GWL_EXSTYLE = -20;
	private const long WS_EX_TRANSPARENT = 0x00000020L;
	private const long WS_EX_LAYERED = 0x00080000L;

	[DllImport("user32.dll")]
	private static extern long GetWindowLong(int hWnd, int nIndex);
	[DllImport("user32.dll")]
	private static extern int SetWindowLong(int hWnd, int nIndex, long dwNewLong);

	public static void SetWindowPassthrough(int HWND, bool Enable) {
		long CurrentWindowStyle = GetWindowLong(HWND, GWL_EXSTYLE);
		if (Enable) {
			CurrentWindowStyle &= ~WS_EX_TRANSPARENT;
			CurrentWindowStyle &= ~WS_EX_LAYERED;
		}
		else {
			CurrentWindowStyle |= WS_EX_TRANSPARENT;
			CurrentWindowStyle |= WS_EX_LAYERED;
		}
	    SetWindowLong(HWND, GWL_EXSTYLE, CurrentWindowStyle);
	}
	public static void SetWindowPassthrough(Window Window, bool Enable) {
		SetWindowPassthrough(GetWindowHandle(Window.GetWindowId()), Enable);
	}
	public static int GetWindowHandle(int WindowId) {
		return (int)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, WindowId);
	}
}