import * as vscode from 'vscode';
import { ConfigManager } from './configManager';
import { WindowManager } from './windowManager';
import { ScreenshotService } from './screenshotService';
import { OCRService } from './ocrService';
import { MouseController } from './mouseController';
import { OCRResult } from './types';

export class StatusBarManager {
  private static instance: StatusBarManager;

  private configManager: ConfigManager;
  private windowManager: WindowManager;
  private screenshotService: ScreenshotService;
  private ocrService: OCRService;
  private mouseController: MouseController;

  private ocrStatusItem: vscode.StatusBarItem | null = null;
  private windowStatusItem: vscode.StatusBarItem | null = null;
  private coordinateButtons: Map<string, vscode.StatusBarItem> = new Map();

  private ocrMonitorInterval: NodeJS.Timeout | null = null;
  private isOCRMonitoring: boolean = false;
  private lastOCRResult: OCRResult | null = null;

  private constructor() {
    this.configManager = ConfigManager.getInstance();
    this.windowManager = WindowManager.getInstance();
    this.screenshotService = ScreenshotService.getInstance();
    this.ocrService = OCRService.getInstance();
    this.mouseController = MouseController.getInstance();
  }

  public static getInstance(): StatusBarManager {
    if (!StatusBarManager.instance) {
      StatusBarManager.instance = new StatusBarManager();
    }
    return StatusBarManager.instance;
  }

  /**
   * Initialize status bar items
   */
  public async initialize(): Promise<void> {
    // Create window status item
    this.windowStatusItem = vscode.window.createStatusBarItem(
      vscode.StatusBarAlignment.Left,
      100
    );
    this.windowStatusItem.command = 'momo.selectWindow';
    this.updateWindowStatus();
    this.windowStatusItem.show();

    // Create OCR status item
    this.ocrStatusItem = vscode.window.createStatusBarItem(
      vscode.StatusBarAlignment.Left,
      99
    );
    this.ocrStatusItem.command = 'momo.captureOCR';
    this.ocrStatusItem.text = '$(eye) OCR: Ready';
    this.ocrStatusItem.tooltip = 'Click to capture OCR (Ctrl+Alt+O)';
    this.ocrStatusItem.show();

    // Initialize coordinate buttons
    await this.updateCoordinateButtons();

    // Initialize OCR service
    const languages = this.configManager.getOCRLanguages();
    await this.ocrService.initialize(languages);
  }

  /**
   * Update window status display
   */
  public updateWindowStatus(): void {
    if (!this.windowStatusItem) return;

    const targetWindow = this.configManager.getTargetWindow();
    if (targetWindow.title) {
      const shortTitle =
        targetWindow.title.length > 20
          ? targetWindow.title.substring(0, 20) + '...'
          : targetWindow.title;
      this.windowStatusItem.text = `$(window) ${shortTitle}`;
      this.windowStatusItem.tooltip = `Target: ${targetWindow.title}\nProcess: ${targetWindow.processName}\nHWND: ${targetWindow.hwnd}`;
    } else {
      this.windowStatusItem.text = '$(window) No Window';
      this.windowStatusItem.tooltip = 'Click to select target window (Ctrl+Alt+W)';
    }
  }

  /**
   * Update coordinate buttons based on config
   */
  public async updateCoordinateButtons(): Promise<void> {
    // Clear existing buttons
    for (const [, button] of this.coordinateButtons) {
      button.dispose();
    }
    this.coordinateButtons.clear();

    // Create new buttons
    const buttonAliases = this.configManager.getStatusBarButtons();
    const coordinates = this.configManager.getCoordinates();

    let priority = 98;
    for (const alias of buttonAliases) {
      const coordinate = coordinates.find((c) => c.alias === alias);
      if (coordinate) {
        const button = vscode.window.createStatusBarItem(
          vscode.StatusBarAlignment.Left,
          priority--
        );
        button.text = `$(play) ${alias}`;
        button.tooltip = `Click at (${coordinate.x}, ${coordinate.y})`;
        button.command = {
          command: 'momo.clickPoint',
          arguments: [alias],
          title: `Click ${alias}`,
        };
        button.show();
        this.coordinateButtons.set(alias, button);
      }
    }
  }

  /**
   * Update OCR status display
   */
  public updateOCRStatus(result: OCRResult | null): void {
    if (!this.ocrStatusItem) return;

    this.lastOCRResult = result;

    if (result && result.text) {
      const shortText =
        result.text.length > 30
          ? result.text.substring(0, 30) + '...'
          : result.text;
      this.ocrStatusItem.text = `$(eye) ${shortText}`;
      this.ocrStatusItem.tooltip = `OCR Result:\n${result.text}\n\nConfidence: ${result.confidence.toFixed(1)}%\nClick to refresh`;
    } else {
      this.ocrStatusItem.text = this.isOCRMonitoring
        ? '$(sync~spin) OCR Monitoring...'
        : '$(eye) OCR: Ready';
      this.ocrStatusItem.tooltip = 'Click to capture OCR (Ctrl+Alt+O)';
    }
  }

  /**
   * Start OCR monitoring
   */
  public async startOCRMonitor(): Promise<void> {
    if (this.isOCRMonitoring) {
      return;
    }

    const targetWindow = this.configManager.getTargetWindow();
    if (!targetWindow.hwnd) {
      vscode.window.showErrorMessage('Please select a target window first');
      return;
    }

    this.isOCRMonitoring = true;
    this.updateOCRStatus(null);

    // Run immediately once
    await this.performOCR();

    // Then set interval
    const interval = this.configManager.getOCRRefreshInterval();
    this.ocrMonitorInterval = setInterval(async () => {
      await this.performOCR();
    }, interval);

    vscode.window.showInformationMessage(
      `OCR monitoring started (refresh every ${interval / 1000}s)`
    );
  }

  /**
   * Stop OCR monitoring
   */
  public stopOCRMonitor(): void {
    if (this.ocrMonitorInterval) {
      clearInterval(this.ocrMonitorInterval);
      this.ocrMonitorInterval = null;
    }
    this.isOCRMonitoring = false;
    this.updateOCRStatus(null);
    vscode.window.showInformationMessage('OCR monitoring stopped');
  }

  /**
   * Perform single OCR capture
   */
  public async performOCR(): Promise<OCRResult | null> {
    const targetWindow = this.configManager.getTargetWindow();
    if (!targetWindow.hwnd) {
      vscode.window.showErrorMessage('Please select a target window first');
      return null;
    }

    try {
      // Get window rect
      const windowRect = await this.windowManager.getWindowRect(targetWindow.hwnd);
      if (!windowRect) {
        vscode.window.showErrorMessage('Target window not found');
        return null;
      }

      // Get OCR region
      const ocrRegion = this.configManager.getOCRRegion();

      // Capture screenshot
      const screenshotPath = await this.screenshotService.captureWindowRegion(
        windowRect,
        ocrRegion
      );

      if (!screenshotPath) {
        vscode.window.showErrorMessage('Failed to capture screenshot');
        return null;
      }

      // Perform OCR
      const result = await this.ocrService.recognize(screenshotPath);

      // Clean up temp file
      this.screenshotService.deleteTempFile(screenshotPath);

      // Update status
      this.updateOCRStatus(result);

      return result;
    } catch (error) {
      console.error('OCR failed:', error);
      vscode.window.showErrorMessage(`OCR failed: ${error}`);
      return null;
    }
  }

  /**
   * Click a coordinate by alias
   */
  public async clickCoordinate(alias: string): Promise<void> {
    const coordinate = this.configManager.getCoordinateByAlias(alias);
    if (!coordinate) {
      vscode.window.showErrorMessage(`Coordinate "${alias}" not found`);
      return;
    }

    const targetWindow = this.configManager.getTargetWindow();
    if (!targetWindow.hwnd) {
      vscode.window.showErrorMessage('Please select a target window first');
      return;
    }

    // Get window rect
    const windowRect = await this.windowManager.getWindowRect(targetWindow.hwnd);
    if (!windowRect) {
      vscode.window.showErrorMessage('Target window not found');
      return;
    }

    // Click
    await this.mouseController.clickRelative(
      windowRect.x,
      windowRect.y,
      coordinate.x,
      coordinate.y
    );

    vscode.window.showInformationMessage(
      `Clicked "${alias}" at (${windowRect.x + coordinate.x}, ${windowRect.y + coordinate.y})`
    );
  }

  /**
   * Get last OCR result
   */
  public getLastOCRResult(): OCRResult | null {
    return this.lastOCRResult;
  }

  /**
   * Check if OCR is monitoring
   */
  public isMonitoring(): boolean {
    return this.isOCRMonitoring;
  }

  /**
   * Dispose all resources
   */
  public dispose(): void {
    this.stopOCRMonitor();

    if (this.ocrStatusItem) {
      this.ocrStatusItem.dispose();
      this.ocrStatusItem = null;
    }

    if (this.windowStatusItem) {
      this.windowStatusItem.dispose();
      this.windowStatusItem = null;
    }

    for (const [, button] of this.coordinateButtons) {
      button.dispose();
    }
    this.coordinateButtons.clear();
  }
}
