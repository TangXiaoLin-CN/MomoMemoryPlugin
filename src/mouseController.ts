import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

/**
 * PowerShell script template for mouse click
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
   * Check if robotjs is available
   */
  public isRobotjsAvailable(): boolean {
    return this.useRobotjs;
  }
}
