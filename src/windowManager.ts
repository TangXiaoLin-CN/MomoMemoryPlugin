import { exec } from 'child_process';
import { promisify } from 'util';
import { WindowInfo, WindowRect } from './types';

const execAsync = promisify(exec);

/**
 * PowerShell script to enumerate all visible windows
 */
const ENUM_WINDOWS_SCRIPT = `
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

public class WindowHelper {
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static List<object[]> GetAllWindows() {
        var windows = new List<object[]>();
        EnumWindows((hWnd, lParam) => {
            if (IsWindowVisible(hWnd)) {
                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                var title = sb.ToString();
                if (!string.IsNullOrWhiteSpace(title)) {
                    RECT rect;
                    GetWindowRect(hWnd, out rect);
                    uint processId;
                    GetWindowThreadProcessId(hWnd, out processId);
                    windows.Add(new object[] {
                        (long)hWnd,
                        title,
                        processId,
                        rect.Left,
                        rect.Top,
                        rect.Right - rect.Left,
                        rect.Bottom - rect.Top
                    });
                }
            }
            return true;
        }, IntPtr.Zero);
        return windows;
    }
}
"@

$windows = [WindowHelper]::GetAllWindows()
$result = @()
foreach ($w in $windows) {
    $proc = Get-Process -Id $w[2] -ErrorAction SilentlyContinue
    $procName = if ($proc) { $proc.ProcessName } else { "Unknown" }
    $result += [PSCustomObject]@{
        hwnd = $w[0]
        title = $w[1]
        processId = $w[2]
        processName = $procName
        x = $w[3]
        y = $w[4]
        width = $w[5]
        height = $w[6]
    }
}
$result | ConvertTo-Json -Compress
`;

/**
 * PowerShell script to get window rect by hwnd
 */
const GET_WINDOW_RECT_SCRIPT = (hwnd: number) => `
Add-Type @"
using System;
using System.Runtime.InteropServices;

public class WinAPI {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
"@

$hwnd = [IntPtr]${hwnd}
if ([WinAPI]::IsWindow($hwnd)) {
    $rect = New-Object WinAPI+RECT
    $null = [WinAPI]::GetWindowRect($hwnd, [ref]$rect)
    @{
        x = $rect.Left
        y = $rect.Top
        width = $rect.Right - $rect.Left
        height = $rect.Bottom - $rect.Top
        valid = $true
    } | ConvertTo-Json -Compress
} else {
    @{ valid = $false } | ConvertTo-Json -Compress
}
`;

/**
 * PowerShell script to get cursor position
 */
const GET_CURSOR_POS_SCRIPT = `
Add-Type @"
using System;
using System.Runtime.InteropServices;

public class CursorHelper {
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
    }
}
"@

$point = New-Object CursorHelper+POINT
$null = [CursorHelper]::GetCursorPos([ref]$point)
@{ x = $point.X; y = $point.Y } | ConvertTo-Json -Compress
`;

/**
 * PowerShell script to find window by title
 */
const FIND_WINDOW_SCRIPT = (title: string) => `
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class FindWindowHelper {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public static IntPtr FindByPartialTitle(string partialTitle) {
        IntPtr foundHwnd = IntPtr.Zero;
        EnumWindows((hWnd, lParam) => {
            if (IsWindowVisible(hWnd)) {
                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                var title = sb.ToString();
                if (title.Contains(partialTitle)) {
                    foundHwnd = hWnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        return foundHwnd;
    }
}
"@

$hwnd = [FindWindowHelper]::FindByPartialTitle("${title.replace(/"/g, '`"')}")
@{ hwnd = [long]$hwnd } | ConvertTo-Json -Compress
`;

export class WindowManager {
  private static instance: WindowManager;

  private constructor() {}

  public static getInstance(): WindowManager {
    if (!WindowManager.instance) {
      WindowManager.instance = new WindowManager();
    }
    return WindowManager.instance;
  }

  /**
   * Execute PowerShell script and return result
   */
  private async executePowerShell<T>(script: string): Promise<T> {
    // Add UTF-8 encoding to script
    const fullScript = `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\n${script}`;
    const encodedCommand = Buffer.from(fullScript, 'utf16le').toString('base64');
    const { stdout } = await execAsync(
      `powershell -NoProfile -NonInteractive -EncodedCommand ${encodedCommand}`,
      { maxBuffer: 10 * 1024 * 1024, encoding: 'utf8' }
    );
    return JSON.parse(stdout.trim());
  }

  /**
   * Get all visible windows
   */
  public async getAllWindows(): Promise<WindowInfo[]> {
    try {
      const result = await this.executePowerShell<any[]>(ENUM_WINDOWS_SCRIPT);

      if (!Array.isArray(result)) {
        return [];
      }

      return result.map((w) => ({
        hwnd: w.hwnd,
        title: w.title,
        processId: w.processId,
        processName: w.processName,
        rect: {
          x: w.x,
          y: w.y,
          width: w.width,
          height: w.height,
        },
        isVisible: true,
      }));
    } catch (error) {
      console.error('Failed to get windows:', error);
      return [];
    }
  }

  /**
   * Get window rect by hwnd
   */
  public async getWindowRect(hwnd: number): Promise<WindowRect | null> {
    try {
      const result = await this.executePowerShell<any>(
        GET_WINDOW_RECT_SCRIPT(hwnd)
      );

      if (!result.valid) {
        return null;
      }

      return {
        x: result.x,
        y: result.y,
        width: result.width,
        height: result.height,
      };
    } catch (error) {
      console.error('Failed to get window rect:', error);
      return null;
    }
  }

  /**
   * Find window by partial title
   */
  public async findWindowByTitle(partialTitle: string): Promise<number> {
    try {
      const result = await this.executePowerShell<{ hwnd: number }>(
        FIND_WINDOW_SCRIPT(partialTitle)
      );
      return result.hwnd;
    } catch (error) {
      console.error('Failed to find window:', error);
      return 0;
    }
  }

  /**
   * Get current cursor position
   */
  public async getCursorPosition(): Promise<{ x: number; y: number }> {
    try {
      return await this.executePowerShell<{ x: number; y: number }>(
        GET_CURSOR_POS_SCRIPT
      );
    } catch (error) {
      console.error('Failed to get cursor position:', error);
      return { x: 0, y: 0 };
    }
  }

  /**
   * Check if window is still valid
   */
  public async isWindowValid(hwnd: number): Promise<boolean> {
    const rect = await this.getWindowRect(hwnd);
    return rect !== null;
  }
}
