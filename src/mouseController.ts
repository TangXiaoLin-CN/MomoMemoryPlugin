import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

/**
 * PowerShell script for background click (sends message to window without moving mouse)
 */
const BACKGROUND_CLICK_SCRIPT = (hwnd: number, x: number, y: number, button: 'left' | 'right' = 'left') => `
Add-Type @"
using System;
using System.Runtime.InteropServices;

public class BackgroundClicker {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr ChildWindowFromPointEx(IntPtr hWndParent, POINT pt, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint MK_LBUTTON = 0x0001;
    public const uint MK_RBUTTON = 0x0002;
    public const uint CWP_ALL = 0x0000;
    public const uint CWP_SKIPDISABLED = 0x0002;
    public const uint CWP_SKIPINVISIBLE = 0x0001;

    public static IntPtr MakeLParam(int x, int y) {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }

    public static void BackgroundClick(IntPtr parentHwnd, int clientX, int clientY, bool rightClick = false) {
        // Try to find the deepest child window at this point
        POINT pt = new POINT(clientX, clientY);
        IntPtr targetHwnd = ChildWindowFromPointEx(parentHwnd, pt, CWP_SKIPINVISIBLE);

        int targetX = clientX;
        int targetY = clientY;

        // If we found a child window, convert coordinates
        if (targetHwnd != IntPtr.Zero && targetHwnd != parentHwnd) {
            // Convert from parent client to screen
            POINT screenPt = new POINT(clientX, clientY);
            ClientToScreen(parentHwnd, ref screenPt);
            // Convert from screen to child client
            ScreenToClient(targetHwnd, ref screenPt);
            targetX = screenPt.X;
            targetY = screenPt.Y;
        } else {
            targetHwnd = parentHwnd;
        }

        IntPtr lParam = MakeLParam(targetX, targetY);

        // Send mouse move first
        PostMessage(targetHwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
        System.Threading.Thread.Sleep(10);

        if (rightClick) {
            PostMessage(targetHwnd, WM_RBUTTONDOWN, (IntPtr)MK_RBUTTON, lParam);
            System.Threading.Thread.Sleep(30);
            PostMessage(targetHwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
        } else {
            PostMessage(targetHwnd, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, lParam);
            System.Threading.Thread.Sleep(30);
            PostMessage(targetHwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        }
    }

    // Alternative: click directly to parent window without child lookup
    public static void DirectClick(IntPtr hwnd, int clientX, int clientY, bool rightClick = false) {
        IntPtr lParam = MakeLParam(clientX, clientY);

        PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
        System.Threading.Thread.Sleep(10);

        if (rightClick) {
            PostMessage(hwnd, WM_RBUTTONDOWN, (IntPtr)MK_RBUTTON, lParam);
            System.Threading.Thread.Sleep(30);
            PostMessage(hwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
        } else {
            PostMessage(hwnd, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, lParam);
            System.Threading.Thread.Sleep(30);
            PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        }
    }
}
"@

$hwnd = [IntPtr]${hwnd}
# Try direct click first (simpler, works for many apps)
[BackgroundClicker]::DirectClick($hwnd, ${x}, ${y}, ${button === 'right' ? '$true' : '$false'})
@{ success = $true } | ConvertTo-Json -Compress
`;

/**
 * PowerShell script template for mouse click (moves mouse)
 */
const MOUSE_CLICK_SCRIPT = (x: number, y: number, button: 'left' | 'right' = 'left') => `
Add-Type @"
using System;
using System.Runtime.InteropServices;

public class MouseHelper {
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    public const uint MOUSEEVENTF_MOVE = 0x0001;

    public static void Click(int x, int y, bool rightClick = false) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(50);
        if (rightClick) {
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
        } else {
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }
    }

    public static void DoubleClick(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        System.Threading.Thread.Sleep(100);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    public static void MoveTo(int x, int y) {
        SetCursorPos(x, y);
    }
}
"@

[MouseHelper]::${button === 'right' ? 'Click(' + x + ', ' + y + ', $true)' : 'Click(' + x + ', ' + y + ')'}
@{ success = $true } | ConvertTo-Json -Compress
`;

const MOUSE_DOUBLE_CLICK_SCRIPT = (x: number, y: number) => `
Add-Type @"
using System;
using System.Runtime.InteropServices;

public class MouseHelper {
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;

    public static void DoubleClick(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        System.Threading.Thread.Sleep(100);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }
}
"@

[MouseHelper]::DoubleClick(${x}, ${y})
@{ success = $true } | ConvertTo-Json -Compress
`;

const MOUSE_MOVE_SCRIPT = (x: number, y: number) => `
Add-Type @"
using System;
using System.Runtime.InteropServices;

public class MouseHelper {
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);
}
"@

[MouseHelper]::SetCursorPos(${x}, ${y})
@{ success = $true } | ConvertTo-Json -Compress
`;

export type MouseButton = 'left' | 'right';

export class MouseController {
  private static instance: MouseController;
  private robotjs: any = null;
  private useRobotjs: boolean = false;

  private constructor() {
    this.tryLoadRobotjs();
  }

  public static getInstance(): MouseController {
    if (!MouseController.instance) {
      MouseController.instance = new MouseController();
    }
    return MouseController.instance;
  }

  /**
   * Try to load robotjs module
   */
  private tryLoadRobotjs(): void {
    try {
      this.robotjs = require('robotjs');
      this.useRobotjs = true;
      console.log('MouseController: Using robotjs');
    } catch (error) {
      console.log('MouseController: robotjs not available, using PowerShell fallback');
      this.useRobotjs = false;
    }
  }

  /**
   * Execute PowerShell script
   */
  private async executePowerShell(script: string): Promise<void> {
    const encodedCommand = Buffer.from(script, 'utf16le').toString('base64');
    await execAsync(
      `powershell -NoProfile -NonInteractive -EncodedCommand ${encodedCommand}`
    );
  }

  /**
   * Click at absolute screen coordinates
   */
  public async click(x: number, y: number, button: MouseButton = 'left'): Promise<void> {
    if (this.useRobotjs && this.robotjs) {
      this.robotjs.moveMouse(x, y);
      this.robotjs.mouseClick(button);
    } else {
      await this.executePowerShell(MOUSE_CLICK_SCRIPT(x, y, button));
    }
  }

  /**
   * Double click at absolute screen coordinates
   */
  public async doubleClick(x: number, y: number): Promise<void> {
    if (this.useRobotjs && this.robotjs) {
      this.robotjs.moveMouse(x, y);
      this.robotjs.mouseClick('left', true);
    } else {
      await this.executePowerShell(MOUSE_DOUBLE_CLICK_SCRIPT(x, y));
    }
  }

  /**
   * Move mouse to absolute screen coordinates
   */
  public async moveTo(x: number, y: number): Promise<void> {
    if (this.useRobotjs && this.robotjs) {
      this.robotjs.moveMouse(x, y);
    } else {
      await this.executePowerShell(MOUSE_MOVE_SCRIPT(x, y));
    }
  }

  /**
   * Click at coordinates relative to a window
   */
  public async clickRelative(
    windowX: number,
    windowY: number,
    relativeX: number,
    relativeY: number,
    button: MouseButton = 'left'
  ): Promise<void> {
    const absoluteX = windowX + relativeX;
    const absoluteY = windowY + relativeY;
    await this.click(absoluteX, absoluteY, button);
  }

  /**
   * Background click - sends click message to window without moving mouse
   */
  public async backgroundClick(
    hwnd: number,
    relativeX: number,
    relativeY: number,
    button: MouseButton = 'left'
  ): Promise<void> {
    await this.executePowerShell(BACKGROUND_CLICK_SCRIPT(hwnd, relativeX, relativeY, button));
  }

  /**
   * Check if robotjs is available
   */
  public isRobotjsAvailable(): boolean {
    return this.useRobotjs;
  }
}
