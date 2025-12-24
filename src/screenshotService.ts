import { exec } from 'child_process';
import { promisify } from 'util';
import * as path from 'path';
import * as os from 'os';
import * as fs from 'fs';
import { WindowRect, ScreenshotResult, OCRRegion } from './types';

const execAsync = promisify(exec);

/**
 * PowerShell script to capture a region of the screen
 */
const CAPTURE_REGION_SCRIPT = (x: number, y: number, width: number, height: number, outputPath: string) => `
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$bounds = New-Object System.Drawing.Rectangle(${x}, ${y}, ${width}, ${height})
$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bitmap.Save("${outputPath.replace(/\\/g, '\\\\')}")
$graphics.Dispose()
$bitmap.Dispose()
@{ success = $true; path = "${outputPath.replace(/\\/g, '\\\\')}" } | ConvertTo-Json -Compress
`;

/**
 * PowerShell script to capture a specific window
 */
const CAPTURE_WINDOW_SCRIPT = (hwnd: number, outputPath: string) => `
Add-Type @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;

public class WindowCapture {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSource, int xSrc, int ySrc, int RasterOp);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static Bitmap CaptureWindow(IntPtr hwnd) {
        RECT rect;
        GetWindowRect(hwnd, out rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0) return null;

        Bitmap bmp = new Bitmap(width, height);
        using (Graphics g = Graphics.FromImage(bmp)) {
            IntPtr hdcBitmap = g.GetHdc();
            PrintWindow(hwnd, hdcBitmap, 2);
            g.ReleaseHdc(hdcBitmap);
        }
        return bmp;
    }
}
"@

$hwnd = [IntPtr]${hwnd}
$bitmap = [WindowCapture]::CaptureWindow($hwnd)
if ($bitmap -ne $null) {
    $bitmap.Save("${outputPath.replace(/\\/g, '\\\\')}")
    $bitmap.Dispose()
    @{ success = $true; path = "${outputPath.replace(/\\/g, '\\\\')}" } | ConvertTo-Json -Compress
} else {
    @{ success = $false; error = "Failed to capture window" } | ConvertTo-Json -Compress
}
`;

export class ScreenshotService {
  private static instance: ScreenshotService;
  private tempDir: string;

  private constructor() {
    this.tempDir = path.join(os.tmpdir(), 'momo-memory-plugin');
    if (!fs.existsSync(this.tempDir)) {
      fs.mkdirSync(this.tempDir, { recursive: true });
    }
  }

  public static getInstance(): ScreenshotService {
    if (!ScreenshotService.instance) {
      ScreenshotService.instance = new ScreenshotService();
    }
    return ScreenshotService.instance;
  }

  /**
   * Execute PowerShell script
   */
  private async executePowerShell<T>(script: string): Promise<T> {
    const encodedCommand = Buffer.from(script, 'utf16le').toString('base64');
    const { stdout } = await execAsync(
      `powershell -NoProfile -NonInteractive -EncodedCommand ${encodedCommand}`,
      { maxBuffer: 10 * 1024 * 1024 }
    );
    return JSON.parse(stdout.trim());
  }

  /**
   * Generate a unique temp file path
   */
  private getTempFilePath(): string {
    return path.join(this.tempDir, `screenshot_${Date.now()}.png`);
  }

  /**
   * Capture a region of the screen
   */
  public async captureRegion(
    x: number,
    y: number,
    width: number,
    height: number
  ): Promise<string | null> {
    const outputPath = this.getTempFilePath();
    try {
      const result = await this.executePowerShell<{ success: boolean; path?: string; error?: string }>(
        CAPTURE_REGION_SCRIPT(x, y, width, height, outputPath)
      );

      if (result.success) {
        return result.path || outputPath;
      }
      console.error('Failed to capture region:', result.error);
      return null;
    } catch (error) {
      console.error('Failed to capture region:', error);
      return null;
    }
  }

  /**
   * Capture a window by hwnd
   */
  public async captureWindow(hwnd: number): Promise<string | null> {
    const outputPath = this.getTempFilePath();
    try {
      const result = await this.executePowerShell<{ success: boolean; path?: string; error?: string }>(
        CAPTURE_WINDOW_SCRIPT(hwnd, outputPath)
      );

      if (result.success) {
        return result.path || outputPath;
      }
      console.error('Failed to capture window:', result.error);
      return null;
    } catch (error) {
      console.error('Failed to capture window:', error);
      return null;
    }
  }

  /**
   * Capture a region relative to a window
   */
  public async captureWindowRegion(
    windowRect: WindowRect,
    region: OCRRegion
  ): Promise<string | null> {
    const absoluteX = windowRect.x + region.x;
    const absoluteY = windowRect.y + region.y;
    return this.captureRegion(absoluteX, absoluteY, region.width, region.height);
  }

  /**
   * Read screenshot file as buffer
   */
  public async readScreenshot(filePath: string): Promise<Buffer | null> {
    try {
      return fs.readFileSync(filePath);
    } catch (error) {
      console.error('Failed to read screenshot:', error);
      return null;
    }
  }

  /**
   * Clean up temp files
   */
  public cleanupTempFiles(): void {
    try {
      const files = fs.readdirSync(this.tempDir);
      const now = Date.now();
      for (const file of files) {
        const filePath = path.join(this.tempDir, file);
        const stats = fs.statSync(filePath);
        // Delete files older than 1 hour
        if (now - stats.mtimeMs > 3600000) {
          fs.unlinkSync(filePath);
        }
      }
    } catch (error) {
      console.error('Failed to cleanup temp files:', error);
    }
  }

  /**
   * Delete a specific temp file
   */
  public deleteTempFile(filePath: string): void {
    try {
      if (fs.existsSync(filePath)) {
        fs.unlinkSync(filePath);
      }
    } catch (error) {
      console.error('Failed to delete temp file:', error);
    }
  }
}
