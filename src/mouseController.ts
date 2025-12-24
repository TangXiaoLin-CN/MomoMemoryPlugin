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
    [DllImport("user32.dll")]
    public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr ChildWindowFromPoint(IntPtr hWndParent, POINT Point);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

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
    public const uint MK_LBUTTON = 0x0001;
    public const uint MK_RBUTTON = 0x0002;
    public const uint GA_ROOT = 2;

    public static IntPtr MakeLParam(int x, int y) {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }

    public static void BackgroundClick(IntPtr parentHwnd, int relX, int relY, bool rightClick = false) {
        // Find the child window at the click point
        POINT pt = new POINT(relX, relY);
        IntPtr targetHwnd = ChildWindowFromPoint(parentHwnd, pt);

        if (targetHwnd == IntPtr.Zero) {
            targetHwnd = parentHwnd;
        }

        // Convert coordinates to target window's client coordinates
        POINT screenPt = new POINT(relX, relY);
        ClientToScreen(parentHwnd, ref screenPt);
        ScreenToClient(targetHwnd, ref screenPt);

        IntPtr lParam = MakeLParam(screenPt.X, screenPt.Y);
        IntPtr wParam = rightClick ? (IntPtr)MK_RBUTTON : (IntPtr)MK_LBUTTON;

        if (rightClick) {
            PostMessage(targetHwnd, WM_RBUTTONDOWN, wParam, lParam);
            System.Threading.Thread.Sleep(50);
            PostMessage(targetHwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
        } else {
            PostMessage(targetHwnd, WM_LBUTTONDOWN, wParam, lParam);
            System.Threading.Thread.Sleep(50);
            PostMessage(targetHwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        }
    }
}
"@

$hwnd = [IntPtr]${hwnd}
[BackgroundClicker]::BackgroundClick($hwnd, ${x}, ${y}, ${button === 'right' ? '$true' : '$false'})
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
