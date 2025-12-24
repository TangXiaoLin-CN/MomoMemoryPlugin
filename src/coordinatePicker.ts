import * as vscode from 'vscode';
import { WindowManager } from './windowManager';
import { ConfigManager } from './configManager';
import { CoordinatePoint, WindowRect } from './types';

export class CoordinatePicker {
  private static instance: CoordinatePicker;
  private windowManager: WindowManager;
  private configManager: ConfigManager;
  private isPickingMode: boolean = false;
  private pickingInterval: NodeJS.Timeout | null = null;
  private statusBarItem: vscode.StatusBarItem | null = null;

  private constructor() {
    this.windowManager = WindowManager.getInstance();
    this.configManager = ConfigManager.getInstance();
  }

  public static getInstance(): CoordinatePicker {
    if (!CoordinatePicker.instance) {
      CoordinatePicker.instance = new CoordinatePicker();
    }
    return CoordinatePicker.instance;
  }

  /**
   * Start coordinate picking mode
   */
  public async startPicking(): Promise<void> {
    const targetWindow = this.configManager.getTargetWindow();

    if (!targetWindow.hwnd) {
      vscode.window.showErrorMessage('Please select a target window first (Ctrl+Alt+W)');
      return;
    }

    // Check if window is still valid
    const isValid = await this.windowManager.isWindowValid(targetWindow.hwnd);
    if (!isValid) {
      vscode.window.showErrorMessage('Target window is no longer available. Please select a new window.');
      return;
    }

    this.isPickingMode = true;

    // Show status bar indicator
    this.showPickingStatus();

    // Show instruction
    const result = await vscode.window.showInformationMessage(
      'Coordinate Picking Mode: Click anywhere on the target window, then press Enter to capture the coordinate. Press Escape to cancel.',
      'Capture Now',
      'Cancel'
    );

    if (result === 'Capture Now') {
      await this.captureCurrentPosition();
    }

    this.stopPicking();
  }

  /**
   * Capture current cursor position relative to target window
   */
  public async captureCurrentPosition(): Promise<CoordinatePoint | null> {
    const targetWindow = this.configManager.getTargetWindow();

    if (!targetWindow.hwnd) {
      vscode.window.showErrorMessage('No target window selected');
      return null;
    }

    // Get window rect
    const windowRect = await this.windowManager.getWindowRect(targetWindow.hwnd);
    if (!windowRect) {
      vscode.window.showErrorMessage('Failed to get target window position');
      return null;
    }

    // Get cursor position
    const cursorPos = await this.windowManager.getCursorPosition();

    // Calculate relative coordinates
    const relativeX = cursorPos.x - windowRect.x;
    const relativeY = cursorPos.y - windowRect.y;

    // Check if cursor is within window bounds
    if (
      relativeX < 0 ||
      relativeY < 0 ||
      relativeX > windowRect.width ||
      relativeY > windowRect.height
    ) {
      vscode.window.showWarningMessage(
        `Cursor is outside the target window. Coordinates: (${relativeX}, ${relativeY})`
      );
    }

    // Ask for alias
    const alias = await vscode.window.showInputBox({
      prompt: 'Enter an alias for this coordinate',
      placeHolder: 'e.g., "Start Button", "Close Icon"',
      validateInput: (value) => {
        if (!value || value.trim().length === 0) {
          return 'Alias cannot be empty';
        }
        return null;
      },
    });

    if (!alias) {
      return null;
    }

    const point: CoordinatePoint = {
      alias: alias.trim(),
      x: relativeX,
      y: relativeY,
    };

    // Save to config
    await this.configManager.addCoordinate(point);

    // Ask if user wants to add to status bar
    const addToStatusBar = await vscode.window.showQuickPick(['Yes', 'No'], {
      placeHolder: 'Add this coordinate as a status bar button?',
    });

    if (addToStatusBar === 'Yes') {
      await this.configManager.addStatusBarButton(point.alias);
    }

    vscode.window.showInformationMessage(
      `Coordinate saved: "${point.alias}" at (${point.x}, ${point.y})`
    );

    return point;
  }

  /**
   * Stop picking mode
   */
  public stopPicking(): void {
    this.isPickingMode = false;
    if (this.pickingInterval) {
      clearInterval(this.pickingInterval);
      this.pickingInterval = null;
    }
    this.hidePickingStatus();
  }

  /**
   * Check if in picking mode
   */
  public isPicking(): boolean {
    return this.isPickingMode;
  }

  /**
   * Show picking status in status bar
   */
  private showPickingStatus(): void {
    if (!this.statusBarItem) {
      this.statusBarItem = vscode.window.createStatusBarItem(
        vscode.StatusBarAlignment.Left,
        1000
      );
    }
    this.statusBarItem.text = '$(target) Picking Coordinate...';
    this.statusBarItem.backgroundColor = new vscode.ThemeColor(
      'statusBarItem.warningBackground'
    );
    this.statusBarItem.show();
  }

  /**
   * Hide picking status
   */
  private hidePickingStatus(): void {
    if (this.statusBarItem) {
      this.statusBarItem.hide();
    }
  }

  /**
   * Manage saved coordinates (show list, edit, delete)
   */
  public async manageCoordinates(): Promise<void> {
    const coordinates = this.configManager.getCoordinates();

    if (coordinates.length === 0) {
      vscode.window.showInformationMessage(
        'No coordinates saved yet. Use Ctrl+Alt+P to pick coordinates.'
      );
      return;
    }

    const items = coordinates.map((c) => ({
      label: c.alias,
      description: `(${c.x}, ${c.y})`,
      detail: 'Click to manage this coordinate',
      coordinate: c,
    }));

    const selected = await vscode.window.showQuickPick(items, {
      placeHolder: 'Select a coordinate to manage',
    });

    if (!selected) {
      return;
    }

    const action = await vscode.window.showQuickPick(
      [
        { label: 'Delete', description: 'Remove this coordinate' },
        { label: 'Add to Status Bar', description: 'Show as button in status bar' },
        { label: 'Remove from Status Bar', description: 'Hide button from status bar' },
        { label: 'Test Click', description: 'Click at this coordinate' },
      ],
      { placeHolder: `Action for "${selected.label}"` }
    );

    if (!action) {
      return;
    }

    switch (action.label) {
      case 'Delete':
        await this.configManager.removeCoordinate(selected.coordinate.alias);
        await this.configManager.removeStatusBarButton(selected.coordinate.alias);
        vscode.window.showInformationMessage(`Deleted coordinate: ${selected.label}`);
        break;

      case 'Add to Status Bar':
        await this.configManager.addStatusBarButton(selected.coordinate.alias);
        vscode.window.showInformationMessage(`Added to status bar: ${selected.label}`);
        break;

      case 'Remove from Status Bar':
        await this.configManager.removeStatusBarButton(selected.coordinate.alias);
        vscode.window.showInformationMessage(`Removed from status bar: ${selected.label}`);
        break;

      case 'Test Click':
        // This will be handled by the extension command
        vscode.commands.executeCommand('momo.clickPoint', selected.coordinate.alias);
        break;
    }
  }

  /**
   * Dispose resources
   */
  public dispose(): void {
    this.stopPicking();
    if (this.statusBarItem) {
      this.statusBarItem.dispose();
      this.statusBarItem = null;
    }
  }
}
