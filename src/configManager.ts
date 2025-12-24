import * as vscode from 'vscode';
import {
  CoordinatePoint,
  TargetWindowConfig,
  OCRRegion,
  MomoConfig,
} from './types';

export class ConfigManager {
  private static instance: ConfigManager;
  private readonly configSection = 'momo';

  private constructor() {}

  public static getInstance(): ConfigManager {
    if (!ConfigManager.instance) {
      ConfigManager.instance = new ConfigManager();
    }
    return ConfigManager.instance;
  }

  /**
   * Get VS Code configuration
   */
  private getConfig(): vscode.WorkspaceConfiguration {
    return vscode.workspace.getConfiguration(this.configSection);
  }

  /**
   * Get target window configuration
   */
  public getTargetWindow(): TargetWindowConfig {
    const config = this.getConfig();
    return config.get<TargetWindowConfig>('targetWindow', {
      title: '',
      processName: '',
      hwnd: 0,
    });
  }

  /**
   * Set target window configuration
   */
  public async setTargetWindow(target: TargetWindowConfig): Promise<void> {
    const config = this.getConfig();
    await config.update('targetWindow', target, vscode.ConfigurationTarget.Global);
  }

  /**
   * Get all saved coordinates
   */
  public getCoordinates(): CoordinatePoint[] {
    const config = this.getConfig();
    return config.get<CoordinatePoint[]>('coordinates', []);
  }

  /**
   * Add a new coordinate
   */
  public async addCoordinate(point: CoordinatePoint): Promise<void> {
    const coordinates = this.getCoordinates();

    // Check if alias already exists
    const existingIndex = coordinates.findIndex((c) => c.alias === point.alias);
    if (existingIndex >= 0) {
      coordinates[existingIndex] = point;
    } else {
      coordinates.push(point);
    }

    const config = this.getConfig();
    await config.update('coordinates', coordinates, vscode.ConfigurationTarget.Global);
  }

  /**
   * Remove a coordinate by alias
   */
  public async removeCoordinate(alias: string): Promise<void> {
    const coordinates = this.getCoordinates();
    const filtered = coordinates.filter((c) => c.alias !== alias);

    const config = this.getConfig();
    await config.update('coordinates', filtered, vscode.ConfigurationTarget.Global);
  }

  /**
   * Get coordinate by alias
   */
  public getCoordinateByAlias(alias: string): CoordinatePoint | undefined {
    const coordinates = this.getCoordinates();
    return coordinates.find((c) => c.alias === alias);
  }

  /**
   * Get OCR region configuration
   */
  public getOCRRegion(): OCRRegion {
    const config = this.getConfig();
    return config.get<OCRRegion>('ocrRegion', {
      x: 0,
      y: 0,
      width: 300,
      height: 50,
    });
  }

  /**
   * Set OCR region configuration
   */
  public async setOCRRegion(region: OCRRegion): Promise<void> {
    const config = this.getConfig();
    await config.update('ocrRegion', region, vscode.ConfigurationTarget.Global);
  }

  /**
   * Get OCR refresh interval
   */
  public getOCRRefreshInterval(): number {
    const config = this.getConfig();
    return config.get<number>('ocrRefreshInterval', 5000);
  }

  /**
   * Set OCR refresh interval
   */
  public async setOCRRefreshInterval(interval: number): Promise<void> {
    const config = this.getConfig();
    await config.update('ocrRefreshInterval', interval, vscode.ConfigurationTarget.Global);
  }

  /**
   * Get OCR languages
   */
  public getOCRLanguages(): string[] {
    const config = this.getConfig();
    return config.get<string[]>('ocrLanguages', ['eng', 'chi_sim']);
  }

  /**
   * Set OCR languages
   */
  public async setOCRLanguages(languages: string[]): Promise<void> {
    const config = this.getConfig();
    await config.update('ocrLanguages', languages, vscode.ConfigurationTarget.Global);
  }

  /**
   * Get status bar buttons
   */
  public getStatusBarButtons(): string[] {
    const config = this.getConfig();
    return config.get<string[]>('statusBarButtons', []);
  }

  /**
   * Set status bar buttons
   */
  public async setStatusBarButtons(buttons: string[]): Promise<void> {
    const config = this.getConfig();
    await config.update('statusBarButtons', buttons, vscode.ConfigurationTarget.Global);
  }

  /**
   * Get click mode
   */
  public getClickMode(): 'background' | 'foreground' {
    const config = this.getConfig();
    return config.get<'background' | 'foreground'>('clickMode', 'foreground');
  }

  /**
   * Add a button to status bar
   */
  public async addStatusBarButton(alias: string): Promise<void> {
    const buttons = this.getStatusBarButtons();
    if (!buttons.includes(alias)) {
      buttons.push(alias);
      await this.setStatusBarButtons(buttons);
    }
  }

  /**
   * Remove a button from status bar
   */
  public async removeStatusBarButton(alias: string): Promise<void> {
    const buttons = this.getStatusBarButtons();
    const filtered = buttons.filter((b) => b !== alias);
    await this.setStatusBarButtons(filtered);
  }

  /**
   * Get all configuration
   */
  public getAllConfig(): MomoConfig {
    return {
      targetWindow: this.getTargetWindow(),
      coordinates: this.getCoordinates(),
      ocrRegion: this.getOCRRegion(),
      ocrRefreshInterval: this.getOCRRefreshInterval(),
      ocrLanguages: this.getOCRLanguages(),
      statusBarButtons: this.getStatusBarButtons(),
    };
  }

  /**
   * Listen to configuration changes
   */
  public onConfigChange(callback: (e: vscode.ConfigurationChangeEvent) => void): vscode.Disposable {
    return vscode.workspace.onDidChangeConfiguration((e) => {
      if (e.affectsConfiguration(this.configSection)) {
        callback(e);
      }
    });
  }
}
