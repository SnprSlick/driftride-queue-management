/**
 * Error recovery and retry logic for API calls and network failures.
 * Provides graceful degradation strategies and user guidance for error scenarios.
 */
class ErrorRecovery {
    constructor(options = {}) {
        this.options = {
            maxRetries: 3,
            initialDelay: 1000,
            maxDelay: 10000,
            backoffMultiplier: 2,
            retryableStatuses: [408, 429, 500, 502, 503, 504],
            showUserFeedback: true,
            enableOfflineDetection: true,
            ...options
        };

        this.retryQueue = new Map();
        this.isOnline = navigator.onLine;
        this.pendingRequests = new Map();

        this.init();
    }

    /**
     * Initialize error recovery system.
     */
    init() {
        if (this.options.enableOfflineDetection) {
            this.setupOfflineDetection();
        }

        this.setupGlobalErrorHandlers();
    }

    /**
     * Setup offline/online detection.
     */
    setupOfflineDetection() {
        window.addEventListener('online', () => {
            this.isOnline = true;
            this.handleOnlineRecovery();
        });

        window.addEventListener('offline', () => {
            this.isOnline = false;
            this.handleOfflineMode();
        });
    }

    /**
     * Setup global error handlers.
     */
    setupGlobalErrorHandlers() {
        // Handle unhandled promise rejections
        window.addEventListener('unhandledrejection', (event) => {
            console.error('Unhandled promise rejection:', event.reason);
            this.handleGlobalError(event.reason);
        });

        // Handle JavaScript errors
        window.addEventListener('error', (event) => {
            console.error('JavaScript error:', event.error);
            this.handleGlobalError(event.error);
        });
    }

    /**
     * Retry an API call with exponential backoff.
     */
    async retryApiCall(apiCall, requestId = null, context = {}) {
        const id = requestId || this.generateRequestId();
        const startTime = Date.now();

        let attempt = 0;
        let lastError = null;

        while (attempt <= this.options.maxRetries) {
            try {
                // Show retry feedback to user
                if (attempt > 0 && this.options.showUserFeedback) {
                    this.showRetryFeedback(attempt, context);
                }

                const result = await apiCall();

                // Clear any retry feedback
                this.clearRetryFeedback(id);

                // Track successful recovery
                if (attempt > 0) {
                    this.trackRecoverySuccess(id, attempt, Date.now() - startTime);
                }

                return result;

            } catch (error) {
                lastError = error;
                attempt++;

                // Check if error is retryable
                if (!this.isRetryableError(error) || attempt > this.options.maxRetries) {
                    break;
                }

                // Calculate delay with exponential backoff
                const delay = this.calculateDelay(attempt);

                // Wait before retry
                await this.delay(delay);

                // Check if we're still offline
                if (!this.isOnline) {
                    await this.waitForOnline();
                }
            }
        }

        // All retries failed
        this.clearRetryFeedback(id);
        this.handleFinalFailure(lastError, attempt - 1, context);
        throw lastError;
    }

    /**
     * Check if an error is retryable.
     */
    isRetryableError(error) {
        // Network errors
        if (error instanceof TypeError && error.message.includes('fetch')) {
            return true;
        }

        // HTTP status codes
        if (error.status && this.options.retryableStatuses.includes(error.status)) {
            return true;
        }

        // Timeout errors
        if (error.name === 'TimeoutError' || error.code === 'TIMEOUT') {
            return true;
        }

        // Rate limit errors (429)
        if (error.status === 429) {
            return true;
        }

        return false;
    }

    /**
     * Calculate delay for exponential backoff.
     */
    calculateDelay(attempt) {
        const delay = this.options.initialDelay * Math.pow(this.options.backoffMultiplier, attempt - 1);
        return Math.min(delay, this.options.maxDelay);
    }

    /**
     * Wait for specified delay.
     */
    delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    /**
     * Wait for online connection.
     */
    waitForOnline() {
        return new Promise(resolve => {
            if (this.isOnline) {
                resolve();
                return;
            }

            const handleOnline = () => {
                window.removeEventListener('online', handleOnline);
                resolve();
            };

            window.addEventListener('online', handleOnline);
        });
    }

    /**
     * Handle offline mode.
     */
    handleOfflineMode() {
        if (this.options.showUserFeedback) {
            this.showOfflineMessage();
        }

        // Pause any pending retries
        this.pauseRetries();
    }

    /**
     * Handle online recovery.
     */
    handleOnlineRecovery() {
        if (this.options.showUserFeedback) {
            this.showOnlineMessage();
        }

        // Resume pending retries
        this.resumeRetries();
    }

    /**
     * Show retry feedback to user.
     */
    showRetryFeedback(attempt, context) {
        const message = `Retrying... (Attempt ${attempt}/${this.options.maxRetries})`;
        this.showUserMessage(message, 'info', context.containerId);
    }

    /**
     * Clear retry feedback.
     */
    clearRetryFeedback(requestId) {
        const messageElement = document.getElementById(`retry-message-${requestId}`);
        if (messageElement) {
            messageElement.remove();
        }
    }

    /**
     * Show offline message.
     */
    showOfflineMessage() {
        this.showUserMessage(
            'You appear to be offline. Operations will resume when connection is restored.',
            'warning',
            'global-messages',
            'offline-message'
        );
    }

    /**
     * Show online message.
     */
    showOnlineMessage() {
        // Remove offline message
        const offlineMessage = document.getElementById('offline-message');
        if (offlineMessage) {
            offlineMessage.remove();
        }

        this.showUserMessage(
            'Connection restored. Resuming operations...',
            'success',
            'global-messages',
            'online-message',
            3000 // Auto-hide after 3 seconds
        );
    }

    /**
     * Show user message.
     */
    showUserMessage(message, type = 'info', containerId = null, messageId = null, autoHide = null) {
        const container = containerId ? document.getElementById(containerId) : document.body;
        if (!container) return;

        const messageElement = document.createElement('div');
        messageElement.className = `alert alert-${type} error-recovery-message`;
        messageElement.textContent = message;

        if (messageId) {
            messageElement.id = messageId;
        }

        // Add close button
        const closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.className = 'btn-close';
        closeButton.setAttribute('aria-label', 'Close');
        closeButton.onclick = () => messageElement.remove();
        messageElement.appendChild(closeButton);

        container.appendChild(messageElement);

        // Auto-hide if specified
        if (autoHide) {
            setTimeout(() => {
                if (messageElement.parentNode) {
                    messageElement.remove();
                }
            }, autoHide);
        }

        return messageElement;
    }

    /**
     * Handle final failure after all retries.
     */
    handleFinalFailure(error, attempts, context) {
        console.error(`API call failed after ${attempts} retries:`, error);

        if (this.options.showUserFeedback) {
            let userMessage = 'Operation failed. Please try again later.';

            // Customize message based on error type
            if (error.status === 429) {
                userMessage = 'Too many requests. Please wait a moment and try again.';
            } else if (error.status >= 500) {
                userMessage = 'Server error occurred. Please try again later.';
            } else if (error.status === 401) {
                userMessage = 'Session expired. Please refresh the page and try again.';
            } else if (!this.isOnline) {
                userMessage = 'No internet connection. Please check your connection and try again.';
            }

            this.showUserMessage(userMessage, 'danger', context.containerId);
        }

        // Track failure for monitoring
        this.trackFailure(error, attempts, context);
    }

    /**
     * Handle global errors.
     */
    handleGlobalError(error) {
        console.error('Global error detected:', error);

        // Don't show UI for every global error, just log
        this.trackGlobalError(error);
    }

    /**
     * Pause retry operations.
     */
    pauseRetries() {
        // Implementation would pause any ongoing retry operations
        console.log('Pausing retry operations due to offline status');
    }

    /**
     * Resume retry operations.
     */
    resumeRetries() {
        // Implementation would resume paused retry operations
        console.log('Resuming retry operations due to online status');
    }

    /**
     * Generate unique request ID.
     */
    generateRequestId() {
        return `req_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    }

    /**
     * Track recovery success for monitoring.
     */
    trackRecoverySuccess(requestId, attempts, duration) {
        console.log(`Recovery successful for ${requestId}: ${attempts} attempts, ${duration}ms`);

        // Could send to analytics service
        if (window.analytics) {
            window.analytics.track('error_recovery_success', {
                requestId,
                attempts,
                duration
            });
        }
    }

    /**
     * Track failure for monitoring.
     */
    trackFailure(error, attempts, context) {
        console.log(`Failure tracked: ${attempts} attempts`, error);

        // Could send to analytics service
        if (window.analytics) {
            window.analytics.track('error_recovery_failure', {
                error: error.message,
                status: error.status,
                attempts,
                context
            });
        }
    }

    /**
     * Track global error for monitoring.
     */
    trackGlobalError(error) {
        // Could send to error tracking service
        if (window.errorTracking) {
            window.errorTracking.captureException(error);
        }
    }

    /**
     * Create a wrapped fetch function with retry logic.
     */
    createRetryFetch() {
        return async (url, options = {}) => {
            const requestId = this.generateRequestId();

            return this.retryApiCall(async () => {
                const response = await fetch(url, options);

                if (!response.ok) {
                    const error = new Error(`HTTP ${response.status}: ${response.statusText}`);
                    error.status = response.status;
                    error.response = response;
                    throw error;
                }

                return response;
            }, requestId, { url, method: options.method || 'GET' });
        };
    }

    /**
     * Wrap an existing API service with retry logic.
     */
    wrapApiService(apiService) {
        const wrappedService = {};

        for (const method in apiService) {
            if (typeof apiService[method] === 'function') {
                wrappedService[method] = async (...args) => {
                    return this.retryApiCall(
                        () => apiService[method](...args),
                        null,
                        { service: apiService.constructor.name, method, args }
                    );
                };
            } else {
                wrappedService[method] = apiService[method];
            }
        }

        return wrappedService;
    }
}

// Global error recovery instance
window.errorRecovery = new ErrorRecovery({
    maxRetries: 3,
    showUserFeedback: true,
    enableOfflineDetection: true
});

// Enhanced fetch with retry logic
window.retryFetch = window.errorRecovery.createRetryFetch();

// Utility function to wrap existing API calls
window.withRetry = (apiCall, context = {}) => {
    return window.errorRecovery.retryApiCall(apiCall, null, context);
};

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ErrorRecovery;
}