import * as vscode from 'vscode';
import { ConfigManager } from './configManager';
import { WindowManager } from './windowManager';
import { BackendClient, BackendConfig, BackendClickPoint } from './backendClient';

/**
 * Status Bar Manager - displays OCR results and click buttons from backend config
 *
 * Default Layout: [Window] [Button1] [Button2] ... [OCR1: xxx] [OCR2: xxx] [Refresh]
 * Customizable via momo.statusBarLayout setting
 */
export class StatusBarManager {
  private static instance: StatusBarManager;

  private configManager: ConfigManager;
  private windowManager: WindowManager;
  private backendClient: BackendClient;

  // Status bar items
  private windowStatusItem: vscode.StatusBarItem | null = null;
  private ocrStatusItems: Map<string, vscode.StatusBarItem> = new Map();
  private refreshButton: vscode.StatusBarItem | null = null;
  private clickButtons: Map<string, vscode.StatusBarItem> = new Map();

  // OCR state
  private ocrResults: Map<string, string> = new Map();
  private autoRefreshTimer: NodeJS.Timeout | null = null;
  private isRefreshing: boolean = false;

  // Backend config cache
  private backendConfig: BackendConfig | null = null;

  // Layout configuration
  private layoutOrder: string[] = ['window', 'buttons', 'ocr', 'refresh'];
  private alignment: vscode.StatusBarAlignment = vscode.StatusBarAlignment.Left;

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
    // Load layout configuration
    this.loadLayoutConfig();

    // Load backend config first
    await this.loadBackendConfig();

    // Calculate priorities based on layout order
    const priorities = this.calculatePriorities();

    // Create status bar items based on layout order
    for (const item of this.layoutOrder) {
      switch (item) {
        case 'window':
          this.windowStatusItem = vscode.window.createStatusBarItem(
            this.alignment,
            priorities.get('window') || 1000
          );
          this.windowStatusItem.command = 'momo.selectWindow';
          this.updateWindowStatus();
          this.windowStatusItem.show();
          break;

        case 'buttons':
          await this.updateClickButtons(priorities.get('buttons') || 990);
          break;

        case 'ocr':
          await this.updateOcrStatusItems(priorities.get('ocr') || 920);
          break;

        case 'refresh':
          this.refreshButton = vscode.window.createStatusBarItem(
            this.alignment,
            priorities.get('refresh') || 900
          );
          this.refreshButton.text = '$(refresh)';
          this.refreshButton.tooltip = 'Refresh OCR and Config (Ctrl+Alt+O)';
          this.refreshButton.command = 'momo.captureOCR';
          this.refreshButton.show();
          break;
      }
    }

    // Start auto-refresh if configured
    this.setupAutoRefresh();
  }

  /**
   * Load layout configuration from VS Code settings
   */
  private loadLayoutConfig(): void {
    const config = vscode.workspace.getConfiguration('momo');

    // Parse layout order
    const layoutStr = config.get<string>('statusBarLayout', 'window,buttons,ocr,refresh');
    this.layoutOrder = layoutStr.split(',').map(s => s.trim().toLowerCase()).filter(s => s);

    // Validate layout items - convert legacy ocr1/ocr2 to ocr
    const validItems = ['window', 'buttons', 'ocr', 'refresh'];
    this.layoutOrder = this.layoutOrder
      .map(item => (item === 'ocr1' || item === 'ocr2') ? 'ocr' : item)
      .filter(item => validItems.includes(item));

    // Remove duplicates
    this.layoutOrder = [...new Set(this.layoutOrder)];

    // Ensure all items are present (add missing ones at the end)
    for (const item of validItems) {
      if (!this.layoutOrder.includes(item)) {
        this.layoutOrder.push(item);
      }
    }

    // Parse alignment
    const alignmentStr = config.get<string>('statusBarAlignment', 'left');
    this.alignment = alignmentStr === 'right'
      ? vscode.StatusBarAlignment.Right
      : vscode.StatusBarAlignment.Left;

    console.log(`Status bar layout: ${this.layoutOrder.join(' -> ')}, alignment: ${alignmentStr}`);
  }

  /**
   * Calculate priorities based on layout order
   * For left alignment: higher priority = more to the left
   * For right alignment: lower priority = more to the right
   */
  private calculatePriorities(): Map<string, number> {
    const priorities = new Map<string, number>();
    const baseP = 1000;
    const step = 10;

    for (let i = 0; i < this.layoutOrder.length; i++) {
      const item = this.layoutOrder[i];
      // Higher priority for items that should appear first (left for Left alignment)
      priorities.set(item, baseP - i * step);
    }

    return priorities;
  }

  // Store current button priority for updateClickButtons
  private currentButtonPriority: number = 990;

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
   * Update OCR status items dynamically based on config
   */
  private async updateOcrStatusItems(basePriority?: number): Promise<void> {
    // Clear existing OCR status items
    for (const [, item] of this.ocrStatusItems) {
      item.hide();
      item.dispose();
    }
    this.ocrStatusItems.clear();

    // Small delay for UI update
    await new Promise(resolve => setTimeout(resolve, 50));

    if (!this.backendConfig?.ocrRegions || this.backendConfig.ocrRegions.length === 0) {
      console.log('No OCR regions in backend config');
      return;
    }

    console.log(`Creating ${this.backendConfig.ocrRegions.length} OCR status items`);

    // Use provided priority or default
    let priority = basePriority ?? this.currentOcrPriority;
    if (basePriority !== undefined) {
      this.currentOcrPriority = basePriority;
    }

    // Create status items for each OCR region
    for (const region of this.backendConfig.ocrRegions) {
      if (!region.enabled) continue;

      const statusItem = vscode.window.createStatusBarItem(
        this.alignment,
        priority--
      );
      statusItem.command = 'momo.captureOCR';

      // Get cached OCR result if available
      const ocrText = this.ocrResults.get(region.alias) || '';
      this.updateOcrStatusItem(statusItem, region.alias, ocrText, region);

      statusItem.show();
      this.ocrStatusItems.set(region.alias, statusItem);
      console.log(`Created OCR status item: ${region.alias}`);
    }
  }

  /**
   * Update single OCR status item display
   */
  private updateOcrStatusItem(
    statusItem: vscode.StatusBarItem,
    alias: string,
    text: string,
    region: { x: number; y: number; width: number; height: number; enabled: boolean }
  ): void {
    if (text) {
      const shortText = text.length > 20 ? text.substring(0, 20) + '...' : text;
      statusItem.text = `$(eye) ${alias}: ${shortText}`;
      statusItem.tooltip = `${alias}:\n${text}\n\nRegion: (${region.x}, ${region.y}) ${region.width}x${region.height}`;
    } else {
      statusItem.text = `$(eye) ${alias}: --`;
      statusItem.tooltip = `${alias}: No content\nRegion: (${region.x}, ${region.y}) ${region.width}x${region.height}`;
    }
  }

  // Store current OCR priority for updateOcrStatusItems
  private currentOcrPriority: number = 920;

  /**
   * Update click buttons from backend config
   */
  public async updateClickButtons(basePriority?: number): Promise<void> {
    // Clear existing buttons - hide first, then dispose
    for (const [, button] of this.clickButtons) {
      button.hide();
      button.dispose();
    }
    this.clickButtons.clear();

    // Small delay to let VS Code update UI
    await new Promise(resolve => setTimeout(resolve, 50));

    // Don't reload config here - use cached config
    if (!this.backendConfig?.clickPoints || this.backendConfig.clickPoints.length === 0) {
      console.log('No click points in backend config');
      return;
    }

    console.log(`Creating ${this.backendConfig.clickPoints.length} click buttons`);

    // Use provided priority or stored priority
    let priority = basePriority ?? this.currentButtonPriority;
    if (basePriority !== undefined) {
      this.currentButtonPriority = basePriority;
    }

    // Create buttons for each click point
    for (const point of this.backendConfig.clickPoints) {
      const button = vscode.window.createStatusBarItem(
        this.alignment,
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
   * Perform OCR on all enabled regions (and refresh config/buttons)
   */
  public async performOCR(): Promise<Map<string, string> | null> {
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

      // Reload config to get latest settings (including OCR regions)
      const oldConfig = this.backendConfig;
      await this.loadBackendConfig();
      if (!this.backendConfig) {
        vscode.window.showWarningMessage('Failed to load backend config');
        return null;
      }

      // Only update click buttons and OCR status items if config changed
      const configChanged = !oldConfig ||
        JSON.stringify(oldConfig.clickPoints) !== JSON.stringify(this.backendConfig.clickPoints) ||
        JSON.stringify(oldConfig.ocrRegions) !== JSON.stringify(this.backendConfig.ocrRegions);

      if (configChanged) {
        console.log('Config changed, updating UI elements');
        await this.updateClickButtons();
        await this.updateOcrStatusItems();
      }

      // Check if there are any OCR regions
      if (!this.backendConfig.ocrRegions || this.backendConfig.ocrRegions.length === 0) {
        console.log('No OCR regions configured');
        return this.ocrResults;
      }

      // Perform OCR on all enabled regions
      for (const region of this.backendConfig.ocrRegions) {
        if (!region.enabled) continue;

        const result = await this.backendClient.ocr(
          targetWindow.hwnd,
          region.x,
          region.y,
          region.width,
          region.height,
          region.language || 'auto'
        );

        const ocrText = result.success ? result.text : '';
        this.ocrResults.set(region.alias, ocrText);

        // Update the corresponding status item
        const statusItem = this.ocrStatusItems.get(region.alias);
        if (statusItem) {
          this.updateOcrStatusItem(statusItem, region.alias, ocrText, region);
        } else {
          console.log(`Status item not found for alias: ${region.alias}`);
        }
      }

      return this.ocrResults;
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
  public getOCRResults(): Map<string, string> {
    return new Map(this.ocrResults);
  }

  /**
   * Refresh config and UI from backend
   */
  public async refreshFromBackend(): Promise<void> {
    await this.loadBackendConfig();
    await this.updateClickButtons();
    await this.updateOcrStatusItems();
    this.setupAutoRefresh();
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

    for (const [, item] of this.ocrStatusItems) {
      item.dispose();
    }
    this.ocrStatusItems.clear();

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
