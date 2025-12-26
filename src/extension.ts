import * as vscode from 'vscode';
import { spawn } from 'child_process';
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
        if (result && result.size > 0) {
          // Build display text from all OCR results
          const textParts: string[] = [];
          const aliases: string[] = [];
          result.forEach((text, alias) => {
            if (text) {
              textParts.push(`${alias}: ${text}`);
              aliases.push(alias);
            }
          });

          if (textParts.length > 0) {
            const displayText = textParts.join('\n');
            const buttons = aliases.slice(0, 3).map(a => `Copy ${a}`); // Show up to 3 copy buttons

            const action = await vscode.window.showInformationMessage(
              displayText.length > 100 ? displayText.substring(0, 100) + '...' : displayText,
              ...buttons
            );

            if (action) {
              const aliasMatch = action.match(/^Copy (.+)$/);
              if (aliasMatch) {
                const alias = aliasMatch[1];
                const text = result.get(alias);
                if (text) {
                  await vscode.env.clipboard.writeText(text);
                  vscode.window.showInformationMessage(`${alias} result copied`);
                }
              }
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

    const openBackendConfigCommand = vscode.commands.registerCommand(
      'momo.openBackendConfig',
      async () => {
        await openBackendConfigWindow(context.extensionPath);
      }
    );

    // Register all disposables
    context.subscriptions.push(
      selectWindowCommand,
      captureOCRCommand,
      clickPointCommand,
      openSettingsCommand,
      refreshConfigCommand,
      showBackendOutputCommand,
      openBackendConfigCommand
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

/**
 * Open backend config window (launches MomoBackend.exe in GUI mode)
 */
async function openBackendConfigWindow(extensionPath: string): Promise<void> {
  const backendPath = backendManager.findBackendPath(extensionPath);

  if (!backendPath) {
    vscode.window.showErrorMessage(
      'MomoBackend.exe not found. Please build the backend first.'
    );
    return;
  }

  try {
    // Launch backend without --headless to show config window
    const configProcess = spawn(backendPath, [], {
      cwd: require('path').dirname(backendPath),
      detached: true,
      stdio: 'ignore',
    });

    // Detach the process so it runs independently
    configProcess.unref();

    vscode.window.showInformationMessage(
      'Backend config window opened. After saving, use "Momo: Refresh Config" to reload.',
      'Refresh Config'
    ).then((action) => {
      if (action === 'Refresh Config') {
        vscode.commands.executeCommand('momo.refreshConfig');
      }
    });
  } catch (error) {
    vscode.window.showErrorMessage(`Failed to open config window: ${error}`);
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
