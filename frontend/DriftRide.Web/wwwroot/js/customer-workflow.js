/**
 * Customer Workflow Management
 * Handles the 4-step customer registration and payment flow
 * Includes form validation, payment processing, and real-time queue updates via SignalR
 */

const CustomerWorkflow = {
    // Configuration
    config: {
        apiBaseUrl: '',
        signalRHubUrl: '',
        currentStep: 1,
        customerId: null,
        paymentId: null,
        selectedPaymentMethod: null,
        connection: null
    },

    // Customer data
    customerData: {
        name: '',
        phoneNumber: '',
        paymentMethod: '',
        ridePrice: 25.00
    },

    /**
     * Initialize the customer workflow
     * @param {Object} options - Configuration options
     */
    init: function(options) {
        this.config.apiBaseUrl = options.apiBaseUrl;
        this.config.signalRHubUrl = options.signalRHubUrl;

        this.initializeEventListeners();
        this.loadPaymentMethods();
        this.setupSignalR();
        this.updateProgressIndicator();
    },

    /**
     * Set up all event listeners for the interface
     */
    initializeEventListeners: function() {
        // Step 1: Customer Information
        document.getElementById('nextToPaymentBtn').addEventListener('click', () => {
            this.validateAndProceedToPayment();
        });

        // Step 2: Payment Method Selection
        document.getElementById('backToInfoBtn').addEventListener('click', () => {
            this.goToStep(1);
        });

        document.getElementById('proceedPaymentBtn').addEventListener('click', () => {
            this.proceedToPaymentConfirmation();
        });

        // Step 3: Payment Confirmation
        document.getElementById('backToPaymentBtn').addEventListener('click', () => {
            this.goToStep(2);
        });

        document.getElementById('paymentCompletedCheck').addEventListener('change', (e) => {
            document.getElementById('confirmPaymentBtn').disabled = !e.target.checked;
        });

        document.getElementById('confirmPaymentBtn').addEventListener('click', () => {
            this.confirmPayment();
        });

        // Phone number formatting
        document.getElementById('customerPhone').addEventListener('input', this.formatPhoneNumber);

        // Real-time form validation
        document.getElementById('customerName').addEventListener('input', this.validateName);
        document.getElementById('customerPhone').addEventListener('input', this.validatePhone);
    },

    /**
     * Load available payment methods from the API
     */
    loadPaymentMethods: async function() {
        try {
            this.showLoading();

            // Use the new payment configuration endpoint that supports real-time updates
            const response = await fetch('/PaymentConfiguration/GetCustomerPaymentMethods');

            if (response.ok) {
                const result = await response.json();
                if (result.success && result.data) {
                    this.renderPaymentMethods(result.data);

                    // Load ride price from the first enabled payment method
                    if (result.data.length > 0) {
                        this.customerData.ridePrice = result.data[0].pricePerRide || 25.00;
                        document.getElementById('ridePrice').textContent = this.customerData.ridePrice.toFixed(2);
                    }
                } else {
                    this.showError(result.message || 'Unable to load payment methods. Please refresh the page.');
                }
            } else {
                this.showError('Unable to load payment methods. Please refresh the page.');
            }
        } catch (error) {
            console.error('Error loading payment methods:', error);
            this.renderDefaultPaymentMethods();
        } finally {
            this.hideLoading();
        }
    },

    /**
     * Render payment method options
     */
    renderPaymentMethods: function(methods) {
        const paymentMethodsContainer = document.querySelector('.payment-methods');
        const template = document.getElementById('paymentTemplate');

        // Clear existing methods except template
        const existingMethods = paymentMethodsContainer.querySelectorAll('.payment-card:not(#paymentTemplate)');
        existingMethods.forEach(method => method.remove());

        methods.forEach(method => {
            if (method.isEnabled) {
                const methodElement = this.createPaymentMethodElement(method);
                paymentMethodsContainer.appendChild(methodElement);
            }
        });

        // If no methods loaded, show defaults
        if (methods.length === 0) {
            this.renderDefaultPaymentMethods();
        }
    },

    /**
     * Create payment method element
     */
    createPaymentMethodElement: function(method) {
        const template = document.getElementById('paymentTemplate');
        const clone = template.cloneNode(true);

        clone.id = `payment-${method.method.toLowerCase()}`;
        clone.classList.remove('d-none');

        const card = clone.querySelector('.payment-card');
        card.dataset.method = method.method;
        card.dataset.url = method.paymentUrl || '';

        const icon = clone.querySelector('.payment-icon-img');
        const name = clone.querySelector('.payment-name');
        const description = clone.querySelector('.payment-description');

        // Set icon based on payment method
        switch (method.method.toLowerCase()) {
            case 'cashapp':
                icon.className = 'fas fa-mobile-alt payment-icon-img';
                description.textContent = 'Pay with CashApp - Quick & Secure';
                break;
            case 'paypal':
                icon.className = 'fab fa-paypal payment-icon-img';
                description.textContent = 'Pay with PayPal - Trusted Worldwide';
                break;
            case 'cash':
            case 'cashinhand':
                icon.className = 'fas fa-money-bill payment-icon-img';
                description.textContent = 'Pay with Cash - At the Counter';
                break;
            default:
                icon.className = 'fas fa-credit-card payment-icon-img';
                description.textContent = 'Secure Payment Method';
        }

        name.textContent = method.displayName || method.method;

        // Add click handler
        card.addEventListener('click', () => {
            this.selectPaymentMethod(method.method, card);
        });

        return clone;
    },

    /**
     * Render default payment methods if API fails
     */
    renderDefaultPaymentMethods: function() {
        const defaultMethods = [
            { method: 'CashApp', displayName: 'CashApp', isEnabled: true, paymentUrl: '' },
            { method: 'PayPal', displayName: 'PayPal', isEnabled: true, paymentUrl: '' },
            { method: 'Cash', displayName: 'Cash in Hand', isEnabled: true, paymentUrl: '' }
        ];

        this.renderPaymentMethods(defaultMethods);
    },

    /**
     * Select a payment method
     */
    selectPaymentMethod: function(method, cardElement) {
        // Remove previous selections
        document.querySelectorAll('.payment-card').forEach(card => {
            card.classList.remove('selected');
        });

        // Select current method
        cardElement.classList.add('selected');
        this.config.selectedPaymentMethod = method;
        this.customerData.paymentMethod = method;

        // Enable proceed button
        document.getElementById('proceedPaymentBtn').disabled = false;
    },

    /**
     * Validate customer information and proceed to payment
     */
    validateAndProceedToPayment: function() {
        const name = document.getElementById('customerName').value.trim();
        const phone = document.getElementById('customerPhone').value.trim();

        let isValid = true;

        // Validate name
        if (!name || name.length < 2) {
            this.setFieldError('customerName', 'Please enter your full name (at least 2 characters)');
            isValid = false;
        } else {
            this.clearFieldError('customerName');
        }

        // Validate phone
        if (!phone || !this.isValidPhoneNumber(phone)) {
            this.setFieldError('customerPhone', 'Please enter a valid phone number');
            isValid = false;
        } else {
            this.clearFieldError('customerPhone');
        }

        if (isValid) {
            this.customerData.name = name;
            this.customerData.phoneNumber = phone;
            this.goToStep(2);
        }
    },

    /**
     * Proceed to payment confirmation step
     */
    proceedToPaymentConfirmation: function() {
        if (!this.config.selectedPaymentMethod) {
            this.showError('Please select a payment method');
            return;
        }

        this.setupPaymentInstructions();
        this.goToStep(3);
    },

    /**
     * Setup payment instructions based on selected method
     */
    setupPaymentInstructions: function() {
        const instructionsContainer = document.getElementById('paymentInstructions');
        const method = this.config.selectedPaymentMethod.toLowerCase();

        let instructions = '';

        switch (method) {
            case 'cashapp':
                instructions = `
                    <h5 class="text-primary mb-3">📱 CashApp Payment</h5>
                    <p>1. Tap the button below to open CashApp</p>
                    <p>2. Send <strong>$${this.customerData.ridePrice.toFixed(2)}</strong> to our CashApp account</p>
                    <p>3. Return here and check the confirmation box</p>
                    <button class="btn btn-success btn-lg mt-3" onclick="CustomerWorkflow.openPaymentApp('cashapp')">
                        Open CashApp <i class="fas fa-external-link-alt ms-2"></i>
                    </button>
                `;
                break;
            case 'paypal':
                instructions = `
                    <h5 class="text-primary mb-3">💙 PayPal Payment</h5>
                    <p>1. Tap the button below to open PayPal</p>
                    <p>2. Send <strong>$${this.customerData.ridePrice.toFixed(2)}</strong> to our PayPal account</p>
                    <p>3. Return here and check the confirmation box</p>
                    <button class="btn btn-primary btn-lg mt-3" onclick="CustomerWorkflow.openPaymentApp('paypal')">
                        Open PayPal <i class="fas fa-external-link-alt ms-2"></i>
                    </button>
                `;
                break;
            case 'cash':
            case 'cashinhand':
                instructions = `
                    <h5 class="text-primary mb-3">💵 Cash Payment</h5>
                    <p>1. Please proceed to our sales counter</p>
                    <p>2. Pay <strong>$${this.customerData.ridePrice.toFixed(2)}</strong> in cash</p>
                    <p>3. Return here and check the confirmation box</p>
                    <div class="alert alert-info mt-3">
                        <i class="fas fa-info-circle me-2"></i>
                        Our sales team will verify your cash payment
                    </div>
                `;
                break;
        }

        instructionsContainer.innerHTML = instructions;
    },

    /**
     * Open payment app (CashApp or PayPal)
     */
    openPaymentApp: function(method) {
        // Get payment URL from the selected card
        const selectedCard = document.querySelector('.payment-card.selected');
        const paymentUrl = selectedCard ? selectedCard.dataset.url : '';

        if (paymentUrl) {
            window.open(paymentUrl, '_blank');
        } else {
            // Fallback to app schemes
            const appUrls = {
                'cashapp': 'https://cash.app/',
                'paypal': 'https://paypal.me/'
            };

            if (appUrls[method]) {
                window.open(appUrls[method], '_blank');
            }
        }
    },

    /**
     * Confirm payment and submit to backend
     */
    confirmPayment: async function() {
        try {
            this.showLoading();

            // Step 1: Create customer
            const customerResponse = await this.createCustomer();
            if (!customerResponse.success) {
                throw new Error(customerResponse.message || 'Failed to create customer');
            }

            this.config.customerId = customerResponse.data.id;

            // Step 2: Process payment
            const paymentResponse = await this.processPayment();
            if (!paymentResponse.success) {
                throw new Error(paymentResponse.message || 'Failed to process payment');
            }

            this.config.paymentId = paymentResponse.data.id;

            // Move to queue status step
            this.goToStep(4);

        } catch (error) {
            console.error('Payment confirmation error:', error);
            this.showError(error.message);
        } finally {
            this.hideLoading();
        }
    },

    /**
     * Create customer via API
     */
    createCustomer: async function() {
        const response = await fetch('/Customer/CreateCustomer', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('[name="__RequestVerificationToken"]')?.value || ''
            },
            body: JSON.stringify({
                name: this.customerData.name,
                phoneNumber: this.customerData.phoneNumber
            })
        });

        return await response.json();
    },

    /**
     * Process payment via API
     */
    processPayment: async function() {
        const response = await fetch('/Customer/ProcessPayment', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('[name="__RequestVerificationToken"]')?.value || ''
            },
            body: JSON.stringify({
                customerId: this.config.customerId,
                amount: this.customerData.ridePrice,
                paymentMethod: this.customerData.paymentMethod
            })
        });

        return await response.json();
    },

    /**
     * Setup SignalR connection for real-time updates
     */
    setupSignalR: function() {
        if (typeof signalR === 'undefined') {
            console.warn('SignalR not available');
            return;
        }

        this.config.connection = new signalR.HubConnectionBuilder()
            .withUrl(this.config.signalRHubUrl)
            .withAutomaticReconnect([0, 2000, 10000, 30000, 60000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Connection event handlers
        this.config.connection.onreconnecting(() => {
            this.showConnectionStatus('Reconnecting...', 'warning');
        });

        this.config.connection.onreconnected(() => {
            this.showConnectionStatus('Connected', 'success');
            this.rejoinGroups();
        });

        this.config.connection.onclose(() => {
            this.showConnectionStatus('Disconnected', 'error');
        });

        // Handle queue updates
        this.config.connection.on('QueueUpdated', (notification) => {
            this.handleQueueUpdate(notification);
        });

        // Handle payment status updates (enhanced)
        this.config.connection.on('PaymentStatusChanged', (notification) => {
            this.handlePaymentStatusUpdate(notification);
        });

        // Handle payment denied notifications
        this.config.connection.on('PaymentDenied', (notification) => {
            this.handlePaymentDenied(notification);
        });

        // Handle queue position updates
        this.config.connection.on('QueuePositionUpdated', (notification) => {
            this.handleQueuePositionUpdate(notification);
        });

        // Handle queue changes that might affect position
        this.config.connection.on('QueueMayHaveChanged', (notification) => {
            this.handleQueueMayHaveChanged(notification);
        });

        // Handle configuration changes to refresh payment methods
        this.config.connection.on('ConfigurationChanged', (notification) => {
            this.handleConfigurationChange(notification);
        });

        // Handle service issues
        this.config.connection.on('ServiceIssue', (notification) => {
            this.handleServiceIssue(notification);
        });

        // Handle heartbeat responses for connection monitoring
        this.config.connection.on('HeartbeatResponse', (timestamp) => {
            this.handleHeartbeatResponse(timestamp);
        });

        // Start connection
        this.config.connection.start()
            .then(() => {
                console.log('SignalR connected successfully');
                this.showConnectionStatus('Connected', 'success');
                this.joinCustomerGroups();
                this.startHeartbeat();
            })
            .catch(err => {
                console.error('SignalR connection error:', err);
                this.showConnectionStatus('Connection Failed', 'error');
                this.retryConnection();
            });
    },

    /**
     * Handle queue update notifications
     */
    handleQueueUpdate: function(notification) {
        if (this.config.currentStep === 4 && this.config.customerId) {
            // Find our customer in the queue
            const ourEntry = notification.queueEntries?.find(entry =>
                entry.customerId === this.config.customerId
            );

            if (ourEntry) {
                this.updateQueueDisplay(ourEntry.position, notification.queueEntries.length);
            }
        }
    },

    /**
     * Handle payment status update notifications
     */
    handlePaymentStatusUpdate: function(notification) {
        if (notification.paymentId === this.config.paymentId) {
            if (notification.status === 'Confirmed') {
                this.showPaymentConfirmed();
            } else if (notification.status === 'Denied') {
                this.showPaymentDenied();
            }
        }
    },

    /**
     * Handle configuration change notifications to refresh payment methods
     */
    handleConfigurationChange: function(notification) {
        console.log('Configuration changed:', notification);

        // Only refresh if we're still on step 2 (payment method selection)
        if (this.config.currentStep === 2) {
            // Silently refresh payment methods in the background
            this.loadPaymentMethods();

            // Show a subtle notification
            if (notification.changeType === 'Updated') {
                this.showInfo(`Payment method "${notification.displayName}" was updated by staff`);
            }
        }
    },

    /**
     * Update queue position display
     */
    updateQueueDisplay: function(position, totalInQueue) {
        document.getElementById('queueSpinner').style.display = 'none';
        document.getElementById('queueStatusTitle').textContent = "You're in the queue!";
        document.getElementById('queueStatusMessage').textContent = 'Get ready for an awesome drift ride!';

        const queueDisplay = document.getElementById('queuePositionDisplay');
        queueDisplay.classList.remove('d-none');

        document.getElementById('yourPosition').textContent = position;
        document.getElementById('peopleAhead').textContent = position - 1;

        // Estimate wait time (assuming 5 minutes per ride)
        const estimatedMinutes = (position - 1) * 5;
        const waitTime = estimatedMinutes > 0 ? `${estimatedMinutes} minutes` : 'You\'re next!';
        document.getElementById('estimatedWait').textContent = waitTime;
    },

    /**
     * Show payment confirmed status
     */
    showPaymentConfirmed: function() {
        document.getElementById('queueSpinner').style.display = 'none';
        document.getElementById('queueStatusTitle').textContent = 'Payment Confirmed!';
        document.getElementById('queueStatusMessage').textContent = 'Welcome to the drift queue!';
    },

    /**
     * Show payment denied status
     */
    showPaymentDenied: function() {
        document.getElementById('queueSpinner').style.display = 'none';
        document.getElementById('queueStatusTitle').style.display = 'none';
        document.getElementById('queueStatusMessage').style.display = 'none';
        document.getElementById('paymentDeniedDisplay').classList.remove('d-none');
    },

    /**
     * Navigate to a specific step
     */
    goToStep: function(stepNumber) {
        // Hide all step content
        document.querySelectorAll('.step-content').forEach(content => {
            content.classList.add('d-none');
        });

        // Show target step
        document.getElementById(`step${stepNumber}Content`).classList.remove('d-none');

        // Update step indicators
        this.updateStepIndicators(stepNumber);

        // Update progress bar
        this.updateProgressIndicator(stepNumber);

        this.config.currentStep = stepNumber;
    },

    /**
     * Update step circle indicators
     */
    updateStepIndicators: function(currentStep) {
        for (let i = 1; i <= 4; i++) {
            const circle = document.getElementById(`step${i}Circle`);
            circle.classList.remove('active', 'completed');

            if (i < currentStep) {
                circle.classList.add('completed');
            } else if (i === currentStep) {
                circle.classList.add('active');
            }
        }
    },

    /**
     * Update progress bar
     */
    updateProgressIndicator: function(step = 1) {
        const progressBar = document.getElementById('progressBar');
        const progress = (step / 4) * 100;
        progressBar.style.width = `${progress}%`;
        progressBar.setAttribute('aria-valuenow', progress);
    },

    /**
     * Form validation helpers
     */
    validateName: function(e) {
        const name = e.target.value.trim();
        if (name.length >= 2) {
            CustomerWorkflow.clearFieldError('customerName');
        }
    },

    validatePhone: function(e) {
        const phone = e.target.value.trim();
        if (CustomerWorkflow.isValidPhoneNumber(phone)) {
            CustomerWorkflow.clearFieldError('customerPhone');
        }
    },

    isValidPhoneNumber: function(phone) {
        const phoneRegex = /^[\+]?[(]?[\d\s\-\(\)]{10,}$/;
        return phoneRegex.test(phone);
    },

    formatPhoneNumber: function(e) {
        let value = e.target.value.replace(/\D/g, '');

        if (value.length >= 6) {
            value = value.replace(/(\d{3})(\d{3})(\d{4})/, '($1) $2-$3');
        } else if (value.length >= 3) {
            value = value.replace(/(\d{3})(\d{3})/, '($1) $2');
        }

        e.target.value = value;
    },

    /**
     * Error handling
     */
    setFieldError: function(fieldId, message) {
        const field = document.getElementById(fieldId);
        const feedback = field.nextElementSibling;

        field.classList.add('is-invalid');
        field.classList.remove('is-valid');
        feedback.textContent = message;
    },

    clearFieldError: function(fieldId) {
        const field = document.getElementById(fieldId);
        const feedback = field.nextElementSibling;

        field.classList.remove('is-invalid');
        field.classList.add('is-valid');
        feedback.textContent = '';
    },

    showError: function(message) {
        const errorAlert = document.getElementById('errorAlert');
        const errorList = document.getElementById('errorList');

        errorList.innerHTML = `<li>${message}</li>`;
        errorAlert.classList.remove('d-none');

        // Auto-hide after 10 seconds
        setTimeout(() => {
            errorAlert.classList.add('d-none');
        }, 10000);
    },

    showSuccess: function(message) {
        const successAlert = document.getElementById('successAlert');
        const successMessage = document.getElementById('successMessage');

        successMessage.textContent = message;
        successAlert.classList.remove('d-none');

        // Auto-hide after 5 seconds
        setTimeout(() => {
            successAlert.classList.add('d-none');
        }, 5000);
    },

    showLoading: function() {
        document.getElementById('loadingOverlay').classList.remove('d-none');
    },

    hideLoading: function() {
        document.getElementById('loadingOverlay').classList.add('d-none');
    },

    /**
     * Join customer-specific SignalR groups
     */
    joinCustomerGroups: function() {
        if (!this.config.connection) return;

        // Join general customers group
        this.config.connection.invoke('JoinGroup', 'Customers');

        // Join customer-specific group if we have a customer ID
        if (this.config.customerId) {
            this.config.connection.invoke('JoinCustomerGroup', this.config.customerId);
        }

        // Join payment-specific group if we have a payment ID
        if (this.config.paymentId) {
            this.config.connection.invoke('JoinPaymentGroup', this.config.paymentId);
        }
    },

    /**
     * Rejoin groups after reconnection
     */
    rejoinGroups: function() {
        console.log('Rejoining SignalR groups after reconnection');
        this.joinCustomerGroups();
    },

    /**
     * Start heartbeat monitoring
     */
    startHeartbeat: function() {
        if (this.heartbeatInterval) {
            clearInterval(this.heartbeatInterval);
        }

        this.heartbeatInterval = setInterval(() => {
            if (this.config.connection && this.config.connection.state === signalR.HubConnectionState.Connected) {
                this.config.connection.invoke('Heartbeat');
            }
        }, 30000); // Send heartbeat every 30 seconds
    },

    /**
     * Handle heartbeat response
     */
    handleHeartbeatResponse: function(timestamp) {
        console.log('Heartbeat response received at:', timestamp);
        this.lastHeartbeat = new Date(timestamp);
    },

    /**
     * Handle payment denied notifications
     */
    handlePaymentDenied: function(notification) {
        console.log('Payment denied notification received:', notification);

        if (notification.PaymentId === this.config.paymentId) {
            this.showPaymentDenied();
            this.showError(`Payment was denied: ${notification.Reason || 'Please contact our sales team for assistance.'}`);

            // Play error sound if available
            this.playNotificationSound('error');
        }
    },

    /**
     * Handle queue position updates
     */
    handleQueuePositionUpdate: function(notification) {
        console.log('Queue position update received:', notification);

        if (notification.CustomerId === this.config.customerId) {
            this.updateQueueDisplay(notification.QueuePosition, null);

            // Update estimated wait time if provided
            if (notification.EstimatedWaitTime) {
                const minutes = Math.round(notification.EstimatedWaitTime / 60000); // Convert from milliseconds
                document.getElementById('estimatedWait').textContent =
                    minutes > 0 ? `${minutes} minutes` : 'You\'re next!';
            }

            this.showInfo(`Your queue position has been updated to #${notification.QueuePosition}`);
        }
    },

    /**
     * Handle queue changes that might affect customer position
     */
    handleQueueMayHaveChanged: function(notification) {
        console.log('Queue may have changed:', notification);

        // If we're currently in the queue, we might want to refresh our position
        if (this.config.currentStep === 4 && this.config.customerId) {
            // Could implement a light refresh here if needed
            this.showInfo('Queue has been updated');
        }
    },

    /**
     * Handle service issues
     */
    handleServiceIssue: function(notification) {
        console.log('Service issue notification:', notification);

        const severityMap = {
            'Info': 'info',
            'Warning': 'warning',
            'Error': 'error',
            'Critical': 'error'
        };

        const alertType = severityMap[notification.Severity] || 'info';
        this.showNotification(notification.Message, alertType);

        if (notification.Severity === 'Critical' || notification.Severity === 'Error') {
            this.playNotificationSound('error');
        }
    },

    /**
     * Show connection status to user
     */
    showConnectionStatus: function(message, type) {
        const statusElement = document.getElementById('connectionStatus');
        if (statusElement) {
            statusElement.textContent = message;
            statusElement.className = `connection-status ${type}`;

            if (type === 'success') {
                setTimeout(() => {
                    statusElement.style.display = 'none';
                }, 3000);
            } else {
                statusElement.style.display = 'block';
            }
        }
    },

    /**
     * Retry connection with exponential backoff
     */
    retryConnection: function() {
        if (this.retryAttempts >= 5) {
            this.showError('Unable to establish connection. Please refresh the page.');
            return;
        }

        const delay = Math.pow(2, this.retryAttempts) * 1000; // Exponential backoff
        this.retryAttempts++;

        setTimeout(() => {
            console.log(`Retrying connection attempt ${this.retryAttempts}`);
            this.setupSignalR();
        }, delay);
    },

    /**
     * Show info notification
     */
    showInfo: function(message) {
        const infoAlert = document.getElementById('infoAlert');
        const infoMessage = document.getElementById('infoMessage');

        if (infoAlert && infoMessage) {
            infoMessage.textContent = message;
            infoAlert.classList.remove('d-none');

            // Auto-hide after 5 seconds
            setTimeout(() => {
                infoAlert.classList.add('d-none');
            }, 5000);
        }
    },

    /**
     * Show notification with different types
     */
    showNotification: function(message, type = 'info') {
        // Create a toast notification
        const toast = document.createElement('div');
        toast.className = `toast align-items-center text-white bg-${type === 'error' ? 'danger' : type} border-0`;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">${message}</div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        `;

        // Add to toast container
        let toastContainer = document.querySelector('.toast-container');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3';
            document.body.appendChild(toastContainer);
        }

        toastContainer.appendChild(toast);

        // Show toast using Bootstrap
        if (typeof bootstrap !== 'undefined') {
            const bsToast = new bootstrap.Toast(toast);
            bsToast.show();

            // Remove from DOM after hiding
            toast.addEventListener('hidden.bs.toast', () => {
                toast.remove();
            });
        }
    },

    /**
     * Play notification sound based on type
     */
    playNotificationSound: function(type = 'default') {
        const soundMap = {
            'default': 'notification-sound',
            'success': 'success-sound',
            'error': 'error-sound',
            'warning': 'warning-sound'
        };

        const soundId = soundMap[type] || soundMap['default'];
        const audio = document.getElementById(soundId);

        if (audio) {
            audio.play().catch(e => console.log('Could not play notification sound:', e));
        }
    },

    /**
     * Initialize retry attempts counter
     */
    retryAttempts: 0,
    heartbeatInterval: null,
    lastHeartbeat: null
};

// Make CustomerWorkflow globally available
window.CustomerWorkflow = CustomerWorkflow;