/**
 * Driver Dashboard JavaScript Controller
 * Handles real-time updates, queue management, and ride completion for drivers.
 */
class DriverDashboard {
    constructor() {
        this.connection = null;
        this.currentCustomer = null;
        this.isConnected = false;
        this.heartbeatInterval = null;
        this.autoRefreshInterval = null;
        this.sessionStats = {
            ridesCompleted: 0,
            sessionStart: new Date(),
            averageRideTime: 0
        };

        // Bind methods to preserve 'this' context
        this.handleKeyPress = this.handleKeyPress.bind(this);
        this.completeCurrentRide = this.completeCurrentRide.bind(this);
        this.refreshDashboard = this.refreshDashboard.bind(this);
    }

    /**
     * Initializes the driver dashboard
     */
    async initialize() {
        console.log('Initializing Driver Dashboard...');

        this.setupEventListeners();
        this.setupKeyboardShortcuts();
        await this.initializeSignalR();
        await this.loadInitialData();
        this.startAutoRefresh();

        console.log('Driver Dashboard initialized successfully');
    }

    /**
     * Sets up event listeners for UI interactions
     */
    setupEventListeners() {
        // Complete ride button
        document.addEventListener('click', (e) => {
            if (e.target.id === 'completeCurrentRideBtn' || e.target.closest('#completeCurrentRideBtn')) {
                e.preventDefault();
                this.completeCurrentRide();
            }
        });

        // Refresh button
        const refreshBtn = document.getElementById('refreshBtn');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => this.refreshDashboard());
        }

        // Emergency button
        const emergencyBtn = document.getElementById('emergencyBtn');
        if (emergencyBtn) {
            emergencyBtn.addEventListener('click', () => this.handleEmergency());
        }

        // Auto-focus on complete button when customer changes
        this.setupAutoFocus();
    }

    /**
     * Sets up keyboard shortcuts for driver efficiency
     */
    setupKeyboardShortcuts() {
        document.addEventListener('keydown', this.handleKeyPress);
    }

    /**
     * Handles keyboard shortcuts
     */
    handleKeyPress(event) {
        // Ignore if user is typing in input fields
        if (event.target.tagName === 'INPUT' || event.target.tagName === 'TEXTAREA') {
            return;
        }

        switch (event.key) {
            case ' ': // Space
            case 'Enter':
                event.preventDefault();
                this.completeCurrentRide();
                break;
            case 'F5':
                event.preventDefault();
                this.refreshDashboard();
                break;
            case 'F1':
                event.preventDefault();
                this.handleEmergency();
                break;
            case 'Escape':
                event.preventDefault();
                this.clearNotifications();
                break;
        }
    }

    /**
     * Initializes SignalR connection for real-time updates
     */
    async initializeSignalR() {
        try {
            // Initialize SignalR connection
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl('/queueHub')
                .withAutomaticReconnect([0, 2000, 10000, 30000, 60000])
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Setup connection event handlers
            this.setupSignalREventHandlers();

            // Start connection
            await this.connection.start();

            // Join driver group for targeted notifications
            await this.connection.invoke('JoinGroup', 'drivers');

            this.updateConnectionStatus('connected', 'Connected');
            console.log('SignalR connected successfully');

            // Start heartbeat monitoring
            this.startHeartbeat();

        } catch (error) {
            console.error('SignalR connection failed:', error);
            this.updateConnectionStatus('disconnected', 'Connection Failed');

            // Retry connection after delay
            setTimeout(() => this.initializeSignalR(), 5000);
        }
    }

    /**
     * Sets up SignalR event handlers for real-time updates
     */
    setupSignalREventHandlers() {
        // Queue update notifications
        this.connection.on('QueueUpdated', (notification) => {
            console.log('Queue updated:', notification);
            this.handleQueueUpdate(notification);
        });

        // Ride completion notifications
        this.connection.on('RideCompleted', (notification) => {
            console.log('Ride completed:', notification);
            this.handleRideCompletion(notification);
        });

        // Customer notifications
        this.connection.on('CustomerNotification', (notification) => {
            console.log('Customer notification:', notification);
            this.handleCustomerNotification(notification);
        });

        // System notifications
        this.connection.on('SystemNotification', (notification) => {
            console.log('System notification:', notification);
            this.showNotification(notification.message, notification.type);
        });

        // Connection state change handlers
        this.connection.onreconnecting(() => {
            console.log('SignalR reconnecting...');
            this.updateConnectionStatus('connecting', 'Reconnecting...');
        });

        this.connection.onreconnected(() => {
            console.log('SignalR reconnected');
            this.updateConnectionStatus('connected', 'Connected');
            this.refreshDashboard();
        });

        this.connection.onclose(() => {
            console.log('SignalR connection closed');
            this.updateConnectionStatus('disconnected', 'Disconnected');
        });
    }

    /**
     * Starts heartbeat monitoring for connection health
     */
    startHeartbeat() {
        this.heartbeatInterval = setInterval(async () => {
            if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
                try {
                    await this.connection.invoke('Heartbeat');
                } catch (error) {
                    console.warn('Heartbeat failed:', error);
                }
            }
        }, 30000); // 30 second heartbeat
    }

    /**
     * Loads initial dashboard data
     */
    async loadInitialData() {
        try {
            await this.loadCurrentCustomer();
            await this.loadQueueStatus();
            this.updateLastUpdated();
        } catch (error) {
            console.error('Failed to load initial data:', error);
            this.showNotification('Failed to load dashboard data', 'error');
        }
    }

    /**
     * Loads current customer data from API
     */
    async loadCurrentCustomer() {
        try {
            const response = await fetch('/Driver/GetCurrentCustomer');
            const result = await response.json();

            if (result.success) {
                this.currentCustomer = result.customer;
                this.updateCurrentCustomerDisplay();
                this.updateCompleteButton();
            } else {
                console.error('Failed to load current customer:', result.error);
                this.showNoCustomer();
            }
        } catch (error) {
            console.error('Error loading current customer:', error);
            this.showNotification('Connection error', 'error');
        }
    }

    /**
     * Loads queue status and statistics
     */
    async loadQueueStatus() {
        try {
            const response = await fetch('/Driver/GetQueueStatus');
            const result = await response.json();

            if (result.success) {
                this.updateQueueDisplay(result.queue);
            } else {
                console.error('Failed to load queue status:', result.error);
            }
        } catch (error) {
            console.error('Error loading queue status:', error);
        }
    }

    /**
     * Completes the current ride
     */
    async completeCurrentRide() {
        if (!this.currentCustomer) {
            this.showNotification('No customer to complete', 'warning');
            return;
        }

        const completeBtn = document.getElementById('completeCurrentRideBtn');
        if (completeBtn) {
            completeBtn.disabled = true;
            completeBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Completing...';
        }

        try {
            const startTime = Date.now();

            const response = await fetch('/Driver/CompleteRide', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    queueEntryId: this.currentCustomer.queueEntryId
                })
            });

            const result = await response.json();

            if (result.success) {
                const rideTime = Date.now() - startTime;
                this.updateSessionStats(rideTime);

                // Show completion modal
                this.showCompletionModal(this.currentCustomer.customerName);

                // Auto-refresh to get next customer
                setTimeout(() => {
                    this.loadCurrentCustomer();
                    this.loadQueueStatus();
                }, 1500);

                this.showNotification('Ride completed successfully!', 'success');

            } else {
                console.error('Failed to complete ride:', result.error);
                this.showNotification(result.error || 'Failed to complete ride', 'error');
            }
        } catch (error) {
            console.error('Error completing ride:', error);
            this.showNotification('Connection error during completion', 'error');
        } finally {
            // Re-enable button
            if (completeBtn) {
                completeBtn.disabled = false;
                this.updateCompleteButton();
            }
        }
    }

    /**
     * Updates the current customer display
     */
    updateCurrentCustomerDisplay() {
        const container = document.getElementById('currentCustomerContent');
        if (!container) return;

        if (this.currentCustomer) {
            // Build customer display HTML
            const customerHtml = this.buildCurrentCustomerHtml(this.currentCustomer);
            container.innerHTML = customerHtml;

            // Setup auto-focus
            this.setupAutoFocus();
        } else {
            this.showNoCustomer();
        }
    }

    /**
     * Builds HTML for current customer display
     */
    buildCurrentCustomerHtml(customer) {
        const waitTime = this.calculateWaitTime(customer.queuedAt);
        const isLongWait = waitTime.totalMinutes > 10;
        const isVeryLongWait = waitTime.totalMinutes > 20;

        return `
            <div class="current-customer-display" data-queue-entry-id="${customer.queueEntryId}">
                <div class="customer-name-section">
                    <h2 class="customer-name-large">${customer.customerName}</h2>
                    ${customer.customerName !== customer.displayName ?
                        `<div class="disambiguation-time">
                            <i class="fas fa-clock"></i>
                            Arrived at ${customer.arrivalTimeDisplay}
                        </div>` : ''}
                </div>

                <div class="customer-details-grid">
                    <div class="detail-item">
                        <div class="detail-label">Position</div>
                        <div class="detail-value">#${customer.position}</div>
                    </div>

                    <div class="detail-item">
                        <div class="detail-label">Wait Time</div>
                        <div class="detail-value ${isLongWait ? 'text-warning' : ''} ${isVeryLongWait ? 'text-danger' : ''}">
                            ${waitTime.display}
                            ${isVeryLongWait ? '<i class="fas fa-exclamation-triangle text-danger ms-1"></i>' : ''}
                        </div>
                    </div>

                    <div class="detail-item">
                        <div class="detail-label">Payment</div>
                        <div class="detail-value">
                            ${customer.paymentMethod}
                            <small class="d-block text-muted">$${customer.paymentAmount.toFixed(2)}</small>
                        </div>
                    </div>

                    ${customer.phoneNumber ? `
                        <div class="detail-item">
                            <div class="detail-label">Contact</div>
                            <div class="detail-value">
                                <a href="tel:${customer.phoneNumber}" class="text-decoration-none">
                                    <i class="fas fa-phone"></i>
                                    ${customer.phoneNumber}
                                </a>
                            </div>
                        </div>
                    ` : ''}
                </div>

                ${isVeryLongWait ? `
                    <div class="alert alert-warning text-center">
                        <i class="fas fa-clock"></i>
                        <strong>Extended Wait Time:</strong> This customer has been waiting over 20 minutes.
                        Consider prioritizing or checking for issues.
                    </div>
                ` : isLongWait ? `
                    <div class="alert alert-info text-center">
                        <i class="fas fa-info-circle"></i>
                        Customer has been waiting over 10 minutes.
                    </div>
                ` : ''}

                <div class="action-section mt-4">
                    <button id="completeCurrentRideBtn"
                            class="btn complete-ride-btn btn-lg w-100"
                            data-queue-entry-id="${customer.queueEntryId}"
                            data-customer-name="${customer.customerName}">
                        <i class="fas fa-check-circle"></i>
                        Complete Ride for ${customer.customerName}
                    </button>

                    <div class="keyboard-hint text-center mt-2">
                        <small class="text-muted">
                            Press <kbd>Space</kbd> or <kbd>Enter</kbd> to complete ride
                        </small>
                    </div>
                </div>
            </div>
        `;
    }

    /**
     * Shows no customer display when queue is empty
     */
    showNoCustomer() {
        const container = document.getElementById('currentCustomerContent');
        if (container) {
            container.innerHTML = `
                <div class="no-customers text-center py-5">
                    <i class="fas fa-hourglass-end fa-3x text-muted mb-3"></i>
                    <h5 class="text-muted">No customers in queue</h5>
                    <p class="text-muted">Waiting for customers to join the queue...</p>
                </div>
            `;
        }

        this.currentCustomer = null;
        this.updateCompleteButton();
    }

    /**
     * Updates the complete ride button state
     */
    updateCompleteButton() {
        const btn = document.getElementById('completeRideBtn');
        if (btn) {
            btn.disabled = !this.currentCustomer;
        }

        const currentBtn = document.getElementById('completeCurrentRideBtn');
        if (currentBtn) {
            currentBtn.disabled = !this.currentCustomer;
        }
    }

    /**
     * Updates queue display with latest data
     */
    updateQueueDisplay(queue) {
        // Update queue length
        const queueLengthEl = document.getElementById('queueLength');
        if (queueLengthEl) {
            queueLengthEl.textContent = queue.filter(c => c.status === 'Waiting').length;
        }

        // Update upcoming customers
        const upcomingContainer = document.getElementById('upcomingCustomers');
        if (upcomingContainer) {
            const waitingCustomers = queue
                .filter(c => c.status === 'Waiting' && c.position > 1)
                .sort((a, b) => a.position - b.position)
                .slice(0, 3);

            if (waitingCustomers.length > 0) {
                upcomingContainer.innerHTML = waitingCustomers
                    .map(customer => this.buildUpcomingCustomerHtml(customer))
                    .join('');
            } else {
                upcomingContainer.innerHTML = `
                    <div class="no-upcoming text-center p-3">
                        <i class="fas fa-inbox text-muted"></i>
                        <p class="text-muted mb-0">No upcoming customers</p>
                    </div>
                `;
            }
        }
    }

    /**
     * Builds HTML for upcoming customer item
     */
    buildUpcomingCustomerHtml(customer) {
        const waitTime = this.calculateWaitTime(customer.queuedAt);
        const isLongWait = waitTime.totalMinutes > 10;

        return `
            <div class="upcoming-customer-item">
                <div class="customer-info">
                    <div class="customer-name">
                        ${customer.displayName}
                        ${customer.customerName !== customer.displayName ?
                            `<small class="text-muted">(${customer.arrivalTimeDisplay})</small>` : ''}
                    </div>
                    <div class="customer-details">
                        <span class="position-badge">Position ${customer.position}</span>
                        <span class="wait-time ${isLongWait ? 'long-wait' : ''}">
                            ${waitTime.display}
                        </span>
                    </div>
                </div>
            </div>
        `;
    }

    /**
     * Calculates wait time from queued timestamp
     */
    calculateWaitTime(queuedAt) {
        const now = new Date();
        const queued = new Date(queuedAt);
        const diffMs = now - queued;
        const diffMinutes = Math.floor(diffMs / 60000);

        let display;
        if (diffMinutes < 1) {
            display = "< 1 min";
        } else if (diffMinutes < 60) {
            display = `${diffMinutes} min`;
        } else {
            const hours = Math.floor(diffMinutes / 60);
            const mins = diffMinutes % 60;
            display = `${hours}h ${mins}m`;
        }

        return {
            totalMinutes: diffMinutes,
            display: display
        };
    }

    /**
     * Updates connection status indicator
     */
    updateConnectionStatus(status, text) {
        const statusEl = document.getElementById('connectionStatus');
        const textEl = document.getElementById('connectionText');

        if (statusEl && textEl) {
            statusEl.className = `status-indicator ${status}`;
            textEl.textContent = text;
        }

        this.isConnected = status === 'connected';
    }

    /**
     * Updates last updated timestamp
     */
    updateLastUpdated() {
        const lastUpdatedEl = document.getElementById('lastUpdated');
        if (lastUpdatedEl) {
            lastUpdatedEl.textContent = new Date().toLocaleTimeString();
        }
    }

    /**
     * Shows completion modal with customer name
     */
    showCompletionModal(customerName) {
        const modal = document.getElementById('completionModal');
        const nameEl = document.getElementById('completedCustomerName');

        if (modal && nameEl) {
            nameEl.textContent = `${customerName}'s ride completed!`;

            // Show modal (Bootstrap)
            if (typeof bootstrap !== 'undefined') {
                const modalInstance = new bootstrap.Modal(modal);
                modalInstance.show();

                // Auto-hide after 2 seconds
                setTimeout(() => modalInstance.hide(), 2000);
            }
        }
    }

    /**
     * Shows notification message
     */
    showNotification(message, type = 'info') {
        // Implementation would depend on your notification system
        console.log(`${type.toUpperCase()}: ${message}`);

        // You could integrate with a toast library here
        // For now, just show in console
    }

    /**
     * Handles emergency button press
     */
    handleEmergency() {
        this.showNotification('Emergency assistance requested', 'warning');
        // In real implementation, this would alert sales staff
    }

    /**
     * Clears all notifications
     */
    clearNotifications() {
        // Clear any visible notifications
        console.log('Notifications cleared');
    }

    /**
     * Sets up auto-focus for accessibility
     */
    setupAutoFocus() {
        // Auto-focus the complete button for keyboard users
        setTimeout(() => {
            const completeBtn = document.getElementById('completeCurrentRideBtn');
            if (completeBtn && !completeBtn.disabled) {
                completeBtn.focus();
            }
        }, 100);
    }

    /**
     * Updates session statistics
     */
    updateSessionStats(rideTimeMs) {
        this.sessionStats.ridesCompleted++;

        // Update average ride time
        const totalRideTime = this.sessionStats.averageRideTime * (this.sessionStats.ridesCompleted - 1) + rideTimeMs;
        this.sessionStats.averageRideTime = totalRideTime / this.sessionStats.ridesCompleted;

        // Update display
        const ridesCompletedEl = document.querySelector('.session-stats .stat-value');
        if (ridesCompletedEl) {
            ridesCompletedEl.textContent = this.sessionStats.ridesCompleted;
        }
    }

    /**
     * Starts auto-refresh timer
     */
    startAutoRefresh() {
        this.autoRefreshInterval = setInterval(() => {
            if (!this.isConnected) {
                this.refreshDashboard();
            }
        }, 30000); // Refresh every 30 seconds when not connected to SignalR
    }

    /**
     * Refreshes dashboard data manually
     */
    async refreshDashboard() {
        console.log('Refreshing dashboard...');
        await this.loadCurrentCustomer();
        await this.loadQueueStatus();
        this.updateLastUpdated();
        this.showNotification('Dashboard refreshed', 'success');
    }

    /**
     * Handles SignalR queue update notifications
     */
    handleQueueUpdate(notification) {
        console.log('Queue update received:', notification);

        // Refresh current customer and queue status
        this.loadCurrentCustomer();
        this.loadQueueStatus();
        this.updateLastUpdated();

        // Show notification based on update type
        switch (notification.updateType) {
            case 'CustomerAdded':
                this.showNotification('New customer added to queue', 'info');
                break;
            case 'QueueReordered':
                this.showNotification('Queue has been reordered', 'info');
                break;
            case 'RideCompleted':
                this.showNotification('Ride completed - queue updated', 'success');
                break;
        }
    }

    /**
     * Handles ride completion notifications
     */
    handleRideCompletion(notification) {
        // If this driver completed the ride, update stats
        if (notification.driverUsername === 'current-driver') { // In real app, check actual driver
            this.updateSessionStats(0); // Would get actual ride time from notification
        }

        // Refresh queue regardless
        this.loadCurrentCustomer();
        this.loadQueueStatus();
    }

    /**
     * Handles customer-specific notifications
     */
    handleCustomerNotification(notification) {
        this.showNotification(notification.message, notification.priority.toLowerCase());
    }

    /**
     * Cleanup method for proper disposal
     */
    dispose() {
        // Clear intervals
        if (this.heartbeatInterval) {
            clearInterval(this.heartbeatInterval);
        }

        if (this.autoRefreshInterval) {
            clearInterval(this.autoRefreshInterval);
        }

        // Remove event listeners
        document.removeEventListener('keydown', this.handleKeyPress);

        // Close SignalR connection
        if (this.connection) {
            this.connection.stop();
        }
    }
}

// Export for use in other scripts
window.DriverDashboard = DriverDashboard;