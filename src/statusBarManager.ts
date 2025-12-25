import * as vscode from 'vscode';
import { ConfigManager } from './configManager';
import { WindowManager } from './windowManager';
import { BackendClient, BackendConfig, BackendClickPoint } from './backendClient';

/**
 * Status Bar Manager - displays OCR results and click buttons from backend config
 *
 * Layout: [Window] [OCR1: xxx] [OCR2: xxx] [Button1] [Button2] ... [Refresh]
 */
export class StatusBarManager {
  private static instance: StatusBarManager;

  private configManager: ConfigManager;
  private windowManager: WindowManager;
  private backendClient: BackendClient;

  // Status bar items
  private windowStatusItem: vscode.StatusBarItem | null = null;
  private ocr1StatusItem: vscode.StatusBarItem | null = null;
  private ocr2StatusItem: vscode.StatusBarItem | null = null;
  private refreshButton: vscode.StatusBarItem | null = null;
  private clickButtons: Map<string, vscode.StatusBarItem> = new Map();

  // OCR state
  private ocr1Text: string = '';
  private ocr2Text: string = '';
  private autoRefreshTimer: NodeJS.Timeout | null = null;
  private isRefreshing: boolean = false;

  // Backend config cache
  private backendConfig: BackendConfig | null = null;

  private constructor() {
    this.configManager = ConfigManager.getInstance();
    this.windowManager = WindowManager.getInstance();
    this.backendClient = BackendClient.getInstance();
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
    // Load backend config first
    await this.loadBackendConfig();

    // Create window status item (leftmost)
    this.windowStatusItem = vscode.window.createStatusBarItem(
      vscode.StatusBarAlignment.Left,
      1000
    );
    this.windowStatusItem.command = 'momo.selectWindow';
    this.updateWindowStatus();
    this.windowStatusItem.show();

    // Create OCR status items
    this.ocr1StatusItem = vscode.window.createStatusBarItem(
      vscode.StatusBarAlignment.Left,
      999
    );
    this.ocr1StatusItem.command = 'momo.captureOCR';
    this.updateOcr1Status();
    this.ocr1StatusItem.show();

    this.ocr2StatusItem = vscode.window.createStatusBarItem(
      vscode.StatusBarAlignment.Left,
      998
    );
    this.ocr2StatusItem.command = 'momo.captureOCR';
    this.updateOcr2Status();
    this.ocr2StatusItem.show();

    // Create click buttons from backend config
    await this.updateClickButtons();

    // Create refresh button (rightmost of our items)
    this.refreshButton = vscode.window.createStatusBarItem(
      vscode.StatusBarAlignment.Left,
      900
    );
    this.refreshButton.text = '$(refresh)';
    this.refreshButton.tooltip = 'Refresh OCR';
    this.refreshButton.command = 'momo.captureOCR';
    this.refreshButton.show();

    // Start auto-refresh if configured
    this.setupAutoRefresh();
  }

  /**
   * Load config from backend
   */
  public async loadBackendConfig(): Promise<void> {
    // Try to connect if not already connected
    if (!this.backendClient.connected) {
      await this.backendClient.checkConnection();
    }

    if (this.backendClient.connected) {
      this.backendConfig = await this.backendClient.getConfig();
      console.log('Backend config loaded:', JSON.stringify(this.backendConfig, null, 2));
    } else {
      console.log('Backend not connected, cannot load config');
    }
  }

  /**
   * Update window status display
   */
  public updateWindowStatus(): void {
    if (!this.windowStatusItem) return;

    const targetWindow = this.configManager.getTargetWindow();
    if (targetWindow.title) {
      const shortTitle =
        targetWindow.title.length > 15
          ? targetWindow.title.substring(0, 15) + '...'
          : targetWindow.title;
      this.windowStatusItem.text = `$(window) ${shortTitle}`;
      this.windowStatusItem.tooltip = `Target: ${targetWindow.title}\nProcess: ${targetWindow.processName}\nHWND: ${targetWindow.hwnd}\n\nClick to change`;
    } else {
      this.windowStatusItem.text = '$(window) Select Window';
      this.windowStatusItem.tooltip = 'Click to select target window (Ctrl+Alt+W)';
    }
  }

  /**
   * Update OCR region 1 status
   */
  private updateOcr1Status(): void {
    if (!this.ocr1StatusItem) return;

    if (!this.backendConfig) {
      this.ocr1StatusItem.text = '$(warning) OCR1: No Config';
      this.ocr1StatusItem.tooltip = 'Backend config not loaded. Click to refresh.';
      return;
    }

    const region = this.backendConfig.ocrRegion1;
    if (!region || !region.enabled) {
      this.ocr1StatusItem.text = '$(eye-closed) OCR1: Disabled';
      this.ocr1StatusItem.tooltip = 'OCR Region 1 is disabled in backend config';
      return;
    }

    if (this.ocr1Text) {
      const shortText = this.ocr1Text.length > 20
        ? this.ocr1Text.substring(0, 20) + '...'
        : this.ocr1Text;
      this.ocr1StatusItem.text = `$(eye) OCR1: ${shortText}`;
      this.ocr1StatusItem.tooltip = `OCR Region 1:\n${this.ocr1Text}\n\nRegion: (${region.x}, ${region.y}) ${region.width}x${region.height}`;
    } else {
      this.ocr1StatusItem.text = '$(eye) OCR1: --';
      this.ocr1StatusItem.tooltip = `OCR Region 1: No content\nRegion: (${region.x}, ${region.y}) ${region.width}x${region.height}`;
    }
  }

  /**
   * Update OCR region 2 status
   */
  private updateOcr2Status(): void {
    if (!this.ocr2StatusItem) return;

    if (!this.backendConfig) {
      this.ocr2StatusItem.text = '$(warning) OCR2: No Config';
      this.ocr2StatusItem.tooltip = 'Backend config not loaded. Click to refresh.';
      return;
    }

    const region = this.backendConfig.ocrRegion2;
    if (!region || !region.enabled) {
      this.ocr2StatusItem.text = '$(eye-closed) OCR2: Disabled';
      this.ocr2StatusItem.tooltip = 'OCR Region 2 is disabled in backend config';
      return;
    }

    if (this.ocr2Text) {
      const shortText = this.ocr2Text.length > 20
        ? this.ocr2Text.substring(0, 20) + '...'
        : this.ocr2Text;
      this.ocr2StatusItem.text = `$(eye) OCR2: ${shortText}`;
      this.ocr2StatusItem.tooltip = `OCR Region 2:\n${this.ocr2Text}\n\nRegion: (${region.x}, ${region.y}) ${region.width}x${region.height}`;
    } else {
      this.ocr2StatusItem.text = '$(eye) OCR2: --';
      this.ocr2StatusItem.tooltip = `OCR Region 2: No content\nRegion: (${region.x}, ${region.y}) ${region.width}x${region.height}`;
    }
  }

  /**
   * Update click buttons from backend config
   */
  public async updateClickButtons(): Promise<void> {
    // Clear existing buttons
    for (const [, button] of this.clickButtons) {
      button.dispose();
    }
    this.clickButtons.clear();

    // Don't reload config here - use cached config
    if (!this.backendConfig?.clickPoints || this.backendConfig.clickPoints.length === 0) {
      console.log('No click points in backend config');
      return;
    }

    console.log(`Creating ${this.backendConfig.clickPoints.length} click buttons`);

    // Create buttons for each click point
    let priority = 997;
    for (const point of this.backendConfig.clickPoints) {
      const button = vscode.window.createStatusBarItem(
        vscode.StatusBarAlignment.Left,
        priority--
      );
      button.text = `$(play) ${point.alias}`;
      button.tooltip = `Click at (${point.x}, ${point.y})\nMode: ${point.clickMode}\nButton: ${point.button}`;
      button.command = {
        command: 'momo.clickPoint',
        arguments: [point.alias],
        title: `Click ${point.alias}`,
      };
      button.show();
      this.clickButtons.set(point.alias, button);
      console.log(`Created button: ${point.alias}`);
    }
  }

  /**
   * Setup auto-refresh timer based on backend config
   */
  private setupAutoRefresh(): void {
    this.stopAutoRefresh();

    if (!this.backendConfig?.ocrAutoRefresh) return;

    const interval = this.backendConfig.ocrRefreshInterval || 3000;

    this.autoRefreshTimer = setInterval(async () => {
      await this.performOCR();
    }, interval);

    console.log(`OCR auto-refresh started: ${interval}ms interval`);
  }

  /**
   * Stop auto-refresh timer
   */
  private stopAutoRefresh(): void {
    if (this.autoRefreshTimer) {
      clearInterval(this.autoRefreshTimer);
      this.autoRefreshTimer = null;
    }
  }

  /**
   * Perform OCR on both regions
   */
  public async performOCR(): Promise<{ ocr1: string; ocr2: string } | null> {
    if (this.isRefreshing) return null;
    this.isRefreshing = true;

    // Update refresh button to show loading
    if (this.refreshButton) {
      this.refreshButton.text = '$(sync~spin)';
    }

    try {
      const targetWindow = this.configManager.getTargetWindow();
      if (!targetWindow.hwnd) {
        vscode.window.showWarningMessage('Please select a target window first');
        return null;
      }

      if (!this.backendClient.connected) {
        vscode.window.showWarningMessage('Backend not connected');
        return null;
      }

      // Reload config to get latest settings
      await this.loadBackendConfig();
      if (!this.backendConfig) {
        vscode.window.showWarningMessage('Failed to load backend config');
        return null;
      }

      // Perform OCR on region 1
      if (this.backendConfig.ocrRegion1?.enabled) {
        const region = this.backendConfig.ocrRegion1;
        const result = await this.backendClient.ocr(
          targetWindow.hwnd,
          region.x,
          region.y,
          region.width,
          region.height,
          region.language || 'auto'
        );
        this.ocr1Text = result.success ? result.text : '';
        this.updateOcr1Status();
      }

      // Perform OCR on region 2
      if (this.backendConfig.ocrRegion2?.enabled) {
        const region = this.backendConfig.ocrRegion2;
        const result = await this.backendClient.ocr(
          targetWindow.hwnd,
          region.x,
          region.y,
          region.width,
          region.height,
          region.language || 'auto'
        );
        this.ocr2Text = result.success ? result.text : '';
        this.updateOcr2Status();
      }

      return { ocr1: this.ocr1Text, ocr2: this.ocr2Text };
    } catch (error) {
      console.error('OCR failed:', error);
      vscode.window.showErrorMessage(`OCR failed: ${error}`);
      return null;
    } finally {
      this.isRefreshing = false;
      if (this.refreshButton) {
        this.refreshButton.text = '$(refresh)';
      }
    }
  }

  /**
   * Click a coordinate by alias and refresh OCR
   */
  public async clickCoordinate(alias: string): Promise<void> {
    const targetWindow = this.configManager.getTargetWindow();
    if (!targetWindow.hwnd) {
      vscode.window.showErrorMessage('Please select a target window first');
      return;
    }

    if (!this.backendClient.connected) {
      vscode.window.showErrorMessage('Backend not connected');
      return;
    }

    // Find click point in config
    const clickPoint = this.backendConfig?.clickPoints?.find(p => p.alias === alias);
    if (!clickPoint) {
      vscode.window.showErrorMessage(`Click point "${alias}" not found in backend config`);
      return;
    }

    // Perform click via backend
    const result = await this.backendClient.click(
      targetWindow.hwnd,
      clickPoint.x,
      clickPoint.y,
      clickPoint.clickMode,
      clickPoint.button
    );

    if (result.success) {
      // Small delay then refresh OCR
      setTimeout(async () => {
        await this.performOCR();
      }, 200);
    } else {
      vscode.window.showErrorMessage(`Click failed: ${result.message}`);
    }
  }

  /**
   * Get current OCR results
   */
  public getOCRResults(): { ocr1: string; ocr2: string } {
    return { ocr1: this.ocr1Text, ocr2: this.ocr2Text };
  }

  /**
   * Refresh config and UI from backend
   */
  public async refreshFromBackend(): Promise<void> {
    await this.loadBackendConfig();
    await this.updateClickButtons();
    this.setupAutoRefresh();
    this.updateOcr1Status();
    this.updateOcr2Status();
  }

  /**
   * Dispose all resources
   */
  public dispose(): void {
    this.stopAutoRefresh();

    if (this.windowStatusItem) {
      this.windowStatusItem.dispose();
      this.windowStatusItem = null;
    }

    if (this.ocr1StatusItem) {
      this.ocr1StatusItem.dispose();
      this.ocr1StatusItem = null;
    }

    if (this.ocr2StatusItem) {
      this.ocr2StatusItem.dispose();
      this.ocr2StatusItem = null;
    }

    if (this.refreshButton) {
      this.refreshButton.dispose();
      this.refreshButton = null;
    }

    for (const [, button] of this.clickButtons) {
      button.dispose();
    }
    this.clickButtons.clear();
  }
}
