/**
 * Sales Dashboard JavaScript Controller
 * Handles payment verification, queue management, and real-time updates
 * Optimized for 30-second confirmation target with keyboard shortcuts
 */

class SalesDashboard {
    constructor(config) {
        this.config = config;
        this.connection = null;
        this.selectedPaymentId = null;
        this.soundsEnabled = config.soundAlertsEnabled;
        this.keyboardEnabled = config.keyboardShortcutsEnabled;
        this.refreshTimer = null;
        this.lastUpdateTime = null;

        this.init();
    }

    async init() {
        console.log('Initializing Sales Dashboard...');

        // Initialize components
        this.setupEventHandlers();
        this.setupKeyboardShortcuts();
        this.setupSignalR();
        this.startAutoRefresh();
        this.updateCurrentTime();

        // Focus on first payment if available
        this.focusFirstPayment();

        console.log('Sales Dashboard initialized successfully');
    }

    setupEventHandlers() {
        // Payment confirmation buttons
        $(document).on('click', '.confirm-payment', (e) => {
            const paymentId = $(e.currentTarget).data('payment-id');
            const customerName = $(e.currentTarget).data('customer-name');
            this.confirmPayment(paymentId, true, `Payment verified for ${customerName}`);
        });

        $(document).on('click', '.deny-payment', (e) => {
            const paymentId = $(e.currentTarget).data('payment-id');
            const customerName = $(e.currentTarget).data('customer-name');
            this.showDenyDialog(paymentId, customerName);
        });

        // Payment item selection
        $(document).on('click', '.payment-item', (e) => {
            if (!$(e.target).closest('.btn-group').length) {
                this.selectPayment($(e.currentTarget).data('payment-id'));
            }
        });

        // Manual customer form
        $('#manual-customer-form').on('submit', (e) => {
            e.preventDefault();
            this.addCustomerManually();
        });

        // Reason dropdown
        $('#manual-reason-select').on('change', (e) => {
            const selectedValue = $(e.target).val();
            if (selectedValue && selectedValue !== 'custom') {
                $('#manual-reason').val(selectedValue);
            } else if (selectedValue === 'custom') {
                $('#manual-reason').val('').focus();
            }
        });

        // Customer search
        $('#search-input').on('input', this.debounce((e) => {
            const searchTerm = $(e.target).val().trim();
            if (searchTerm.length >= 2) {
                this.searchCustomers(searchTerm);
            } else {
                $('#search-results').empty();
            }
        }, 300));

        $('#search-btn').on('click', () => {
            const searchTerm = $('#search-input').val().trim();
            if (searchTerm.length >= 2) {
                this.searchCustomers(searchTerm);
            }
        });

        // Control buttons
        $('#refresh-data').on('click', () => this.refreshData());
        $('#toggle-sounds').on('click', () => this.toggleSounds());
        $('#bulk-approve').on('click', () => this.bulkApproveSelected());
        $('#next-payment').on('click', () => this.focusNextPayment());
        $('#filter-urgent').on('click', () => this.filterUrgentPayments());

        // Tab switching
        $('[data-bs-toggle="tab"]').on('shown.bs.tab', (e) => {
            const targetTab = $(e.target).attr('data-bs-target');
            if (targetTab === '#pending-payments') {
                this.focusFirstPayment();
            }
        });
    }

    setupKeyboardShortcuts() {
        if (!this.keyboardEnabled) return;

        $(document).on('keydown', (e) => {
            // Only handle shortcuts when not in input fields
            if ($(e.target).is('input, textarea, select')) {
                return;
            }

            switch (e.key.toLowerCase()) {
                case 'a':
                    e.preventDefault();
                    if (this.selectedPaymentId) {
                        this.confirmPayment(this.selectedPaymentId, true, 'Quick approval via keyboard');
                    }
                    break;

                case 'd':
                    e.preventDefault();
                    if (this.selectedPaymentId) {
                        this.confirmPayment(this.selectedPaymentId, false, 'Quick denial via keyboard');
                    }
                    break;

                case ' ':
                    e.preventDefault();
                    this.focusNextPayment();
                    break;

                case 'escape':
                    e.preventDefault();
                    this.clearSelection();
                    break;

                case 'f5':
                    e.preventDefault();
                    this.refreshData();
                    break;

                case 'u':
                    e.preventDefault();
                    this.filterUrgentPayments();
                    break;

                case 't':
                    e.preventDefault();
                    this.switchToNextTab();
                    break;

                case '?':
                    e.preventDefault();
                    this.showKeyboardHelp();
                    break;
            }

            // Ctrl combinations
            if (e.ctrlKey) {
                switch (e.key.toLowerCase()) {
                    case 'a':
                        e.preventDefault();
                        this.bulkApproveSelected();
                        break;

                    case 'l':
                        e.preventDefault();
                        $('#logout-form').submit();
                        break;
                }
            }

            // Function keys
            if (e.key === 'F8') {
                e.preventDefault();
                this.toggleSounds();
            }
        });
    }

    async setupSignalR() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(this.config.signalRHubUrl)
                .withAutomaticReconnect([0, 2000, 10000, 30000, 60000])
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Handle connection events
            this.connection.onreconnecting(() => {
                this.updateConnectionStatus('Reconnecting...', 'warning');
                this.showNotification('Connection lost, attempting to reconnect...', 'warning');
            });

            this.connection.onreconnected(() => {
                this.updateConnectionStatus('Connected', 'success');
                this.showNotification('Connection restored', 'success');
                this.refreshData();
                this.rejoinGroups();
            });

            this.connection.onclose(() => {
                this.updateConnectionStatus('Disconnected', 'danger');
                this.showNotification('Connection lost. Real-time updates unavailable.', 'error');
            });

            // Enhanced real-time notification handlers
            this.connection.on('PaymentStatusUpdated', (notification) => {
                this.handlePaymentStatusUpdate(notification);
            });

            this.connection.on('QueueUpdated', (notification) => {
                this.handleQueueUpdate(notification);
            });

            this.connection.on('NewPayment', (notification) => {
                this.handleNewPayment(notification);
            });

            this.connection.on('CustomerAlert', (notification) => {
                this.handleCustomerAlert(notification);
            });

            this.connection.on('QueueStatisticsUpdated', (notification) => {
                this.handleQueueStatisticsUpdate(notification);
            });

            this.connection.on('ConfigurationChanged', (notification) => {
                this.handleConfigurationChange(notification);
            });

            this.connection.on('ServiceIssue', (notification) => {
                this.handleServiceIssue(notification);
            });

            // Connection monitoring
            this.connection.on('HeartbeatResponse', (timestamp) => {
                this.handleHeartbeatResponse(timestamp);
            });

            // Start connection
            await this.connection.start();
            this.updateConnectionStatus('Connected', 'success');

            // Join sales group for targeted notifications
            await this.connection.invoke('JoinGroup', 'Role_Sales');

            // Start heartbeat monitoring
            this.startHeartbeat();

        } catch (error) {
            console.error('SignalR connection failed:', error);
            this.updateConnectionStatus('Connection Failed', 'danger');
            this.showNotification('Failed to establish real-time connection', 'error');
            this.retryConnection();
        }
    }

    startAutoRefresh() {
        this.refreshTimer = setInterval(() => {
            this.refreshData();
        }, this.config.refreshInterval);
    }

    updateCurrentTime() {
        const updateTime = () => {
            const now = new Date();
            $('#current-time').text(now.toLocaleTimeString());
        };

        updateTime();
        setInterval(updateTime, 1000);
    }

    // Payment Management
    async confirmPayment(paymentId, confirmed, notes = '') {
        try {
            const response = await $.ajax({
                url: '/Sales/ConfirmPayment',
                method: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({
                    paymentId: paymentId,
                    confirmed: confirmed,
                    notes: notes
                }),
                headers: {
                    'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                }
            });

            if (response.success) {
                this.showNotification(
                    `Payment ${confirmed ? 'approved' : 'denied'} successfully`,
                    'success'
                );

                // Remove payment from list
                $(`.payment-item[data-payment-id="${paymentId}"]`).fadeOut(300, function() {
                    $(this).remove();
                });

                // Update counters
                this.updatePendingCounters();

                // Focus next payment
                setTimeout(() => this.focusNextPayment(), 100);

                // Play success sound
                if (this.soundsEnabled) {
                    this.playNotificationSound();
                }

            } else {
                this.showNotification(response.message || 'Operation failed', 'error');
            }
        } catch (error) {
            console.error('Error confirming payment:', error);
            this.showNotification('Unable to process payment confirmation', 'error');
        }
    }

    showDenyDialog(paymentId, customerName) {
        const modal = $(`
            <div class="modal fade" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">Deny Payment - ${customerName}</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div class="mb-3">
                                <label class="form-label">Reason for denial:</label>
                                <select class="form-select" id="deny-reason-select">
                                    <option value="">Select a reason...</option>
                                    <option value="Payment amount insufficient">Payment amount insufficient</option>
                                    <option value="Payment not received">Payment not received</option>
                                    <option value="Invalid transaction ID">Invalid transaction ID</option>
                                    <option value="Duplicate payment attempt">Duplicate payment attempt</option>
                                    <option value="Customer not present">Customer not present</option>
                                    <option value="Payment method not accepted">Payment method not accepted</option>
                                    <option value="custom">Other (specify below)</option>
                                </select>
                            </div>
                            <div class="mb-3">
                                <textarea class="form-control" id="deny-notes" rows="3"
                                         placeholder="Additional details..."></textarea>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-danger" id="confirm-deny">Deny Payment</button>
                        </div>
                    </div>
                </div>
            </div>
        `);

        modal.find('#deny-reason-select').on('change', function() {
            const selectedValue = $(this).val();
            if (selectedValue && selectedValue !== 'custom') {
                modal.find('#deny-notes').val(selectedValue);
            } else if (selectedValue === 'custom') {
                modal.find('#deny-notes').val('').focus();
            }
        });

        modal.find('#confirm-deny').on('click', () => {
            const notes = modal.find('#deny-notes').val().trim();
            if (!notes) {
                this.showNotification('Please provide a reason for denial', 'warning');
                return;
            }

            this.confirmPayment(paymentId, false, notes);
            modal.modal('hide');
        });

        $('body').append(modal);
        modal.modal('show');
        modal.on('hidden.bs.modal', () => modal.remove());
    }

    // Manual Customer Addition
    async addCustomerManually() {
        const name = $('#manual-name').val().trim();
        const phone = $('#manual-phone').val().trim();
        const reason = $('#manual-reason').val().trim();

        if (!name || !phone || !reason) {
            this.showNotification('Please fill in all fields', 'warning');
            return;
        }

        try {
            const response = await $.ajax({
                url: '/Sales/AddCustomerManually',
                method: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({
                    name: name,
                    phoneNumber: phone,
                    reason: reason
                }),
                headers: {
                    'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                }
            });

            if (response.success) {
                this.showNotification(
                    `Customer ${name} added successfully`,
                    'success'
                );

                // Clear form
                $('#manual-customer-form')[0].reset();
                $('#manual-reason-select').val('');

                // Switch to queue tab to show the addition
                $('#queue-tab').tab('show');

                // Refresh queue data
                this.refreshQueueData();

            } else {
                this.showNotification(response.message || 'Failed to add customer', 'error');
            }
        } catch (error) {
            console.error('Error adding customer manually:', error);
            this.showNotification('Unable to add customer', 'error');
        }
    }

    // Customer Search
    async searchCustomers(searchTerm) {
        try {
            const response = await $.ajax({
                url: '/Sales/SearchCustomers',
                method: 'GET',
                data: { searchTerm: searchTerm }
            });

            if (response.success && response.data) {
                this.displaySearchResults(response.data, searchTerm);
            } else {
                $('#search-results').html('<p class="text-muted">No customers found.</p>');
            }
        } catch (error) {
            console.error('Error searching customers:', error);
            $('#search-results').html('<p class="text-danger">Search failed. Please try again.</p>');
        }
    }

    displaySearchResults(customers, searchTerm) {
        if (!customers || customers.length === 0) {
            $('#search-results').html('<p class="text-muted">No customers found.</p>');
            return;
        }

        const resultsHtml = customers.map(customer => `
            <div class="card mb-2">
                <div class="card-body py-2">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <h6 class="mb-1">${customer.name}</h6>
                            <small class="text-muted">${customer.phoneNumber}</small>
                            <small class="text-muted ms-2">Added: ${new Date(customer.createdAt).toLocaleDateString()}</small>
                        </div>
                        <div>
                            <button class="btn btn-sm btn-outline-primary"
                                    onclick="viewCustomerHistory(${customer.id})">
                                View History
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `).join('');

        $('#search-results').html(`
            <h6>Search Results for "${searchTerm}" (${customers.length} found)</h6>
            ${resultsHtml}
        `);
    }

    // Data Management
    async refreshData() {
        try {
            const response = await $.ajax({
                url: '/Sales/GetPendingPayments',
                method: 'GET'
            });

            if (response.success && response.data) {
                this.updatePendingPaymentsList(response.data);
                this.updatePendingCounters();
                this.lastUpdateTime = new Date();
            }

            // Also refresh queue data
            await this.refreshQueueData();

        } catch (error) {
            console.error('Error refreshing data:', error);
            this.showNotification('Unable to refresh data', 'error');
        }
    }

    async refreshQueueData() {
        try {
            const response = await $.ajax({
                url: '/Sales/GetQueueStatus',
                method: 'GET'
            });

            if (response.success && response.data) {
                this.updateQueueList(response.data);
            }
        } catch (error) {
            console.error('Error refreshing queue data:', error);
        }
    }

    updatePendingPaymentsList(payments) {
        // This would update the pending payments list
        // Implementation depends on the specific HTML structure
        console.log('Updating pending payments list:', payments);
    }

    updateQueueList(queueData) {
        // This would update the queue list
        // Implementation depends on the specific HTML structure
        console.log('Updating queue list:', queueData);
    }

    updatePendingCounters() {
        const totalPending = $('.payment-item:visible').length;
        const over5Minutes = $('.payment-item.moderate-payment:visible, .payment-item.urgent-payment:visible').length;
        const over10Minutes = $('.payment-item.urgent-payment:visible').length;

        $('#total-pending').text(totalPending);
        $('#pending-count').text(totalPending);
        $('#over-5-minutes').text(over5Minutes);
        $('#over-10-minutes').text(over10Minutes);
    }

    // UI Interactions
    selectPayment(paymentId) {
        $('.payment-item').removeClass('selected');
        $(`.payment-item[data-payment-id="${paymentId}"]`).addClass('selected');
        this.selectedPaymentId = paymentId;
    }

    focusFirstPayment() {
        const firstPayment = $('.payment-item:visible').first();
        if (firstPayment.length) {
            const paymentId = firstPayment.data('payment-id');
            this.selectPayment(paymentId);
            firstPayment[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    focusNextPayment() {
        const currentSelected = $('.payment-item.selected');
        const nextPayment = currentSelected.length
            ? currentSelected.next('.payment-item:visible')
            : $('.payment-item:visible').first();

        if (nextPayment.length) {
            const paymentId = nextPayment.data('payment-id');
            this.selectPayment(paymentId);
            nextPayment[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
        } else {
            // Wrap to first payment
            this.focusFirstPayment();
        }
    }

    clearSelection() {
        $('.payment-item').removeClass('selected');
        this.selectedPaymentId = null;
    }

    toggleSounds() {
        this.soundsEnabled = !this.soundsEnabled;
        const btn = $('#toggle-sounds');
        const icon = btn.find('i');

        if (this.soundsEnabled) {
            icon.removeClass('fa-volume-mute').addClass('fa-volume-up');
            btn.removeClass('btn-outline-secondary').addClass('btn-outline-light');
            this.showNotification('Sound alerts enabled', 'info');
        } else {
            icon.removeClass('fa-volume-up').addClass('fa-volume-mute');
            btn.removeClass('btn-outline-light').addClass('btn-outline-secondary');
            this.showNotification('Sound alerts disabled', 'info');
        }
    }

    filterUrgentPayments() {
        const urgentItems = $('.payment-item.urgent-payment');
        const allItems = $('.payment-item');

        if (urgentItems.filter(':visible').length === urgentItems.length) {
            // Currently showing all urgent, show all payments
            allItems.show();
            $('#filter-urgent').removeClass('btn-warning').addClass('btn-outline-light');
        } else {
            // Show only urgent payments
            allItems.hide();
            urgentItems.show();
            $('#filter-urgent').removeClass('btn-outline-light').addClass('btn-warning');
        }

        this.updatePendingCounters();
    }

    switchToNextTab() {
        const currentTab = $('.nav-tabs .nav-link.active');
        const nextTab = currentTab.parent().next().find('.nav-link');

        if (nextTab.length) {
            nextTab.tab('show');
        } else {
            // Wrap to first tab
            $('.nav-tabs .nav-link').first().tab('show');
        }
    }

    bulkApproveSelected() {
        const selectedItems = $('.payment-item.selected');
        if (selectedItems.length === 0) {
            this.showNotification('No payments selected', 'warning');
            return;
        }

        if (confirm(`Approve ${selectedItems.length} selected payment(s)?`)) {
            selectedItems.each((index, item) => {
                const paymentId = $(item).data('payment-id');
                setTimeout(() => {
                    this.confirmPayment(paymentId, true, 'Bulk approval');
                }, index * 100); // Stagger requests
            });
        }
    }

    // Real-time Updates
    handlePaymentStatusUpdate(notification) {
        console.log('Payment status update received:', notification);

        // Remove the payment from the list if it was confirmed/denied
        if (notification.status === 'Confirmed' || notification.status === 'Denied') {
            $(`.payment-item[data-payment-id="${notification.paymentId}"]`).fadeOut(300, function() {
                $(this).remove();
            });
            this.updatePendingCounters();
        }

        if (this.soundsEnabled) {
            this.playNotificationSound();
        }
    }

    handleQueueUpdate(notification) {
        console.log('Queue update received:', notification);
        this.refreshQueueData();
    }

    handleNewPayment(notification) {
        console.log('New payment received:', notification);

        this.showNotification(
            `New payment from ${notification.customerName}`,
            'info'
        );

        // Refresh pending payments to include the new one
        this.refreshData();

        if (this.soundsEnabled) {
            this.playNotificationSound();
        }
    }

    handleConfigurationChange(notification) {
        console.log('Configuration changed:', notification);

        const message = `Payment configuration updated: ${notification.displayName} ${notification.changeType.toLowerCase()} by ${notification.changedBy}`;
        this.showNotification(message, 'info');

        // If viewing payment configuration page, show reload prompt
        if (window.location.pathname.includes('/PaymentConfiguration')) {
            setTimeout(() => {
                if (confirm('Payment configuration was updated. Reload page to see changes?')) {
                    window.location.reload();
                }
            }, 1500);
        }

        if (this.soundsEnabled) {
            this.playNotificationSound();
        }
    }

    // Utility Functions
    updateConnectionStatus(message, type) {
        const statusDiv = $('#connection-status');
        const textSpan = $('#connection-text');

        statusDiv.removeClass('alert-info alert-success alert-warning alert-danger')
                 .addClass(`alert-${type}`);
        textSpan.text(message);

        if (type === 'success') {
            statusDiv.fadeIn().delay(3000).fadeOut();
        } else {
            statusDiv.fadeIn();
        }
    }

    showNotification(message, type = 'info') {
        // Create toast notification
        const toast = $(`
            <div class="toast align-items-center text-white bg-${type === 'error' ? 'danger' : type} border-0" role="alert">
                <div class="d-flex">
                    <div class="toast-body">
                        ${message}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
                </div>
            </div>
        `);

        // Add to toast container (create if doesn't exist)
        let toastContainer = $('.toast-container');
        if (!toastContainer.length) {
            toastContainer = $('<div class="toast-container position-fixed top-0 end-0 p-3"></div>');
            $('body').append(toastContainer);
        }

        toastContainer.append(toast);

        // Show toast
        const bsToast = new bootstrap.Toast(toast[0]);
        bsToast.show();

        // Remove from DOM after hiding
        toast.on('hidden.bs.toast', () => toast.remove());
    }

    playNotificationSound() {
        const audio = document.getElementById('notification-sound');
        if (audio) {
            audio.play().catch(e => console.log('Could not play notification sound:', e));
        }
    }

    showKeyboardHelp() {
        const helpModal = $(`
            <div class="modal fade" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">Keyboard Shortcuts</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div class="row">
                                <div class="col-6">
                                    <h6>Payment Actions</h6>
                                    <p><kbd>A</kbd> - Approve selected payment</p>
                                    <p><kbd>D</kbd> - Deny selected payment</p>
                                    <p><kbd>Space</kbd> - Focus next payment</p>
                                    <p><kbd>Esc</kbd> - Clear selection</p>
                                </div>
                                <div class="col-6">
                                    <h6>Navigation</h6>
                                    <p><kbd>T</kbd> - Switch to next tab</p>
                                    <p><kbd>F5</kbd> - Refresh data</p>
                                    <p><kbd>U</kbd> - Filter urgent payments</p>
                                    <p><kbd>F8</kbd> - Toggle sound alerts</p>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-12">
                                    <h6>Bulk Actions</h6>
                                    <p><kbd>Ctrl+A</kbd> - Bulk approve selected</p>
                                    <p><kbd>Ctrl+L</kbd> - Logout</p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);

        $('body').append(helpModal);
        helpModal.modal('show');
        helpModal.on('hidden.bs.modal', () => helpModal.remove());
    }

    debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    // Enhanced SignalR notification handlers

    /**
     * Handle customer alert notifications
     */
    handleCustomerAlert(notification) {
        console.log('Customer alert received:', notification);

        // Show high-priority notification
        this.showNotification(
            `Customer Alert: ${notification.Message}`,
            'warning'
        );

        // Add visual indicator to interface
        this.addCustomerAlertIndicator(notification);

        // Play priority sound
        if (this.soundsEnabled) {
            this.playPrioritySound(notification.Priority);
        }

        // Update alert counters
        this.updateAlertCounters();
    }

    /**
     * Handle queue statistics updates
     */
    handleQueueStatisticsUpdate(notification) {
        console.log('Queue statistics update received:', notification);

        // Update dashboard statistics
        if (notification.TotalInQueue !== undefined) {
            $('#total-in-queue').text(notification.TotalInQueue);
        }

        if (notification.PendingPayments !== undefined) {
            $('#pending-count').text(notification.PendingPayments);
        }

        // Update average wait time display
        if (notification.AverageWaitTime) {
            const minutes = Math.round(notification.AverageWaitTime / 60000);
            $('#average-wait-time').text(`${minutes} min`);
        }

        // Update timestamp
        $('#last-updated').text(new Date(notification.UpdatedAt).toLocaleTimeString());
    }

    /**
     * Handle service issue notifications
     */
    handleServiceIssue(notification) {
        console.log('Service issue notification:', notification);

        const severityMap = {
            'Info': 'info',
            'Warning': 'warning',
            'Error': 'danger',
            'Critical': 'danger'
        };

        const alertClass = severityMap[notification.Severity] || 'info';

        // Show prominent notification for service issues
        this.showNotification(
            `Service Issue: ${notification.Message}`,
            alertClass === 'danger' ? 'error' : alertClass
        );

        // For critical issues, show modal
        if (notification.Severity === 'Critical') {
            this.showCriticalIssueModal(notification);
        }

        if (this.soundsEnabled && (notification.Severity === 'Error' || notification.Severity === 'Critical')) {
            this.playNotificationSound();
        }
    }

    /**
     * Start heartbeat monitoring
     */
    startHeartbeat() {
        if (this.heartbeatInterval) {
            clearInterval(this.heartbeatInterval);
        }

        this.heartbeatInterval = setInterval(() => {
            if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
                this.connection.invoke('Heartbeat');
            }
        }, 30000); // Send heartbeat every 30 seconds
    }

    /**
     * Handle heartbeat response
     */
    handleHeartbeatResponse(timestamp) {
        this.lastHeartbeat = new Date(timestamp);

        // Update connection health indicator
        const healthIndicator = $('#connection-health');
        if (healthIndicator.length) {
            healthIndicator.removeClass('text-danger text-warning').addClass('text-success');
            healthIndicator.attr('title', `Last heartbeat: ${this.lastHeartbeat.toLocaleTimeString()}`);
        }
    }

    /**
     * Rejoin groups after reconnection
     */
    rejoinGroups() {
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
            this.connection.invoke('JoinGroup', 'Role_Sales');
            console.log('Rejoined Sales group after reconnection');
        }
    }

    /**
     * Retry connection with exponential backoff
     */
    retryConnection() {
        if (this.retryAttempts >= 5) {
            this.showNotification('Connection failed permanently. Please refresh the page.', 'error');
            return;
        }

        const delay = Math.pow(2, this.retryAttempts) * 1000;
        this.retryAttempts++;

        setTimeout(() => {
            console.log(`Retrying SignalR connection attempt ${this.retryAttempts}`);
            this.setupSignalR();
        }, delay);
    }

    /**
     * Add visual indicator for customer alerts
     */
    addCustomerAlertIndicator(alert) {
        const alertsContainer = $('#customer-alerts-container');
        if (!alertsContainer.length) return;

        const alertElement = $(`
            <div class="alert alert-warning alert-dismissible fade show customer-alert" data-alert-id="${alert.CustomerId}">
                <i class="fas fa-exclamation-triangle me-2"></i>
                <strong>Customer Alert:</strong> ${alert.Message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `);

        alertsContainer.append(alertElement);

        // Auto-remove after 30 seconds if not manually dismissed
        setTimeout(() => {
            alertElement.fadeOut(() => alertElement.remove());
        }, 30000);
    }

    /**
     * Update alert counters
     */
    updateAlertCounters() {
        const alertCount = $('.customer-alert').length;
        $('#customer-alerts-count').text(alertCount);

        if (alertCount > 0) {
            $('#customer-alerts-badge').removeClass('d-none');
        } else {
            $('#customer-alerts-badge').addClass('d-none');
        }
    }

    /**
     * Play priority sound based on notification priority
     */
    playPrioritySound(priority) {
        if (!this.soundsEnabled) return;

        const soundMap = {
            'Critical': 'critical-sound',
            'Warning': 'warning-sound',
            'Error': 'error-sound',
            'Info': 'notification-sound'
        };

        const soundId = soundMap[priority] || 'notification-sound';
        const audio = document.getElementById(soundId);

        if (audio) {
            audio.play().catch(e => console.log('Could not play priority sound:', e));
        }
    }

    /**
     * Show critical issue modal
     */
    showCriticalIssueModal(issue) {
        const modal = $(`
            <div class="modal fade" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header bg-danger text-white">
                            <h5 class="modal-title">
                                <i class="fas fa-exclamation-triangle me-2"></i>
                                Critical System Issue
                            </h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <p><strong>Issue:</strong> ${issue.IssueType}</p>
                            <p><strong>Description:</strong> ${issue.Message}</p>
                            <p><strong>Time:</strong> ${new Date(issue.Timestamp).toLocaleString()}</p>
                            <div class="alert alert-warning">
                                <i class="fas fa-info-circle me-2"></i>
                                Please contact system administrator if this issue persists.
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Acknowledge</button>
                        </div>
                    </div>
                </div>
            </div>
        `);

        $('body').append(modal);
        modal.modal('show');
        modal.on('hidden.bs.modal', () => modal.remove());
    }

    // Initialize properties for enhanced functionality
    retryAttempts = 0;
    heartbeatInterval = null;
    lastHeartbeat = null;
}

// Initialize dashboard when DOM is ready
$(document).ready(function() {
    if (typeof dashboardConfig !== 'undefined') {
        window.salesDashboard = new SalesDashboard(dashboardConfig);
    }
});

// Global functions for inline event handlers
function removeFromQueue(queueEntryId) {
    if (confirm('Are you sure you want to remove this customer from the queue?')) {
        // Implementation for queue removal
        console.log('Removing queue entry:', queueEntryId);
    }
}

function viewCustomerHistory(customerId) {
    // Implementation for viewing customer history
    console.log('Viewing customer history:', customerId);
}