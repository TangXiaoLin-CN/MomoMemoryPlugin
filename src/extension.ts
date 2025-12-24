import * as vscode from 'vscode';
import { WindowManager } from './windowManager';
import { ConfigManager } from './configManager';
import { CoordinatePicker } from './coordinatePicker';
import { StatusBarManager } from './statusBarManager';
import { ScreenshotService } from './screenshotService';
import { OCRService } from './ocrService';
import { WindowInfo } from './types';

let statusBarManager: StatusBarManager;
let coordinatePicker: CoordinatePicker;

export async function activate(context: vscode.ExtensionContext) {
  console.log('Momo Memory Plugin is now active!');

  // Check platform
  if (process.platform !== 'win32') {
    vscode.window.showErrorMessage(
      'Momo Memory Plugin only supports Windows platform.'
    );
    return;
  }

  // Initialize managers
  const windowManager = WindowManager.getInstance();
  const configManager = ConfigManager.getInstance();
  coordinatePicker = CoordinatePicker.getInstance();
  statusBarManager = StatusBarManager.getInstance();

  // Initialize status bar
  await statusBarManager.initialize();

  // Register commands
  const selectWindowCommand = vscode.commands.registerCommand(
    'momo.selectWindow',
    async () => {
      await selectWindow(windowManager, configManager);
    }
  );

  const pickCoordinateCommand = vscode.commands.registerCommand(
    'momo.pickCoordinate',
    async () => {
      await coordinatePicker.startPicking();
    }
  );

  const captureOCRCommand = vscode.commands.registerCommand(
    'momo.captureOCR',
    async () => {
      const result = await statusBarManager.performOCR();
      if (result && result.text) {
        // Show result in info message with option to copy
        const action = await vscode.window.showInformationMessage(
          `OCR Result: ${result.text}`,
          'Copy to Clipboard'
        );
        if (action === 'Copy to Clipboard') {
          await vscode.env.clipboard.writeText(result.text);
          vscode.window.showInformationMessage('OCR result copied to clipboard');
        }
      }
    }
  );

  const clickPointCommand = vscode.commands.registerCommand(
    'momo.clickPoint',
    async (alias?: string) => {
      if (!alias) {
        // Show picker if no alias provided
        const coordinates = configManager.getCoordinates();
        if (coordinates.length === 0) {
          vscode.window.showInformationMessage('No coordinates saved yet.');
          return;
        }

        const items = coordinates.map((c) => ({
          label: c.alias,
          description: `(${c.x}, ${c.y})`,
        }));

        const selected = await vscode.window.showQuickPick(items, {
          placeHolder: 'Select a coordinate to click',
        });

        if (selected) {
          alias = selected.label;
        }
      }

      if (alias) {
        await statusBarManager.clickCoordinate(alias);
      }
    }
  );

  const openSettingsCommand = vscode.commands.registerCommand(
    'momo.openSettings',
    async () => {
      await vscode.commands.executeCommand(
        'workbench.action.openSettings',
        'momo'
      );
    }
  );

  const startOCRMonitorCommand = vscode.commands.registerCommand(
    'momo.startOCRMonitor',
    async () => {
      await statusBarManager.startOCRMonitor();
    }
  );

  const stopOCRMonitorCommand = vscode.commands.registerCommand(
    'momo.stopOCRMonitor',
    () => {
      statusBarManager.stopOCRMonitor();
    }
  );

  const manageCoordinatesCommand = vscode.commands.registerCommand(
    'momo.manageCoordinates',
    async () => {
      await coordinatePicker.manageCoordinates();
    }
  );

  // Listen to config changes
  const configChangeListener = configManager.onConfigChange(async (e) => {
    if (e.affectsConfiguration('momo.statusBarButtons')) {
      await statusBarManager.updateCoordinateButtons();
    }
    if (e.affectsConfiguration('momo.targetWindow')) {
      statusBarManager.updateWindowStatus();
    }
    if (e.affectsConfiguration('momo.ocrLanguages')) {
      const languages = configManager.getOCRLanguages();
      const ocrService = OCRService.getInstance();
      await ocrService.setLanguages(languages);
    }
  });

  // Register all disposables
  context.subscriptions.push(
    selectWindowCommand,
    pickCoordinateCommand,
    captureOCRCommand,
    clickPointCommand,
    openSettingsCommand,
    startOCRMonitorCommand,
    stopOCRMonitorCommand,
    manageCoordinatesCommand,
    configChangeListener
  );

  // Show welcome message
  vscode.window.showInformationMessage(
    'Momo Memory Plugin activated. Use Ctrl+Alt+W to select a window.'
  );
}

/**
 * Show window selection picker
 */
async function selectWindow(
  windowManager: WindowManager,
  configManager: ConfigManager
): Promise<void> {
  // Show loading
  const loadingMessage = vscode.window.setStatusBarMessage(
    '$(sync~spin) Loading windows...'
  );

  try {
    const windows = await windowManager.getAllWindows();
    loadingMessage.dispose();

    if (windows.length === 0) {
      vscode.window.showInformationMessage('No visible windows found.');
      return;
    }

    // Filter out very small windows and sort by title
    const filteredWindows = windows
      .filter((w) => w.rect.width > 100 && w.rect.height > 100)
      .sort((a, b) => a.title.localeCompare(b.title));

    const items = filteredWindows.map((w) => ({
      label: w.title,
      description: `${w.processName} (${w.rect.width}x${w.rect.height})`,
      detail: `HWND: ${w.hwnd} | PID: ${w.processId}`,
      window: w,
    }));

    const selected = await vscode.window.showQuickPick(items, {
      placeHolder: 'Select a target window',
      matchOnDescription: true,
      matchOnDetail: true,
    });

    if (selected) {
      await configManager.setTargetWindow({
        title: selected.window.title,
        processName: selected.window.processName,
        hwnd: selected.window.hwnd,
      });

      statusBarManager.updateWindowStatus();

      vscode.window.showInformationMessage(
        `Target window set: ${selected.window.title}`
      );
    }
  } catch (error) {
    loadingMessage.dispose();
    console.error('Failed to get windows:', error);
    vscode.window.showErrorMessage(`Failed to get windows: ${error}`);
  }
}

export function deactivate() {
  console.log('Momo Memory Plugin is now deactivated');

  // Clean up resources
  if (statusBarManager) {
    statusBarManager.dispose();
  }

  if (coordinatePicker) {
    coordinatePicker.dispose();
  }

  // Terminate OCR service
  const ocrService = OCRService.getInstance();
  ocrService.terminate();

  // Clean up temp files
  const screenshotService = ScreenshotService.getInstance();
  screenshotService.cleanupTempFiles();
}
