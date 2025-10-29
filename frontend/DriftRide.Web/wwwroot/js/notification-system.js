/**
 * Comprehensive Notification Display System
 * Handles visual notifications, sound alerts, and status indicators for DriftRide
 * Supports multiple notification types with priority handling and accessibility features
 */

class NotificationSystem {
    constructor(options = {}) {
        this.config = {
            soundEnabled: options.soundEnabled ?? true,
            animationsEnabled: options.animationsEnabled ?? true,
            maxNotifications: options.maxNotifications ?? 5,
            defaultTimeout: options.defaultTimeout ?? 5000,
            soundVolume: options.soundVolume ?? 0.7,
            position: options.position ?? 'top-end',
            theme: options.theme ?? 'light'
        };

        this.notifications = new Map();
        this.soundQueue = [];
        this.isPlayingSound = false;

        this.init();
    }

    /**
     * Initialize the notification system
     */
    init() {
        this.createNotificationContainer();
        this.createStatusIndicators();
        this.setupKeyboardAccessibility();
        this.preloadSounds();
        this.bindEvents();

        console.log('Notification system initialized');
    }

    /**
     * Create notification container if it doesn't exist
     */
    createNotificationContainer() {
        let container = document.querySelector('.notification-container');
        if (!container) {
            container = document.createElement('div');
            container.className = `notification-container position-fixed ${this.getPositionClasses()} p-3`;
            container.setAttribute('aria-live', 'polite');
            container.setAttribute('aria-label', 'Notifications');
            document.body.appendChild(container);
        }
        this.container = container;
    }

    /**
     * Create status indicators for connection and system health
     */
    createStatusIndicators() {
        let statusBar = document.querySelector('.status-bar');
        if (!statusBar) {
            statusBar = document.createElement('div');
            statusBar.className = 'status-bar position-fixed bottom-0 start-0 w-100 bg-light border-top p-2 d-none';
            statusBar.innerHTML = `
                <div class="d-flex justify-content-between align-items-center">
                    <div class="status-indicators d-flex gap-3">
                        <div class="connection-status d-flex align-items-center">
                            <i id="connection-icon" class="fas fa-circle text-success me-2"></i>
                            <span id="connection-text">Connected</span>
                        </div>
                        <div class="queue-status d-flex align-items-center">
                            <i class="fas fa-users me-2"></i>
                            <span id="queue-count">0</span> in queue
                        </div>
                        <div class="pending-status d-flex align-items-center">
                            <i class="fas fa-clock me-2"></i>
                            <span id="pending-count">0</span> pending
                        </div>
                    </div>
                    <div class="system-controls d-flex gap-2">
                        <button id="toggle-sounds" class="btn btn-sm btn-outline-secondary" title="Toggle sounds">
                            <i class="fas fa-volume-up"></i>
                        </button>
                        <button id="clear-notifications" class="btn btn-sm btn-outline-secondary" title="Clear all notifications">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                </div>
            `;
            document.body.appendChild(statusBar);
        }
        this.statusBar = statusBar;
    }

    /**
     * Setup keyboard accessibility
     */
    setupKeyboardAccessibility() {
        document.addEventListener('keydown', (e) => {
            // Escape key clears all notifications
            if (e.key === 'Escape' && e.ctrlKey) {
                this.clearAllNotifications();
            }
            // F9 toggles sounds
            if (e.key === 'F9') {
                e.preventDefault();
                this.toggleSounds();
            }
        });
    }

    /**
     * Preload notification sounds
     */
    preloadSounds() {
        const sounds = [
            { id: 'notification-sound', src: '/audio/notification.mp3' },
            { id: 'success-sound', src: '/audio/success.mp3' },
            { id: 'warning-sound', src: '/audio/warning.mp3' },
            { id: 'error-sound', src: '/audio/error.mp3' },
            { id: 'critical-sound', src: '/audio/critical.mp3' },
            { id: 'new-item-sound', src: '/audio/new-item.mp3' }
        ];

        sounds.forEach(sound => {
            let audio = document.getElementById(sound.id);
            if (!audio) {
                audio = document.createElement('audio');
                audio.id = sound.id;
                audio.src = sound.src;
                audio.volume = this.config.soundVolume;
                audio.preload = 'auto';
                document.body.appendChild(audio);
            }
        });
    }

    /**
     * Bind event handlers
     */
    bindEvents() {
        // Toggle sounds button
        const toggleSoundsBtn = document.getElementById('toggle-sounds');
        if (toggleSoundsBtn) {
            toggleSoundsBtn.addEventListener('click', () => this.toggleSounds());
        }

        // Clear notifications button
        const clearBtn = document.getElementById('clear-notifications');
        if (clearBtn) {
            clearBtn.addEventListener('click', () => this.clearAllNotifications());
        }
    }

    /**
     * Show a notification
     */
    show(options) {
        const notification = this.createNotification(options);
        this.addToContainer(notification);
        this.manageLimits();

        // Play sound if enabled
        if (this.config.soundEnabled && options.sound !== false) {
            this.playSound(options.soundType || 'default', options.priority);
        }

        // Auto-dismiss if configured
        if (options.autoDismiss !== false) {
            const timeout = options.timeout || this.config.defaultTimeout;
            setTimeout(() => {
                this.dismiss(notification.id);
            }, timeout);
        }

        return notification.id;
    }

    /**
     * Create notification element
     */
    createNotification(options) {
        const id = options.id || `notification-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

        const notification = {
            id,
            element: null,
            options: { ...options, id }
        };

        const element = document.createElement('div');
        element.className = this.getNotificationClasses(options);
        element.setAttribute('role', 'alert');
        element.setAttribute('data-notification-id', id);

        if (options.priority === 'Critical') {
            element.setAttribute('aria-live', 'assertive');
        }

        element.innerHTML = this.getNotificationHTML(options);

        // Bind dismiss handlers
        const dismissBtn = element.querySelector('.notification-dismiss');
        if (dismissBtn) {
            dismissBtn.addEventListener('click', () => this.dismiss(id));
        }

        // Bind action handlers
        const actionBtns = element.querySelectorAll('.notification-action');
        actionBtns.forEach(btn => {
            btn.addEventListener('click', (e) => {
                const action = e.target.getAttribute('data-action');
                if (options.onAction) {
                    options.onAction(action, options.data);
                }
                if (e.target.getAttribute('data-dismiss') === 'true') {
                    this.dismiss(id);
                }
            });
        });

        notification.element = element;
        this.notifications.set(id, notification);

        return notification;
    }

    /**
     * Get notification CSS classes
     */
    getNotificationClasses(options) {
        const baseClasses = 'notification toast align-items-center border-0 mb-2';
        const typeClasses = this.getTypeClasses(options.type, options.priority);
        const animationClasses = this.config.animationsEnabled ? 'fade show' : 'show';

        return `${baseClasses} ${typeClasses} ${animationClasses}`;
    }

    /**
     * Get type-specific CSS classes
     */
    getTypeClasses(type, priority) {
        const typeMap = {
            'info': 'text-white bg-info',
            'success': 'text-white bg-success',
            'warning': 'text-dark bg-warning',
            'error': 'text-white bg-danger',
            'payment': 'text-white bg-primary',
            'queue': 'text-white bg-secondary',
            'alert': 'text-white bg-warning'
        };

        const priorityMap = {
            'Critical': 'border-danger border-3',
            'Warning': 'border-warning border-2',
            'Info': ''
        };

        const typeClass = typeMap[type] || typeMap['info'];
        const priorityClass = priorityMap[priority] || '';

        return `${typeClass} ${priorityClass}`;
    }

    /**
     * Get notification HTML content
     */
    getNotificationHTML(options) {
        const iconMap = {
            'info': 'fa-info-circle',
            'success': 'fa-check-circle',
            'warning': 'fa-exclamation-triangle',
            'error': 'fa-times-circle',
            'payment': 'fa-credit-card',
            'queue': 'fa-users',
            'alert': 'fa-bell'
        };

        const icon = iconMap[options.type] || iconMap['info'];
        const title = options.title ? `<strong>${options.title}</strong><br>` : '';
        const actions = this.getActionsHTML(options.actions);

        return `
            <div class="d-flex w-100">
                <div class="notification-icon me-2">
                    <i class="fas ${icon}"></i>
                </div>
                <div class="notification-body flex-grow-1">
                    ${title}
                    ${options.message}
                    ${actions}
                </div>
                <button type="button" class="btn-close btn-close-white notification-dismiss ms-2"
                        aria-label="Close notification"></button>
            </div>
        `;
    }

    /**
     * Get actions HTML
     */
    getActionsHTML(actions) {
        if (!actions || actions.length === 0) return '';

        const actionButtons = actions.map(action => `
            <button type="button"
                    class="btn btn-sm btn-outline-light notification-action me-2 mt-2"
                    data-action="${action.id}"
                    data-dismiss="${action.dismiss || false}">
                ${action.label}
            </button>
        `).join('');

        return `<div class="notification-actions">${actionButtons}</div>`;
    }

    /**
     * Add notification to container
     */
    addToContainer(notification) {
        this.container.appendChild(notification.element);

        // Trigger entrance animation
        if (this.config.animationsEnabled) {
            requestAnimationFrame(() => {
                notification.element.classList.add('notification-enter');
            });
        }
    }

    /**
     * Manage notification limits
     */
    manageLimits() {
        const visibleNotifications = this.container.querySelectorAll('.notification');

        if (visibleNotifications.length > this.config.maxNotifications) {
            // Remove oldest notifications
            const excess = visibleNotifications.length - this.config.maxNotifications;
            for (let i = 0; i < excess; i++) {
                const oldest = visibleNotifications[i];
                const id = oldest.getAttribute('data-notification-id');
                this.dismiss(id);
            }
        }
    }

    /**
     * Dismiss a notification
     */
    dismiss(id) {
        const notification = this.notifications.get(id);
        if (!notification) return;

        const element = notification.element;

        if (this.config.animationsEnabled) {
            element.classList.add('notification-exit');
            element.addEventListener('animationend', () => {
                this.removeNotification(id);
            }, { once: true });
        } else {
            this.removeNotification(id);
        }
    }

    /**
     * Remove notification from DOM and tracking
     */
    removeNotification(id) {
        const notification = this.notifications.get(id);
        if (notification && notification.element.parentNode) {
            notification.element.parentNode.removeChild(notification.element);
        }
        this.notifications.delete(id);
    }

    /**
     * Clear all notifications
     */
    clearAllNotifications() {
        this.notifications.forEach((notification, id) => {
            this.dismiss(id);
        });
    }

    /**
     * Play notification sound
     */
    playSound(type, priority) {
        if (!this.config.soundEnabled) return;

        const soundMap = {
            'default': 'notification-sound',
            'success': 'success-sound',
            'warning': 'warning-sound',
            'error': 'error-sound',
            'critical': 'critical-sound',
            'new-item': 'new-item-sound'
        };

        // Priority override
        if (priority === 'Critical') {
            type = 'critical';
        }

        const soundId = soundMap[type] || soundMap['default'];
        this.soundQueue.push(soundId);
        this.processSoundQueue();
    }

    /**
     * Process sound queue to prevent overlapping
     */
    processSoundQueue() {
        if (this.isPlayingSound || this.soundQueue.length === 0) return;

        const soundId = this.soundQueue.shift();
        const audio = document.getElementById(soundId);

        if (audio) {
            this.isPlayingSound = true;
            audio.currentTime = 0;

            audio.play()
                .then(() => {
                    audio.addEventListener('ended', () => {
                        this.isPlayingSound = false;
                        this.processSoundQueue();
                    }, { once: true });
                })
                .catch(e => {
                    console.log('Could not play notification sound:', e);
                    this.isPlayingSound = false;
                    this.processSoundQueue();
                });
        }
    }

    /**
     * Toggle sound enablement
     */
    toggleSounds() {
        this.config.soundEnabled = !this.config.soundEnabled;

        const toggleBtn = document.getElementById('toggle-sounds');
        if (toggleBtn) {
            const icon = toggleBtn.querySelector('i');
            if (this.config.soundEnabled) {
                icon.className = 'fas fa-volume-up';
                toggleBtn.classList.remove('btn-outline-secondary');
                toggleBtn.classList.add('btn-outline-primary');
            } else {
                icon.className = 'fas fa-volume-mute';
                toggleBtn.classList.remove('btn-outline-primary');
                toggleBtn.classList.add('btn-outline-secondary');
            }
        }

        this.show({
            type: 'info',
            message: `Sound alerts ${this.config.soundEnabled ? 'enabled' : 'disabled'}`,
            timeout: 2000,
            sound: false
        });
    }

    /**
     * Update connection status
     */
    updateConnectionStatus(status, type = 'success') {
        const icon = document.getElementById('connection-icon');
        const text = document.getElementById('connection-text');

        if (icon && text) {
            const statusMap = {
                'success': { class: 'text-success', icon: 'fa-circle' },
                'warning': { class: 'text-warning', icon: 'fa-circle' },
                'error': { class: 'text-danger', icon: 'fa-times-circle' },
                'connecting': { class: 'text-info', icon: 'fa-circle-notch fa-spin' }
            };

            const config = statusMap[type] || statusMap['success'];
            icon.className = `fas ${config.icon} ${config.class} me-2`;
            text.textContent = status;
        }

        // Show status bar for non-success states
        if (type !== 'success') {
            this.statusBar.classList.remove('d-none');
        } else {
            setTimeout(() => {
                this.statusBar.classList.add('d-none');
            }, 3000);
        }
    }

    /**
     * Update queue and pending counts
     */
    updateCounts(queueCount, pendingCount) {
        const queueElement = document.getElementById('queue-count');
        const pendingElement = document.getElementById('pending-count');

        if (queueElement) queueElement.textContent = queueCount || 0;
        if (pendingElement) pendingElement.textContent = pendingCount || 0;
    }

    /**
     * Get position classes for container
     */
    getPositionClasses() {
        const positionMap = {
            'top-start': 'top-0 start-0',
            'top-center': 'top-0 start-50 translate-middle-x',
            'top-end': 'top-0 end-0',
            'bottom-start': 'bottom-0 start-0',
            'bottom-center': 'bottom-0 start-50 translate-middle-x',
            'bottom-end': 'bottom-0 end-0'
        };

        return positionMap[this.config.position] || positionMap['top-end'];
    }

    // Convenience methods for different notification types

    showSuccess(message, options = {}) {
        return this.show({ ...options, type: 'success', message, soundType: 'success' });
    }

    showError(message, options = {}) {
        return this.show({ ...options, type: 'error', message, soundType: 'error' });
    }

    showWarning(message, options = {}) {
        return this.show({ ...options, type: 'warning', message, soundType: 'warning' });
    }

    showInfo(message, options = {}) {
        return this.show({ ...options, type: 'info', message, soundType: 'default' });
    }

    showPaymentNotification(message, options = {}) {
        return this.show({ ...options, type: 'payment', message, soundType: 'new-item' });
    }

    showQueueNotification(message, options = {}) {
        return this.show({ ...options, type: 'queue', message, soundType: 'default' });
    }

    showCriticalAlert(message, options = {}) {
        return this.show({
            ...options,
            type: 'error',
            priority: 'Critical',
            message,
            soundType: 'critical',
            autoDismiss: false,
            actions: [
                { id: 'acknowledge', label: 'Acknowledge', dismiss: true }
            ]
        });
    }
}

// Global instance
window.NotificationSystem = NotificationSystem;

// Auto-initialize if container exists
document.addEventListener('DOMContentLoaded', () => {
    if (!window.notificationSystem) {
        window.notificationSystem = new NotificationSystem();
    }
});