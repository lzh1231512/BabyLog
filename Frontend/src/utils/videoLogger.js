// Video Player Logger

import dayjs from 'dayjs';

/**
 * Video Player Logger Class
 */
export class VideoLogger {
  constructor() {
    this.logs = [];
    this.listeners = [];
  }

  /**
   * Add a log entry
   * @param {string} message - Log message
   * @param {string} type - Log type (info, success, warning, error)
   */
  add(message, type = 'info') {
    const log = {
      time: dayjs().format('HH:mm:ss.SSS'),
      message,
      type
    };
    
    this.logs.push(log);
    console.log(`[VideoPlayer ${type}]:`, message);
    
    // Notify listeners
    this.listeners.forEach(listener => listener(this.logs));
    
    return log;
  }

  /**
   * Clear all logs
   */
  clear() {
    this.logs = [];
    this.listeners.forEach(listener => listener(this.logs));
  }

  /**
   * Add a listener for log changes
   * @param {Function} callback - Function to call when logs change
   */
  addListener(callback) {
    if (typeof callback === 'function') {
      this.listeners.push(callback);
    }
  }

  /**
   * Remove a listener
   * @param {Function} callback - Function to remove
   */
  removeListener(callback) {
    this.listeners = this.listeners.filter(listener => listener !== callback);
  }

  /**
   * Get all logs
   * @returns {Array} - All log entries
   */
  getLogs() {
    return this.logs;
  }

  /**
   * Log an info message
   * @param {string} message - Log message
   */
  info(message) {
    return this.add(message, 'info');
  }

  /**
   * Log a success message
   * @param {string} message - Log message
   */
  success(message) {
    return this.add(message, 'success');
  }

  /**
   * Log a warning message
   * @param {string} message - Log message
   */
  warning(message) {
    return this.add(message, 'warning');
  }

  /**
   * Log an error message
   * @param {string} message - Log message
   */
  error(message) {
    return this.add(message, 'error');
  }
}

export default new VideoLogger();
