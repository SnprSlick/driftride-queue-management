/**
 * Payment Configuration Management JavaScript
 * Handles dynamic payment method configuration for sales staff
 */

class PaymentConfigurationManager {
    constructor() {
        this.hasUnsavedChanges = false;
        this.originalConfigs = new Map();
        this.initialize();
    }

    initialize() {
        this.setupEventListeners();
        this.loadOriginalConfigs();
        this.validateAllForms();

        // Setup real-time updates via SignalR if available
        if (typeof signalRConnection !== 'undefined' && signalRConnection) {
            this.setupSignalRHandlers();
        }
    }

    setupEventListeners() {
        // Configuration field changes
        $(document).on('input change', '.config-field', (e) => {
            this.handleConfigChange(e);
        });

        // Enable/disable toggle
        $(document).on('change', '.config-enabled', (e) => {
            this.handleEnabledToggle(e);
        });

        // API integration toggle
        $(document).on('change', 'input[data-field="ApiIntegrationEnabled"]', (e) => {
            this.handleApiIntegrationToggle(e);
        });

        // Save all changes
        $('#save-all-btn').on('click', () => {
            this.saveAllChanges();
        });

        // Refresh cache
        $('#refresh-cache-btn').on('click', () => {
            this.refreshCache();
        });

        // Form validation
        $(document).on('blur', '.config-field', (e) => {
            this.validateField(e.target);
        });

        // Prevent leaving with unsaved changes
        window.addEventListener('beforeunload', (e) => {
            if (this.hasUnsavedChanges) {
                e.preventDefault();
                e.returnValue = 'You have unsaved changes. Are you sure you want to leave?';
                return e.returnValue;
            }
        });
    }

    loadOriginalConfigs() {
        $('.payment-config-card').each((index, card) => {
            const $card = $(card);
            const configId = $card.data('config-id');
            const config = this.extractConfigFromCard($card);
            this.originalConfigs.set(configId, JSON.stringify(config));
        });
    }

    extractConfigFromCard($card) {
        const config = {
            Id: $card.find('.config-id').val(),
            DisplayName: $card.find('input[data-field="DisplayName"]').val(),
            PaymentUrl: $card.find('input[data-field="PaymentUrl"]').val() || null,
            IsEnabled: $card.find('input[data-field="IsEnabled"]').is(':checked'),
            PricePerRide: parseFloat($card.find('input[data-field="PricePerRide"]').val()) || 0,
            ApiIntegrationEnabled: $card.find('input[data-field="ApiIntegrationEnabled"]').is(':checked'),
            ApiCredentials: $card.find('textarea[data-field="ApiCredentials"]').val() || null
        };
        return config;
    }

    handleConfigChange(e) {
        const $field = $(e.target);
        const $card = $field.closest('.payment-config-card');

        this.validateField(e.target);
        this.checkForChanges($card);
        this.updateSaveButtonState();
    }

    handleEnabledToggle(e) {
        const $toggle = $(e.target);
        const $card = $toggle.closest('.payment-config-card');
        const $label = $toggle.next('label');

        const isEnabled = $toggle.is(':checked');
        $label.find('small').text(isEnabled ? 'Enabled' : 'Disabled');

        // Visual feedback
        $card.toggleClass('card-disabled', !isEnabled);

        this.checkForChanges($card);
        this.updateSaveButtonState();
    }

    handleApiIntegrationToggle(e) {
        const $toggle = $(e.target);
        const $card = $toggle.closest('.payment-config-card');
        const $credentialsGroup = $card.find('.api-credentials-group');

        const isEnabled = $toggle.is(':checked');

        if (isEnabled) {
            $credentialsGroup.slideDown();
        } else {
            $credentialsGroup.slideUp();
            $card.find('textarea[data-field="ApiCredentials"]').val('');
        }

        this.checkForChanges($card);
        this.updateSaveButtonState();
    }

    checkForChanges($card) {
        const configId = $card.data('config-id');
        const currentConfig = this.extractConfigFromCard($card);
        const originalConfig = this.originalConfigs.get(configId);

        const hasChanges = JSON.stringify(currentConfig) !== originalConfig;

        $card.toggleClass('has-changes', hasChanges);

        // Update global change state
        this.hasUnsavedChanges = $('.payment-config-card.has-changes').length > 0;
    }

    updateSaveButtonState() {
        const $saveBtn = $('#save-all-btn');
        const hasChanges = this.hasUnsavedChanges;
        const isValid = this.validateAllForms();

        $saveBtn.prop('disabled', !hasChanges || !isValid);

        if (hasChanges) {
            $saveBtn.removeClass('btn-success').addClass('btn-warning');
            $saveBtn.html('<i class="fas fa-save"></i> Save Changes');
        } else {
            $saveBtn.removeClass('btn-warning').addClass('btn-success');
            $saveBtn.html('<i class="fas fa-check"></i> All Saved');
        }
    }

    validateField(field) {
        const $field = $(field);
        const fieldName = $field.data('field');
        const value = $field.val();

        let isValid = true;
        let errorMessage = '';

        // Reset validation state
        $field.removeClass('is-invalid');
        $field.next('.invalid-feedback').remove();

        // Validation rules
        switch (fieldName) {
            case 'DisplayName':
                if (!value || value.trim().length === 0) {
                    isValid = false;
                    errorMessage = 'Display name is required';
                } else if (value.length > 100) {
                    isValid = false;
                    errorMessage = 'Display name cannot exceed 100 characters';
                }
                break;

            case 'PaymentUrl':
                if (value && !this.isValidUrl(value)) {
                    isValid = false;
                    errorMessage = 'Please enter a valid URL';
                } else if (value && value.length > 500) {
                    isValid = false;
                    errorMessage = 'URL cannot exceed 500 characters';
                }
                break;

            case 'PricePerRide':
                const price = parseFloat(value);
                if (isNaN(price) || price < 0.01 || price > 999999.99) {
                    isValid = false;
                    errorMessage = 'Price must be between $0.01 and $999,999.99';
                }
                break;

            case 'ApiCredentials':
                if (value && value.length > 1000) {
                    isValid = false;
                    errorMessage = 'API credentials cannot exceed 1000 characters';
                }
                break;
        }

        // Apply validation feedback
        if (!isValid) {
            $field.addClass('is-invalid');
            $field.after(`<div class="invalid-feedback">${errorMessage}</div>`);
        }

        return isValid;
    }

    validateAllForms() {
        let allValid = true;

        $('.config-field').each((index, field) => {
            if (!this.validateField(field)) {
                allValid = false;
            }
        });

        return allValid;
    }

    isValidUrl(string) {
        try {
            new URL(string);
            return true;
        } catch (_) {
            return false;
        }
    }

    async saveAllChanges() {
        const $saveBtn = $('#save-all-btn');
        const $spinner = $('#loading-spinner');

        if (!this.validateAllForms()) {
            this.showAlert('Please fix validation errors before saving', 'danger');
            return;
        }

        try {
            $saveBtn.prop('disabled', true);
            $spinner.removeClass('d-none');

            const changedCards = $('.payment-config-card.has-changes');
            const promises = [];

            changedCards.each((index, card) => {
                const $card = $(card);
                const config = this.extractConfigFromCard($card);
                promises.push(this.saveConfiguration(config));
            });

            const results = await Promise.allSettled(promises);

            let successCount = 0;
            let errors = [];

            results.forEach((result, index) => {
                if (result.status === 'fulfilled' && result.value.success) {
                    successCount++;
                    const $card = $(changedCards[index]);
                    $card.removeClass('has-changes');

                    // Update original config
                    const configId = $card.data('config-id');
                    const newConfig = this.extractConfigFromCard($card);
                    this.originalConfigs.set(configId, JSON.stringify(newConfig));
                } else {
                    const error = result.status === 'rejected' ? result.reason : result.value.message;
                    errors.push(error);
                }
            });

            if (successCount > 0) {
                this.showAlert(`Successfully updated ${successCount} configuration(s)`, 'success');

                // Refresh customer payment methods cache
                await this.refreshCache(false);
            }

            if (errors.length > 0) {
                this.showAlert(`Errors updating configurations: ${errors.join(', ')}`, 'danger');
            }

            this.hasUnsavedChanges = false;
            this.updateSaveButtonState();

        } catch (error) {
            console.error('Error saving configurations:', error);
            this.showAlert('An unexpected error occurred while saving', 'danger');
        } finally {
            $saveBtn.prop('disabled', false);
            $spinner.addClass('d-none');
        }
    }

    async saveConfiguration(config) {
        const response = await fetch('/PaymentConfiguration/UpdateConfiguration', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
            },
            body: JSON.stringify(config)
        });

        return await response.json();
    }

    async refreshCache(showMessage = true) {
        try {
            const response = await fetch('/PaymentConfiguration/RefreshCache', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                }
            });

            const result = await response.json();

            if (result.success && showMessage) {
                this.showAlert('Payment methods cache refreshed successfully', 'info');
            }

            return result.success;
        } catch (error) {
            console.error('Error refreshing cache:', error);
            if (showMessage) {
                this.showAlert('Failed to refresh cache', 'warning');
            }
            return false;
        }
    }

    showAlert(message, type = 'info') {
        const alertHtml = `
            <div class="alert alert-${type} alert-dismissible fade show" role="alert">
                <i class="fas fa-${this.getAlertIcon(type)}"></i> ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;

        $('#alert-container').prepend(alertHtml);

        // Auto-dismiss after 5 seconds
        setTimeout(() => {
            $('#alert-container .alert').first().alert('close');
        }, 5000);
    }

    getAlertIcon(type) {
        const icons = {
            'success': 'check-circle',
            'danger': 'exclamation-triangle',
            'warning': 'exclamation-circle',
            'info': 'info-circle'
        };
        return icons[type] || 'info-circle';
    }

    setupSignalRHandlers() {
        // Check if SignalR connection is available
        if (typeof signalRConnection === 'undefined' || !signalRConnection) {
            console.warn('SignalR connection not available for payment configuration');
            return;
        }

        // Listen for configuration change notifications
        signalRConnection.on("ConfigurationChanged", (notification) => {
            this.handleConfigurationChangeNotification(notification);
        });
    }

    handleConfigurationChangeNotification(notification) {
        // Show notification that configuration was changed by another user
        this.showAlert(
            `Configuration for ${notification.DisplayName} was ${notification.ChangeType.toLowerCase()} by ${notification.ChangedBy}`,
            'info'
        );

        // Optionally refresh the page or update the specific configuration
        // For now, just show a refresh suggestion
        if (!this.hasUnsavedChanges) {
            setTimeout(() => {
                if (confirm('Payment configuration was updated by another user. Refresh to see changes?')) {
                    window.location.reload();
                }
            }, 2000);
        }
    }
}

// Initialize when DOM is ready
$(document).ready(() => {
    window.paymentConfigManager = new PaymentConfigurationManager();
});

// Export for external access
window.PaymentConfigurationManager = PaymentConfigurationManager;