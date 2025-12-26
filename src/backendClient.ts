import * as http from 'http';
import * as vscode from 'vscode';

/**
 * Backend API response types
 */
export interface BackendWindow {
  hwnd: number;
  title: string;
  processName: string;
  processId: number;
  rect: {
    x: number;
    y: number;
    width: number;
    height: number;
  } | null;
}

export interface BackendClickResult {
  success: boolean;
  message: string;
}

export interface BackendOcrResult {
  success: boolean;
  text: string;
  confidence: number;
  errorMessage?: string;
}

export interface BackendStatus {
  success: boolean;
  status: string;
  port: number;
  ocrEngine: string;
  ocrAvailable: boolean;
}

export interface BackendWindowRect {
  success: boolean;
  valid: boolean;
  rect: {
    x: number;
    y: number;
    width: number;
    height: number;
  };
  clientOrigin: {
    x: number;
    y: number;
  } | null;
}

/**
 * Backend OCR Region config
 */
export interface BackendOcrRegion {
  alias: string;
  x: number;
  y: number;
  width: number;
  height: number;
  language: string;
  enabled: boolean;
}

/**
 * Backend Click Point config
 */
export interface BackendClickPoint {
  alias: string;
  x: number;
  y: number;
  clickMode: string;
  button: string;
}

/**
 * Backend full config
 */
export interface BackendConfig {
  version: number;
  targetWindowTitle: string;
  targetProcessName: string;
  clickPoints: BackendClickPoint[];
  ocrRegions: BackendOcrRegion[];
  ocrRefreshInterval: number;
  ocrAutoRefresh: boolean;
  ocrEngine: string;
}

/**
 * Backend client service for communicating with MomoBackend
 */
export class BackendClient {
  private static instance: BackendClient;
  private baseUrl: string;
  private isConnected: boolean = false;

  private constructor() {
    this.baseUrl = 'http://localhost:5678';
  }

  public static getInstance(): BackendClient {
    if (!BackendClient.instance) {
      BackendClient.instance = new BackendClient();
    }
    return BackendClient.instance;
  }

  /**
   * Check if backend is available
   */
  public async checkConnection(): Promise<boolean> {
    try {
      const status = await this.getStatus();
      this.isConnected = status.success && status.status === 'running';
      return this.isConnected;
    } catch {
      this.isConnected = false;
      return false;
    }
  }

  /**
   * Get backend status
   */
  public async getStatus(): Promise<BackendStatus> {
    return this.request<BackendStatus>('/api/status', 'GET');
  }

  /**
   * Get all windows
   */
  public async getWindows(): Promise<BackendWindow[]> {
    const result = await this.request<{ success: boolean; windows: BackendWindow[] }>(
      '/api/windows',
      'GET'
    );
    return result.windows || [];
  }

  /**
   * Get window rect by hwnd
   */
  public async getWindowRect(hwnd: number): Promise<BackendWindowRect> {
    return this.request<BackendWindowRect>(`/api/window/rect?hwnd=${hwnd}`, 'GET');
  }

  /**
   * Get cursor position
   */
  public async getCursorPosition(): Promise<{ x: number; y: number }> {
    const result = await this.request<{ success: boolean; x: number; y: number }>(
      '/api/cursor',
      'GET'
    );
    return { x: result.x, y: result.y };
  }

  /**
   * Click at position
   */
  public async click(
    hwnd: number,
    x: number,
    y: number,
    mode: string = 'fast_background',
    button: string = 'left'
  ): Promise<BackendClickResult> {
    return this.request<BackendClickResult>('/api/click', 'POST', {
      hwnd,
      x,
      y,
      mode,
      button,
    });
  }

  /**
   * Perform OCR on window region
   */
  public async ocr(
    hwnd: number,
    x: number,
    y: number,
    width: number,
    height: number,
    language: string = 'auto'
  ): Promise<BackendOcrResult> {
    return this.request<BackendOcrResult>('/api/ocr', 'POST', {
      hwnd,
      x,
      y,
      width,
      height,
      language,
    });
  }

  /**
   * Get config from backend
   */
  public async getConfig(): Promise<BackendConfig | null> {
    try {
      const result = await this.request<{ success: boolean; config: BackendConfig }>(
        '/api/config',
        'GET'
      );
      return result.success ? result.config : null;
    } catch {
      return null;
    }
  }

  /**
   * Make HTTP request to backend
   */
  private request<T>(path: string, method: string, body?: any): Promise<T> {
    return new Promise((resolve, reject) => {
      const url = new URL(path, this.baseUrl);

      const options: http.RequestOptions = {
        hostname: url.hostname,
        port: url.port,
        path: url.pathname + url.search,
        method,
        headers: {
          'Content-Type': 'application/json',
        },
        timeout: 10000,
      };

      const req = http.request(options, (res) => {
        let data = '';
        res.on('data', (chunk) => {
          data += chunk;
        });
        res.on('end', () => {
          try {
            const json = JSON.parse(data);
            resolve(json as T);
          } catch (e) {
            reject(new Error(`Invalid JSON response: ${data}`));
          }
        });
      });

      req.on('error', (e) => {
        reject(new Error(`Backend request failed: ${e.message}`));
      });

      req.on('timeout', () => {
        req.destroy();
        reject(new Error('Request timeout'));
      });

      if (body) {
        req.write(JSON.stringify(body));
      }

      req.end();
    });
  }

  /**
   * Show backend not running warning
   */
  public async showBackendWarning(): Promise<void> {
    const action = await vscode.window.showWarningMessage(
      'Momo Backend is not running. Please start MomoBackend.exe first.',
      'OK',
      'Check Again'
    );

    if (action === 'Check Again') {
      const connected = await this.checkConnection();
      if (connected) {
        vscode.window.showInformationMessage('Momo Backend is now connected!');
      } else {
        this.showBackendWarning();
      }
    }
  }

  /**
   * Check if connected
   */
  public get connected(): boolean {
    return this.isConnected;
  }
}
