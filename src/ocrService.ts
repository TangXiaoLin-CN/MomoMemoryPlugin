import { createWorker, Worker } from 'tesseract.js';
import { OCRResult } from './types';

export class OCRService {
  private static instance: OCRService;
  private worker: Worker | null = null;
  private isInitialized: boolean = false;
  private initPromise: Promise<void> | null = null;
  private currentLanguages: string[] = [];

  private constructor() {}

  public static getInstance(): OCRService {
    if (!OCRService.instance) {
      OCRService.instance = new OCRService();
    }
    return OCRService.instance;
  }

  /**
   * Initialize the OCR worker with specified languages
   */
  public async initialize(languages: string[] = ['eng', 'chi_sim']): Promise<void> {
    // If already initializing, wait for it
    if (this.initPromise) {
      await this.initPromise;
      return;
    }

    // Check if already initialized with same languages
    if (this.isInitialized && this.arraysEqual(this.currentLanguages, languages)) {
      return;
    }

    this.initPromise = this.doInitialize(languages);
    await this.initPromise;
    this.initPromise = null;
  }

  private async doInitialize(languages: string[]): Promise<void> {
    try {
      // Terminate existing worker if any
      if (this.worker) {
        await this.worker.terminate();
        this.worker = null;
      }

      const langString = languages.join('+');
      console.log(`OCRService: Initializing with languages: ${langString}`);

      this.worker = await createWorker(langString, 1, {
        logger: (m) => {
          if (m.status === 'recognizing text') {
            // Progress update
          }
        },
      });

      this.currentLanguages = [...languages];
      this.isInitialized = true;
      console.log('OCRService: Initialized successfully');
    } catch (error) {
      console.error('OCRService: Failed to initialize:', error);
      this.isInitialized = false;
      throw error;
    }
  }

  /**
   * Recognize text from image file
   */
  public async recognize(imagePath: string): Promise<OCRResult> {
    if (!this.isInitialized || !this.worker) {
      await this.initialize();
    }

    try {
      const result = await this.worker!.recognize(imagePath);

      return {
        text: result.data.text.trim(),
        confidence: result.data.confidence,
        timestamp: Date.now(),
      };
    } catch (error) {
      console.error('OCRService: Recognition failed:', error);
      return {
        text: '',
        confidence: 0,
        timestamp: Date.now(),
      };
    }
  }

  /**
   * Recognize text from buffer
   */
  public async recognizeBuffer(buffer: Buffer): Promise<OCRResult> {
    if (!this.isInitialized || !this.worker) {
      await this.initialize();
    }

    try {
      const result = await this.worker!.recognize(buffer);

      return {
        text: result.data.text.trim(),
        confidence: result.data.confidence,
        timestamp: Date.now(),
      };
    } catch (error) {
      console.error('OCRService: Recognition failed:', error);
      return {
        text: '',
        confidence: 0,
        timestamp: Date.now(),
      };
    }
  }

  /**
   * Change OCR languages
   */
  public async setLanguages(languages: string[]): Promise<void> {
    if (!this.arraysEqual(this.currentLanguages, languages)) {
      await this.initialize(languages);
    }
  }

  /**
   * Get current languages
   */
  public getLanguages(): string[] {
    return [...this.currentLanguages];
  }

  /**
   * Check if initialized
   */
  public isReady(): boolean {
    return this.isInitialized && this.worker !== null;
  }

  /**
   * Terminate the worker
   */
  public async terminate(): Promise<void> {
    if (this.worker) {
      await this.worker.terminate();
      this.worker = null;
      this.isInitialized = false;
      this.currentLanguages = [];
    }
  }

  /**
   * Helper to compare arrays
   */
  private arraysEqual(a: string[], b: string[]): boolean {
    if (a.length !== b.length) return false;
    const sortedA = [...a].sort();
    const sortedB = [...b].sort();
    return sortedA.every((val, idx) => val === sortedB[idx]);
  }
}
