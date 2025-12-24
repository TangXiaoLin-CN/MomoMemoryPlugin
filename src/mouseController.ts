import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

/**
 * PowerShell script for background click (sends message to window without moving mouse)
 */
const BACKGROUND_CLICK_SCRIPT = (hwnd: number, x: number, y: number, button: 'left' | 'right' = 'left') => {
  const uid = Date.now();
  return `
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class BC${uid} {
    [DllImport("user32.dll")]
    public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static IntPtr MakeLParam(int x, int y) {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }

    public static void Click(IntPtr hwnd, int clientX, int clientY, bool rightClick) {
        IntPtr lParam = MakeLParam(clientX, clientY);

        // Mouse move
        PostMessage(hwnd, 0x0200, IntPtr.Zero, lParam);
        System.Threading.Thread.Sleep(10);

        if (rightClick) {
            PostMessage(hwnd, 0x0204, (IntPtr)0x0002, lParam);
            System.Threading.Thread.Sleep(30);
            PostMessage(hwnd, 0x0205, IntPtr.Zero, lParam);
        } else {
            PostMessage(hwnd, 0x0201, (IntPtr)0x0001, lParam);
            System.Threading.Thread.Sleep(30);
            PostMessage(hwnd, 0x0202, IntPtr.Zero, lParam);
        }
    }
}
"@

[BC${uid}]::Click([IntPtr]${hwnd}, ${x}, ${y}, ${button === 'right' ? '$true' : '$false'})
@{ success = $true } | ConvertTo-Json -Compress
`;
};

/**
 * PowerShell script template for mouse click (moves mouse)
 */
const MOUSE_CLICK_SCRIPT = (x: number, y: number, button: 'left' | 'right' = 'left') => {
  const uid = Date.now();
  const downFlag = button === 'right' ? '0x0008' : '0x0002';
  const upFlag = button === 'right' ? '0x0010' : '0x0004';
  return `
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class MC${uid} {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern IntPtr GetMessageExtraInfo();

    public const int INPUT_MOUSE = 0;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT {
        public int type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public static void Click(int x, int y, bool rightClick) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(50);

        uint downFlag = rightClick ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
        uint upFlag = rightClick ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;

        INPUT[] inputs = new INPUT[2];

        inputs[0].type = INPUT_MOUSE;
        inputs[0].mi.dwFlags = downFlag;
        inputs[0].mi.dwExtraInfo = GetMessageExtraInfo();

        inputs[1].type = INPUT_MOUSE;
        inputs[1].mi.dwFlags = upFlag;
        inputs[1].mi.dwExtraInfo = GetMessageExtraInfo();

        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}
"@

[MC${uid}]::Click(${x}, ${y}, ${button === 'right' ? '$true' : '$false'})
@{ success = $true } | ConvertTo-Json -Compress
`;
};

const MOUSE_DOUBLE_CLICK_SCRIPT = (x: number, y: number) => {
  const uid = Date.now();
  return `
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class MDC${uid} {
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);
}
"@

$null = [MDC${uid}]::SetCursorPos(${x}, ${y})
Start-Sleep -Milliseconds 50
[MDC${uid}]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
[MDC${uid}]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep -Milliseconds 100
[MDC${uid}]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
[MDC${uid}]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
@{ success = $true } | ConvertTo-Json -Compress
`;
};

const MOUSE_MOVE_SCRIPT = (x: number, y: number) => {
  const uid = Date.now();
  return `
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class MM${uid} {
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);
}
"@

$null = [MM${uid}]::SetCursorPos(${x}, ${y})
@{ success = $true } | ConvertTo-Json -Compress
`;
};

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
