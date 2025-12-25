import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { spawn, ChildProcess } from 'child_process';

/**
 * Backend process manager - handles starting/stopping the MomoBackend
 */
export class BackendManager {
  private static instance: BackendManager;
  private backendProcess: ChildProcess | null = null;
  private isStarting: boolean = false;
  private outputChannel: vscode.OutputChannel;

  private constructor() {
    this.outputChannel = vscode.window.createOutputChannel('Momo Backend');
  }

  public static getInstance(): BackendManager {
    if (!BackendManager.instance) {
      BackendManager.instance = new BackendManager();
    }
    return BackendManager.instance;
  }

  /**
   * Find the backend executable path
   */
  public findBackendPath(extensionPath: string): string | null {
    // Possible locations to look for the backend
    const possiblePaths = [
      // In extension's backend folder (for packaged extension)
      path.join(extensionPath, 'backend', 'MomoBackend.exe'),
      // Development path - relative to extension
      path.join(extensionPath, 'MomoMemoryPlugin-backend', 'bin', 'Debug', 'net8.0-windows10.0.19041.0', 'MomoBackend.exe'),
      path.join(extensionPath, 'MomoMemoryPlugin-backend', 'bin', 'Release', 'net8.0-windows10.0.19041.0', 'MomoBackend.exe'),
      // Published path
      path.join(extensionPath, 'MomoMemoryPlugin-backend', 'publish', 'MomoBackend.exe'),
    ];

    for (const p of possiblePaths) {
      if (fs.existsSync(p)) {
        this.log(`Found backend at: ${p}`);
        return p;
      }
    }

    return null;
  }

  /**
   * Start the backend process
   */
  public async start(extensionPath: string, port: number = 5678): Promise<boolean> {
    if (this.backendProcess && !this.backendProcess.killed) {
      this.log('Backend is already running');
      return true;
    }

    if (this.isStarting) {
      this.log('Backend is already starting...');
      return false;
    }

    this.isStarting = true;

    try {
      const backendPath = this.findBackendPath(extensionPath);
      if (!backendPath) {
        this.log('Backend executable not found');
        vscode.window.showErrorMessage(
          'MomoBackend.exe not found. Please build the backend project first.'
        );
        return false;
      }

      this.log(`Starting backend: ${backendPath}`);

      // Start the backend process in headless mode
      this.backendProcess = spawn(backendPath, ['--headless'], {
        cwd: path.dirname(backendPath),
        detached: false,
        windowsHide: true, // Hide the console window
        stdio: ['ignore', 'pipe', 'pipe'],
      });

      // Handle stdout
      this.backendProcess.stdout?.on('data', (data) => {
        this.log(`[Backend] ${data.toString().trim()}`);
      });

      // Handle stderr
      this.backendProcess.stderr?.on('data', (data) => {
        this.log(`[Backend Error] ${data.toString().trim()}`);
      });

      // Handle process exit
      this.backendProcess.on('exit', (code, signal) => {
        this.log(`Backend exited with code ${code}, signal ${signal}`);
        this.backendProcess = null;
      });

      // Handle process error
      this.backendProcess.on('error', (err) => {
        this.log(`Backend error: ${err.message}`);
        this.backendProcess = null;
      });

      // Wait a bit for the backend to start
      await this.waitForStart(port);

      this.log('Backend started successfully');
      return true;
    } catch (error) {
      this.log(`Failed to start backend: ${error}`);
      return false;
    } finally {
      this.isStarting = false;
    }
  }

  /**
   * Wait for backend to be ready
   */
  private async waitForStart(port: number, maxWaitMs: number = 10000): Promise<boolean> {
    const startTime = Date.now();
    const checkInterval = 500;

    while (Date.now() - startTime < maxWaitMs) {
      try {
        const response = await this.checkHealth(port);
        if (response) {
          return true;
        }
      } catch {
        // Backend not ready yet
      }
      await this.sleep(checkInterval);
    }

    return false;
  }

  /**
   * Check if backend is healthy
   */
  private checkHealth(port: number): Promise<boolean> {
    return new Promise((resolve) => {
      const http = require('http');
      const req = http.get(`http://localhost:${port}/api/status`, (res: any) => {
        resolve(res.statusCode === 200);
      });
      req.on('error', () => resolve(false));
      req.setTimeout(2000, () => {
        req.destroy();
        resolve(false);
      });
    });
  }

  /**
   * Stop the backend process
   */
  public stop(): void {
    if (this.backendProcess && !this.backendProcess.killed) {
      this.log('Stopping backend...');

      // On Windows, we need to kill the process tree
      if (process.platform === 'win32') {
        const { execSync } = require('child_process');
        try {
          execSync(`taskkill /pid ${this.backendProcess.pid} /T /F`, {
            windowsHide: true,
            stdio: 'ignore'
          });
        } catch {
          // Process might already be dead
        }
      } else {
        this.backendProcess.kill('SIGTERM');
      }

      this.backendProcess = null;
      this.log('Backend stopped');
    }
  }

  /**
   * Check if backend is running
   */
  public isRunning(): boolean {
    return this.backendProcess !== null && !this.backendProcess.killed;
  }

  /**
   * Get the process ID
   */
  public getPid(): number | null {
    return this.backendProcess?.pid ?? null;
  }

  /**
   * Show output channel
   */
  public showOutput(): void {
    this.outputChannel.show();
  }

  /**
   * Log message to output channel
   */
  private log(message: string): void {
    const timestamp = new Date().toLocaleTimeString();
    this.outputChannel.appendLine(`[${timestamp}] ${message}`);
    console.log(`[BackendManager] ${message}`);
  }

  /**
   * Sleep helper
   */
  private sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  /**
   * Dispose resources
   */
  public dispose(): void {
    this.stop();
    this.outputChannel.dispose();
  }
}
