import * as vscode from 'vscode';
import { WindowManager } from './windowManager';
import { ConfigManager } from './configManager';
import { StatusBarManager } from './statusBarManager';
import { WindowInfo } from './types';
import { BackendClient } from './backendClient';
import { BackendManager } from './backendManager';

let statusBarManager: StatusBarManager;
let backendClient: BackendClient;
let backendManager: BackendManager;

export async function activate(context: vscode.ExtensionContext) {
  console.log('Momo Memory Plugin is now active!');

  // Check platform
  if (process.platform !== 'win32') {
    vscode.window.showErrorMessage(
      'Momo Memory Plugin only supports Windows platform.'
    );
    return;
  }

  try {
    // Initialize managers
    const windowManager = WindowManager.getInstance();
    const configManager = ConfigManager.getInstance();
    statusBarManager = StatusBarManager.getInstance();
    backendClient = BackendClient.getInstance();
    backendManager = BackendManager.getInstance();

    // Auto-start backend if enabled
    if (configManager.useBackend() && configManager.autoStartBackend()) {
      vscode.window.setStatusBarMessage('$(sync~spin) Starting Momo Backend...', 5000);

      const started = await backendManager.start(
        context.extensionPath,
        configManager.getBackendPort()
      );

      if (started) {
        // Wait a moment then check connection
        await new Promise(resolve => setTimeout(resolve, 1000));
        const connected = await backendClient.checkConnection();
        if (connected) {
          vscode.window.showInformationMessage('Momo Backend started successfully (PaddleOCR ready)');
        }
      } else {
        vscode.window.showWarningMessage(
          'Failed to start Momo Backend. Using fallback methods.',
          'Show Output'
        ).then((action) => {
          if (action === 'Show Output') {
            backendManager.showOutput();
          }
        });
      }
    } else if (configManager.useBackend()) {
      // Check if backend is already running
      const connected = await backendClient.checkConnection();
      if (connected) {
        console.log('Momo Backend connected');
      } else {
        console.log('Momo Backend not available, using fallback methods');
      }
    }

    // Register commands
    const selectWindowCommand = vscode.commands.registerCommand(
      'momo.selectWindow',
      async () => {
        await selectWindow(windowManager, configManager);
      }
    );

    const captureOCRCommand = vscode.commands.registerCommand(
      'momo.captureOCR',
      async () => {
        const result = await statusBarManager.performOCR();
        if (result) {
          // Show both OCR results
          const text = [
            result.ocr1 ? `OCR1: ${result.ocr1}` : null,
            result.ocr2 ? `OCR2: ${result.ocr2}` : null,
          ].filter(Boolean).join('\n');

          if (text) {
            const action = await vscode.window.showInformationMessage(
              text.length > 100 ? text.substring(0, 100) + '...' : text,
              'Copy OCR1',
              'Copy OCR2'
            );
            if (action === 'Copy OCR1' && result.ocr1) {
              await vscode.env.clipboard.writeText(result.ocr1);
              vscode.window.showInformationMessage('OCR1 result copied');
            } else if (action === 'Copy OCR2' && result.ocr2) {
              await vscode.env.clipboard.writeText(result.ocr2);
              vscode.window.showInformationMessage('OCR2 result copied');
            }
          }
        }
      }
    );

    const clickPointCommand = vscode.commands.registerCommand(
      'momo.clickPoint',
      async (alias?: string) => {
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

    const refreshConfigCommand = vscode.commands.registerCommand(
      'momo.refreshConfig',
      async () => {
        await statusBarManager.refreshFromBackend();
        vscode.window.showInformationMessage('Config refreshed from backend');
      }
    );

    const showBackendOutputCommand = vscode.commands.registerCommand(
      'momo.showBackendOutput',
      () => {
        backendManager.showOutput();
      }
    );

    // Register all disposables
    context.subscriptions.push(
      selectWindowCommand,
      captureOCRCommand,
      clickPointCommand,
      openSettingsCommand,
      refreshConfigCommand,
      showBackendOutputCommand
    );

    // Initialize status bar (async, don't block)
    statusBarManager.initialize().catch((err) => {
      console.error('Failed to initialize status bar:', err);
    });

    // Listen to config changes
    const configChangeListener = configManager.onConfigChange(async (e) => {
      if (e.affectsConfiguration('momo.targetWindow')) {
        statusBarManager.updateWindowStatus();
      }
    });

    context.subscriptions.push(configChangeListener);

    // Show welcome message
    vscode.window.showInformationMessage(
      'Momo Memory Plugin activated. Use Ctrl+Alt+W to select a window.'
    );
  } catch (error) {
    console.error('Failed to activate Momo Memory Plugin:', error);
    vscode.window.showErrorMessage(`Momo Memory Plugin activation failed: ${error}`);
  }
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
    let windows: WindowInfo[] = [];

    // Try backend first if enabled
    if (configManager.useBackend() && backendClient.connected) {
      try {
        const backendWindows = await backendClient.getWindows();
        windows = backendWindows.map((w) => ({
          hwnd: w.hwnd,
          title: w.title,
          processId: w.processId,
          processName: w.processName,
          rect: w.rect || { x: 0, y: 0, width: 0, height: 0 },
          isVisible: true,
        }));
      } catch (e) {
        console.log('Backend getWindows failed, using fallback:', e);
        windows = await windowManager.getAllWindows();
      }
    } else {
      windows = await windowManager.getAllWindows();
    }

    loadingMessage.dispose();

    if (windows.length === 0) {
      vscode.window.showInformationMessage('No visible windows found.');
      return;
    }

    // Filter out minimized/invalid windows (negative or zero size) and sort by title
    const filteredWindows = windows
      .filter((w) => w.rect.width > 0 && w.rect.height > 0)
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

  // Stop backend if we started it
  if (backendManager) {
    backendManager.dispose();
  }

  // Clean up resources
  if (statusBarManager) {
    statusBarManager.dispose();
  }
}
