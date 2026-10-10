param([Parameter(Mandatory)][int]$OwnedPid, [string]$Screenshot, [int]$ClickX = -1, [int]$ClickY = -1)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class OwnedGameWindow {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
}
"@
$process = Get-Process -Id $OwnedPid
if ($process.ProcessName -ne 'Sea Power') { throw 'Only the owned game window is allowed' }
$process.Refresh()
$window = $process.MainWindowHandle
[OwnedGameWindow]::SetForegroundWindow($window) | Out-Null
Start-Sleep -Milliseconds 200
if ([OwnedGameWindow]::GetForegroundWindow() -ne $window) { throw 'Game window is not foreground' }
$bounds = [OwnedGameWindow+Rect]::new()
[OwnedGameWindow]::GetWindowRect($window, [ref]$bounds) | Out-Null
if ($ClickX -ge 0 -and $ClickY -ge 0) {
    [OwnedGameWindow]::SetCursorPos($bounds.Left + $ClickX, $bounds.Top + $ClickY) | Out-Null
    [OwnedGameWindow]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [OwnedGameWindow]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}
if ($Screenshot) {
    $bitmap = [Drawing.Bitmap]::new($bounds.Right - $bounds.Left, $bounds.Bottom - $bounds.Top)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $bitmap.Size)
    $bitmap.Save($Screenshot)
    $graphics.Dispose()
    $bitmap.Dispose()
}
