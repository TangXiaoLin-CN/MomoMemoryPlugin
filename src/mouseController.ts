import { exec } from 'child_process';
import { promisify } from 'util';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';

const execAsync = promisify(exec);

/**
 * PowerShell script for background click using PostMessage
 */
const BACKGROUND_POST_MESSAGE_SCRIPT = (hwnd: number, x: number, y: number, button: 'left' | 'right' = 'left') => {
  const uid = Date.now();
  return `
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class BPM${uid} {
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static IntPtr MakeLParam(int x, int y) {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }

    public static void Click(IntPtr hwnd, int clientX, int clientY, bool rightClick) {
        IntPtr lParam = MakeLParam(clientX, clientY);

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

[BPM${uid}]::Click([IntPtr]${hwnd}, ${x}, ${y}, ${button === 'right' ? '$true' : '$false'})
@{ success = $true } | ConvertTo-Json -Compress
`;
};

/**
 * PowerShell script for background click using SendMessage (synchronous)
 */
const BACKGROUND_SEND_MESSAGE_SCRIPT = (hwnd: number, x: number, y: number, button: 'left' | 'right' = 'left') => {
  const uid = Date.now();
  return `
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class BSM${uid} {
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static IntPtr MakeLParam(int x, int y) {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }

    public static void Click(IntPtr hwnd, int clientX, int clientY, bool rightClick) {
        IntPtr lParam = MakeLParam(clientX, clientY);

        SendMessage(hwnd, 0x0200, IntPtr.Zero, lParam);
        System.Threading.Thread.Sleep(10);

        if (rightClick) {
            SendMessage(hwnd, 0x0204, (IntPtr)0x0002, lParam);
            System.Threading.Thread.Sleep(30);
            SendMessage(hwnd, 0x0205, IntPtr.Zero, lParam);
        } else {
            SendMessage(hwnd, 0x0201, (IntPtr)0x0001, lParam);
            System.Threading.Thread.Sleep(30);
            SendMessage(hwnd, 0x0202, IntPtr.Zero, lParam);
        }
    }
}
"@

[BSM${uid}]::Click([IntPtr]${hwnd}, ${x}, ${y}, ${button === 'right' ? '$true' : '$false'})
@{ success = $true } | ConvertTo-Json -Compress
`;
};

/**
 * PowerShell script for fast foreground click (saves/restores cursor position)
 * This moves cursor very quickly and restores it - appears like background click
 */
const FAST_FOREGROUND_CLICK_SCRIPT = (hwnd: number, windowX: number, windowY: number, relativeX: number, relativeY: number, button: 'left' | 'right' = 'left') => {
  const uid = Date.now();
  const absoluteX = windowX + relativeX;
  const absoluteY = windowY + relativeY;
  return `
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class FFC${uid} {
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
    }

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    public static void Click(int targetX, int targetY, bool rightClick) {
        POINT originalPos;
        GetCursorPos(out originalPos);

        SetCursorPos(targetX, targetY);
        System.Threading.Thread.Sleep(5);

        if (rightClick) {
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
        } else {
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        }

        System.Threading.Thread.Sleep(5);
        SetCursorPos(originalPos.X, originalPos.Y);
    }
}
"@

[FFC${uid}]::Click(${absoluteX}, ${absoluteY}, ${button === 'right' ? '$true' : '$false'})
@{ success = $true } | ConvertTo-Json -Compress
`;
};

/**
 * PowerShell script for background click with window activation messages
 */
const BACKGROUND_ACTIVATE_CLICK_SCRIPT = (hwnd: number, x: number, y: number, button: 'left' | 'right' = 'left') => {
  const uid = Date.now();
  return `
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class BAC${uid} {
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public const uint WM_MOUSEACTIVATE = 0x0021;
    public const uint WM_ACTIVATE = 0x0006;
    public const uint WM_SETFOCUS = 0x0007;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint MA_ACTIVATE = 1;
    public const uint WA_ACTIVE = 1;

    public static IntPtr MakeLParam(int x, int y) {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }

    public static void Click(IntPtr hwnd, int clientX, int clientY, bool rightClick) {
        IntPtr lParam = MakeLParam(clientX, clientY);

        // Send activation messages
        PostMessage(hwnd, WM_MOUSEACTIVATE, hwnd, (IntPtr)((WM_LBUTTONDOWN << 16) | 1));
        System.Threading.Thread.Sleep(5);
        PostMessage(hwnd, WM_ACTIVATE, (IntPtr)WA_ACTIVE, IntPtr.Zero);
        System.Threading.Thread.Sleep(5);
        PostMessage(hwnd, WM_SETFOCUS, IntPtr.Zero, IntPtr.Zero);
        System.Threading.Thread.Sleep(5);

        // Mouse move
        PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
        System.Threading.Thread.Sleep(10);

        // Click
        if (rightClick) {
            PostMessage(hwnd, WM_RBUTTONDOWN, (IntPtr)0x0002, lParam);
            System.Threading.Thread.Sleep(30);
            PostMessage(hwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
        } else {
            PostMessage(hwnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
            System.Threading.Thread.Sleep(30);
            PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        }
    }
}
"@

[BAC${uid}]::Click([IntPtr]${hwnd}, ${x}, ${y}, ${button === 'right' ? '$true' : '$false'})
@{ success = $true } | ConvertTo-Json -Compress
`;
};

/**
 * PowerShell script for child window click - finds child at coordinate and sends message to it
 */
const CHILD_WINDOW_CLICK_SCRIPT = (hwnd: number, x: number, y: number, windowX: number, windowY: number, button: 'left' | 'right' = 'left') => {
  const uid = Date.now();
  const absoluteX = windowX + x;
  const absoluteY = windowY + y;
  return `
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class CWC${uid} {
    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    public static extern IntPtr ChildWindowFromPointEx(IntPtr hWndParent, POINT Point, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr GetParent(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
    }

    public const uint CWP_ALL = 0x0000;
    public const uint CWP_SKIPDISABLED = 0x0002;
    public const uint CWP_SKIPINVISIBLE = 0x0001;
    public const uint CWP_SKIPTRANSPARENT = 0x0004;

    public static IntPtr MakeLParam(int x, int y) {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }

    public static void Click(IntPtr parentHwnd, int screenX, int screenY, int relX, int relY, bool rightClick) {
        POINT pt = new POINT { X = relX, Y = relY };

        // Try to find child window at this point
        IntPtr childHwnd = ChildWindowFromPointEx(parentHwnd, pt, CWP_SKIPINVISIBLE | CWP_SKIPDISABLED);

        IntPtr targetHwnd = (childHwnd != IntPtr.Zero && childHwnd != parentHwnd) ? childHwnd : parentHwnd;

        // Convert to client coordinates of target window
        POINT clientPt = new POINT { X = screenX, Y = screenY };
        ScreenToClient(targetHwnd, ref clientPt);

        IntPtr lParam = MakeLParam(clientPt.X, clientPt.Y);

        PostMessage(targetHwnd, 0x0200, IntPtr.Zero, lParam);
        System.Threading.Thread.Sleep(10);

        if (rightClick) {
            PostMessage(targetHwnd, 0x0204, (IntPtr)0x0002, lParam);
            System.Threading.Thread.Sleep(30);
            PostMessage(targetHwnd, 0x0205, IntPtr.Zero, lParam);
        } else {
            PostMessage(targetHwnd, 0x0201, (IntPtr)0x0001, lParam);
            System.Threading.Thread.Sleep(30);
            PostMessage(targetHwnd, 0x0202, IntPtr.Zero, lParam);
        }
    }
}
"@

[CWC${uid}]::Click([IntPtr]${hwnd}, ${absoluteX}, ${absoluteY}, ${x}, ${y}, ${button === 'right' ? '$true' : '$false'})
@{ success = $true } | ConvertTo-Json -Compress
`;
};

/**
 * PowerShell script for quick foreground switch - brings window to front, clicks, restores
 */
const QUICK_FOREGROUND_SWITCH_SCRIPT = (hwnd: number, x: number, y: number, windowX: number, windowY: number, button: 'left' | 'right' = 'left') => {
  const uid = Date.now();
  const absoluteX = windowX + x;
  const absoluteY = windowY + y;
  return `
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class QFS${uid} {
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
    }

    public const int SW_SHOW = 5;
    public const int SW_RESTORE = 9;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    public static void Click(IntPtr targetHwnd, int targetX, int targetY, bool rightClick) {
        IntPtr prevForeground = GetForegroundWindow();
        POINT originalPos;
        GetCursorPos(out originalPos);

        // Attach to target thread to allow SetForegroundWindow
        uint targetThreadId = GetWindowThreadProcessId(targetHwnd, out _);
        uint currentThreadId = GetCurrentThreadId();

        bool attached = false;
        if (targetThreadId != currentThreadId) {
            attached = AttachThreadInput(currentThreadId, targetThreadId, true);
        }

        try {
            // Bring target window to front
            ShowWindow(targetHwnd, SW_RESTORE);
            SetForegroundWindow(targetHwnd);
            System.Threading.Thread.Sleep(30);

            // Move and click
            SetCursorPos(targetX, targetY);
            System.Threading.Thread.Sleep(10);

            if (rightClick) {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
            } else {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            }

            System.Threading.Thread.Sleep(30);

            // Restore previous foreground window
            if (prevForeground != IntPtr.Zero && prevForeground != targetHwnd) {
                SetForegroundWindow(prevForeground);
            }

            // Restore cursor position
            SetCursorPos(originalPos.X, originalPos.Y);
        }
        finally {
            if (attached) {
                AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        }
    }
}
"@

[QFS${uid}]::Click([IntPtr]${hwnd}, ${absoluteX}, ${absoluteY}, ${button === 'right' ? '$true' : '$false'})
@{ success = $true } | ConvertTo-Json -Compress
`;
};

/**
 * AutoHotkey v2 script for ControlClick (most reliable background click)
 */
const generateAHKScript = (hwnd: number, x: number, y: number, button: 'left' | 'right' = 'left') => {
  const clickButton = button === 'right' ? 'Right' : 'Left';
  return `#Requires AutoHotkey v2.0
#SingleInstance Force

hwnd := ${hwnd}
x := ${x}
y := ${y}

try {
    ControlClick("x" x " y" y, "ahk_id " hwnd,, "${clickButton}", 1, "NA")
} catch as e {
    ; Fallback: try clicking the window directly
    PostMessage(0x0201, 0, (y << 16) | (x & 0xFFFF),, "ahk_id " hwnd)
    Sleep(30)
    PostMessage(0x0202, 0, (y << 16) | (x & 0xFFFF),, "ahk_id " hwnd)
}

ExitApp()
`;
};

/**
 * AutoHotkey v2 script using PostMessage directly
 */
const generateAHKPostMessageScript = (hwnd: number, x: number, y: number, button: 'left' | 'right' = 'left') => {
  const WM_LBUTTONDOWN = '0x0201';
  const WM_LBUTTONUP = '0x0202';
  const WM_RBUTTONDOWN = '0x0204';
  const WM_RBUTTONUP = '0x0205';
  const downMsg = button === 'right' ? WM_RBUTTONDOWN : WM_LBUTTONDOWN;
  const upMsg = button === 'right' ? WM_RBUTTONUP : WM_LBUTTONUP;
  const wParam = button === 'right' ? '0x0002' : '0x0001';

  return `#Requires AutoHotkey v2.0
#SingleInstance Force

hwnd := ${hwnd}
x := ${x}
y := ${y}
lParam := (y << 16) | (x & 0xFFFF)

; Send mouse messages directly
PostMessage(0x0200, 0, lParam,, "ahk_id " hwnd)  ; WM_MOUSEMOVE
Sleep(10)
PostMessage(${downMsg}, ${wParam}, lParam,, "ahk_id " hwnd)
Sleep(30)
PostMessage(${upMsg}, 0, lParam,, "ahk_id " hwnd)

ExitApp()
`;
};

/**
 * AutoHotkey v2 script - make window transparent, activate, click, restore
 * This is the "stealth" mode - window becomes invisible while clicking
 */
const generateAHKStealthScript = (hwnd: number, x: number, y: number, button: 'left' | 'right' = 'left') => {
  const clickButton = button === 'right' ? 'Right' : 'Left';
  return `#Requires AutoHotkey v2.0
#SingleInstance Force

hwnd := ${hwnd}
clickX := ${x}
clickY := ${y}
winTitle := "ahk_id " hwnd

; Save current foreground window
prevWin := WinGetID("A")

; Make target window fully transparent (invisible)
try {
    WinSetTransparent(0, winTitle)
} catch {
}

Sleep(30)

; Activate the window (it's invisible so user won't see)
try {
    WinActivate(winTitle)
    WinWaitActive(winTitle,, 1)
} catch {
    ; Restore transparency and exit
    try {
        WinSetTransparent("Off", winTitle)
    }
    ExitApp()
}

Sleep(30)

; Click using ControlClick (doesn't move mouse)
clickSuccess := false
try {
    ControlClick("x" clickX " y" clickY, winTitle,, "${clickButton}", 1, "NA")
    clickSuccess := true
} catch {
}

; If ControlClick failed, try direct mouse simulation
if (!clickSuccess) {
    try {
        PostMessage(0x0201, 0, (clickY << 16) | (clickX & 0xFFFF),, winTitle)
        Sleep(30)
        PostMessage(0x0202, 0, (clickY << 16) | (clickX & 0xFFFF),, winTitle)
    }
}

Sleep(30)

; Restore previous foreground window first
if (prevWin != hwnd && prevWin != 0) {
    try {
        WinActivate("ahk_id " prevWin)
        WinWaitActive("ahk_id " prevWin,, 0.5)
    } catch {
    }
}

Sleep(30)

; Restore window transparency (make visible again)
try {
    WinSetTransparent("Off", winTitle)
} catch {
}

ExitApp()
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

/**
 * Click mode types:
 * - foreground: Normal click, moves mouse cursor
 * - background_post: PostMessage (async), doesn't move mouse
 * - background_send: SendMessage (sync), doesn't move mouse
 * - background_activate: PostMessage with activation messages
 * - fast_foreground: Moves mouse quickly, clicks, restores position
 * - child_window: Finds child window at coordinate and sends message to it
 * - quick_switch: Brings window to front, clicks, restores previous window
 * - ahk_control: Uses AutoHotkey ControlClick (requires AHK installed)
 * - ahk_post: Uses AutoHotkey PostMessage
 * - ahk_stealth: Moves window offscreen, activates, clicks, restores (best for stubborn apps)
 */
export type ClickMode =
  | 'foreground'
  | 'background_post'
  | 'background_send'
  | 'background_activate'
  | 'fast_foreground'
  | 'child_window'
  | 'quick_switch'
  | 'ahk_control'
  | 'ahk_post'
  | 'ahk_stealth';

export class MouseController {
  private static instance: MouseController;
  private robotjs: any = null;
  private useRobotjs: boolean = false;
  private ahkPath: string | null = null;

  private constructor() {
    this.tryLoadRobotjs();
    this.findAutoHotkey();
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
   * Find AutoHotkey executable
   */
  private async findAutoHotkey(): Promise<void> {
    const possiblePaths = [
      'C:\\Program Files\\AutoHotkey\\v2\\AutoHotkey.exe',
      'C:\\Program Files\\AutoHotkey\\AutoHotkey.exe',
      'C:\\Program Files (x86)\\AutoHotkey\\AutoHotkey.exe',
      'D:\\software\\autoHotKey\\v2\\AutoHotkey.exe',
      'D:\\software\\autoHotKey\\AutoHotkey.exe',
      'D:\\software\\autoHotKey\\v2\\AutoHotkey64.exe',
      'D:\\software\\autoHotKey\\AutoHotkey64.exe',
      'D:\\software\\autoHotKey\\AutoHotkey32.exe',
      path.join(os.homedir(), 'scoop\\apps\\autohotkey\\current\\v2\\AutoHotkey.exe'),
      path.join(os.homedir(), 'scoop\\apps\\autohotkey\\current\\AutoHotkey.exe'),
    ];

    for (const p of possiblePaths) {
      if (fs.existsSync(p)) {
        this.ahkPath = p;
        console.log(`MouseController: Found AutoHotkey at ${p}`);
        return;
      }
    }

    // Try to find via where command
    try {
      const { stdout } = await execAsync('where AutoHotkey.exe');
      const firstPath = stdout.trim().split('\n')[0];
      if (firstPath && fs.existsSync(firstPath)) {
        this.ahkPath = firstPath;
        console.log(`MouseController: Found AutoHotkey at ${firstPath}`);
        return;
      }
    } catch {
      // Ignore
    }

    // Try to find AutoHotkey64.exe
    try {
      const { stdout } = await execAsync('where AutoHotkey64.exe');
      const firstPath = stdout.trim().split('\n')[0];
      if (firstPath && fs.existsSync(firstPath)) {
        this.ahkPath = firstPath;
        console.log(`MouseController: Found AutoHotkey at ${firstPath}`);
        return;
      }
    } catch {
      console.log('MouseController: AutoHotkey not found');
    }
  }

  /**
   * Set custom AutoHotkey path
   */
  public setAHKPath(ahkPath: string): void {
    if (fs.existsSync(ahkPath)) {
      this.ahkPath = ahkPath;
      console.log(`MouseController: Set AutoHotkey path to ${ahkPath}`);
    } else {
      console.error(`MouseController: AutoHotkey path not found: ${ahkPath}`);
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
   * Execute AutoHotkey script
   */
  private async executeAHK(script: string): Promise<void> {
    if (!this.ahkPath) {
      throw new Error('AutoHotkey not installed. Please install from https://www.autohotkey.com/');
    }

    const tempFile = path.join(os.tmpdir(), `momo_click_${Date.now()}.ahk`);
    fs.writeFileSync(tempFile, script, 'utf8');

    try {
      await execAsync(`"${this.ahkPath}" "${tempFile}"`);
    } finally {
      // Clean up temp file
      try {
        fs.unlinkSync(tempFile);
      } catch {}
    }
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
   * Background click using PostMessage (original method)
   */
  public async backgroundClickPost(
    hwnd: number,
    relativeX: number,
    relativeY: number,
    button: MouseButton = 'left'
  ): Promise<void> {
    await this.executePowerShell(BACKGROUND_POST_MESSAGE_SCRIPT(hwnd, relativeX, relativeY, button));
  }

  /**
   * Background click using SendMessage (synchronous)
   */
  public async backgroundClickSend(
    hwnd: number,
    relativeX: number,
    relativeY: number,
    button: MouseButton = 'left'
  ): Promise<void> {
    await this.executePowerShell(BACKGROUND_SEND_MESSAGE_SCRIPT(hwnd, relativeX, relativeY, button));
  }

  /**
   * Background click with activation messages
   */
  public async backgroundClickActivate(
    hwnd: number,
    relativeX: number,
    relativeY: number,
    button: MouseButton = 'left'
  ): Promise<void> {
    await this.executePowerShell(BACKGROUND_ACTIVATE_CLICK_SCRIPT(hwnd, relativeX, relativeY, button));
  }

  /**
   * Fast foreground click - moves cursor quickly, clicks, restores position
   */
  public async fastForegroundClick(
    hwnd: number,
    windowX: number,
    windowY: number,
    relativeX: number,
    relativeY: number,
    button: MouseButton = 'left'
  ): Promise<void> {
    await this.executePowerShell(FAST_FOREGROUND_CLICK_SCRIPT(hwnd, windowX, windowY, relativeX, relativeY, button));
  }

  /**
   * Child window click - finds child control at coordinate
   */
  public async childWindowClick(
    hwnd: number,
    relativeX: number,
    relativeY: number,
    windowX: number,
    windowY: number,
    button: MouseButton = 'left'
  ): Promise<void> {
    await this.executePowerShell(CHILD_WINDOW_CLICK_SCRIPT(hwnd, relativeX, relativeY, windowX, windowY, button));
  }

  /**
   * Quick foreground switch - brings window to front, clicks, restores
   */
  public async quickSwitchClick(
    hwnd: number,
    relativeX: number,
    relativeY: number,
    windowX: number,
    windowY: number,
    button: MouseButton = 'left'
  ): Promise<void> {
    await this.executePowerShell(QUICK_FOREGROUND_SWITCH_SCRIPT(hwnd, relativeX, relativeY, windowX, windowY, button));
  }

  /**
   * AutoHotkey ControlClick - most reliable background click
   */
  public async ahkControlClick(
    hwnd: number,
    relativeX: number,
    relativeY: number,
    button: MouseButton = 'left'
  ): Promise<void> {
    await this.executeAHK(generateAHKScript(hwnd, relativeX, relativeY, button));
  }

  /**
   * AutoHotkey PostMessage click
   */
  public async ahkPostClick(
    hwnd: number,
    relativeX: number,
    relativeY: number,
    button: MouseButton = 'left'
  ): Promise<void> {
    await this.executeAHK(generateAHKPostMessageScript(hwnd, relativeX, relativeY, button));
  }

  /**
   * AutoHotkey Stealth click - moves window offscreen, activates, clicks, restores
   * Best for stubborn apps that only respond when in foreground
   */
  public async ahkStealthClick(
    hwnd: number,
    relativeX: number,
    relativeY: number,
    button: MouseButton = 'left'
  ): Promise<void> {
    await this.executeAHK(generateAHKStealthScript(hwnd, relativeX, relativeY, button));
  }

  /**
   * Universal background click - tries different methods based on mode
   */
  public async backgroundClick(
    hwnd: number,
    relativeX: number,
    relativeY: number,
    button: MouseButton = 'left',
    mode: ClickMode = 'background_post',
    windowX: number = 0,
    windowY: number = 0
  ): Promise<void> {
    switch (mode) {
      case 'background_post':
        await this.backgroundClickPost(hwnd, relativeX, relativeY, button);
        break;
      case 'background_send':
        await this.backgroundClickSend(hwnd, relativeX, relativeY, button);
        break;
      case 'background_activate':
        await this.backgroundClickActivate(hwnd, relativeX, relativeY, button);
        break;
      case 'fast_foreground':
        await this.fastForegroundClick(hwnd, windowX, windowY, relativeX, relativeY, button);
        break;
      case 'child_window':
        await this.childWindowClick(hwnd, relativeX, relativeY, windowX, windowY, button);
        break;
      case 'quick_switch':
        await this.quickSwitchClick(hwnd, relativeX, relativeY, windowX, windowY, button);
        break;
      case 'ahk_control':
        await this.ahkControlClick(hwnd, relativeX, relativeY, button);
        break;
      case 'ahk_post':
        await this.ahkPostClick(hwnd, relativeX, relativeY, button);
        break;
      case 'ahk_stealth':
        await this.ahkStealthClick(hwnd, relativeX, relativeY, button);
        break;
      default:
        await this.backgroundClickPost(hwnd, relativeX, relativeY, button);
    }
  }

  /**
   * Check if robotjs is available
   */
  public isRobotjsAvailable(): boolean {
    return this.useRobotjs;
  }

  /**
   * Check if AutoHotkey is available
   */
  public isAHKAvailable(): boolean {
    return this.ahkPath !== null;
  }

  /**
   * Get AutoHotkey path
   */
  public getAHKPath(): string | null {
    return this.ahkPath;
  }
}
