/**
 * Window information structure
 */
export interface WindowInfo {
  hwnd: number;
  title: string;
  processId: number;
  processName: string;
  rect: WindowRect;
  isVisible: boolean;
}

/**
 * Window rectangle (position and size)
 */
export interface WindowRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

/**
 * Coordinate point with alias
 */
export interface CoordinatePoint {
  alias: string;
  x: number;
  y: number;
}

/**
 * Target window configuration
 */
export interface TargetWindowConfig {
  title: string;
  processName: string;
  hwnd: number;
}

/**
 * OCR region configuration
 */
export interface OCRRegion {
  x: number;
  y: number;
  width: number;
  height: number;
}

/**
 * OCR result
 */
export interface OCRResult {
  text: string;
  confidence: number;
  timestamp: number;
}

/**
 * Plugin configuration
 */
export interface MomoConfig {
  targetWindow: TargetWindowConfig;
  coordinates: CoordinatePoint[];
  ocrRegion: OCRRegion;
  ocrRefreshInterval: number;
  ocrLanguages: string[];
  statusBarButtons: string[];
}

/**
 * Screenshot result
 */
export interface ScreenshotResult {
  buffer: Buffer;
  width: number;
  height: number;
}
