/**
 * Enhanced client-side form validation for customer and payment forms.
 * Provides real-time validation feedback, error display, and form submission protection.
 */
class FormValidator {
    constructor(formElement, options = {}) {
        this.form = formElement;
        this.options = {
            validateOnInput: true,
            validateOnBlur: true,
            showErrorsInline: true,
            preventInvalidSubmission: true,
            errorClass: 'is-invalid',
            successClass: 'is-valid',
            errorMessageClass: 'invalid-feedback',
            ...options
        };

        this.validators = new Map();
        this.errors = new Map();
        this.isSubmitting = false;

        this.init();
    }

    /**
     * Initialize the form validator.
     */
    init() {
        this.setupValidationRules();
        this.attachEventListeners();
        this.createErrorContainers();
    }

    /**
     * Setup validation rules for different field types.
     */
    setupValidationRules() {
        // Customer name validation
        this.addValidator('customerName', (value) => {
            if (!value || value.trim().length === 0) {
                return 'Customer name is required';
            }

            if (value.trim().length < 2) {
                return 'Customer name must be at least 2 characters long';
            }

            if (value.length > 100) {
                return 'Customer name cannot exceed 100 characters';
            }

            // Check for invalid characters
            const invalidChars = /[<>"'&;()*%$#@![\]{}\\\/|]/;
            if (invalidChars.test(value)) {
                return 'Customer name contains prohibited characters';
            }

            // Check for valid name format
            const validNamePattern = /^[a-zA-Z\s.\-'\u00C0-\u017F]+$/;
            if (!validNamePattern.test(value)) {
                return 'Please use only letters, spaces, hyphens, apostrophes, and periods';
            }

            // Check for suspicious patterns
            if (value.includes('..') || value.includes('--') || value.includes('  ')) {
                return 'Customer name contains invalid character sequences';
            }

            // Check for generic names
            const lowerName = value.toLowerCase().trim();
            const genericNames = ['test', 'admin', 'user', 'customer', 'guest', 'unknown'];
            if (genericNames.includes(lowerName)) {
                return 'Please enter a valid customer name';
            }

            return null;
        });

        // Email validation
        this.addValidator('email', (value) => {
            if (!value || value.trim().length === 0) {
                return 'Email address is required';
            }

            if (value.length > 255) {
                return 'Email address cannot exceed 255 characters';
            }

            const emailPattern = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
            if (!emailPattern.test(value)) {
                return 'Please enter a valid email address';
            }

            return null;
        });

        // Phone number validation
        this.addValidator('phone', (value) => {
            // Phone is optional, so allow empty
            if (!value || value.trim().length === 0) {
                return null;
            }

            if (value.length > 20) {
                return 'Phone number cannot exceed 20 characters';
            }

            // Remove formatting for validation
            const cleanedPhone = value.replace(/[\s\-\(\)\.]/g, '');
            const digitCount = cleanedPhone.replace(/[^\d]/g, '').length;

            if (digitCount < 7 || digitCount > 15) {
                return 'Phone number must contain 7-15 digits';
            }

            const phonePattern = /^[\+]?[1-9]?[\d\s\-\(\)\.]{7,20}$/;
            if (!phonePattern.test(value)) {
                return 'Please enter a valid phone number';
            }

            return null;
        });

        // Payment amount validation
        this.addValidator('paymentAmount', (value) => {
            if (!value || value.trim().length === 0) {
                return 'Payment amount is required';
            }

            const amount = parseFloat(value);
            if (isNaN(amount)) {
                return 'Payment amount must be a valid number';
            }

            if (amount < 0.01) {
                return 'Payment amount cannot be less than $0.01';
            }

            if (amount > 9999.99) {
                return 'Payment amount cannot exceed $9999.99';
            }

            // Check decimal places
            const decimalPlaces = (value.split('.')[1] || '').length;
            if (decimalPlaces > 2) {
                return 'Payment amount cannot have more than 2 decimal places';
            }

            return null;
        });

        // External transaction ID validation
        this.addValidator('externalTransactionId', (value, formData) => {
            const paymentMethod = formData.get('paymentMethod');

            // Required for electronic payments
            if ((paymentMethod === 'CashApp' || paymentMethod === 'PayPal')) {
                if (!value || value.trim().length === 0) {
                    return `External transaction ID is required for ${paymentMethod} payments`;
                }

                if (value.length < 3) {
                    return 'External transaction ID must be at least 3 characters';
                }
            }

            // Optional for cash payments
            if (value && value.length > 255) {
                return 'External transaction ID cannot exceed 255 characters';
            }

            return null;
        });

        // Payment confirmation notes validation
        this.addValidator('confirmationNotes', (value, formData) => {
            const confirmed = formData.get('confirmed');

            // Required when denying payment
            if (confirmed === 'false' || confirmed === false) {
                if (!value || value.trim().length === 0) {
                    return 'Notes are required when denying a payment';
                }

                if (value.trim().length < 10) {
                    return 'Notes must be at least 10 characters when denying a payment';
                }
            }

            if (value && value.length > 500) {
                return 'Notes cannot exceed 500 characters';
            }

            // Check for valid characters
            const validNotesPattern = /^[a-zA-Z0-9\s\.\,\!\?\-\'\(\)]+$/;
            if (value && !validNotesPattern.test(value)) {
                return 'Notes contain invalid characters';
            }

            return null;
        });
    }

    /**
     * Add a custom validator for a field.
     */
    addValidator(fieldName, validatorFn) {
        this.validators.set(fieldName, validatorFn);
    }

    /**
     * Attach event listeners for validation.
     */
    attachEventListeners() {
        if (this.options.validateOnInput) {
            this.form.addEventListener('input', (e) => this.handleInputValidation(e));
        }

        if (this.options.validateOnBlur) {
            this.form.addEventListener('blur', (e) => this.handleBlurValidation(e), true);
        }

        if (this.options.preventInvalidSubmission) {
            this.form.addEventListener('submit', (e) => this.handleSubmit(e));
        }
    }

    /**
     * Create error containers for inline error display.
     */
    createErrorContainers() {
        if (!this.options.showErrorsInline) return;

        const inputs = this.form.querySelectorAll('input, select, textarea');
        inputs.forEach(input => {
            if (!input.parentElement.querySelector(`.${this.options.errorMessageClass}`)) {
                const errorDiv = document.createElement('div');
                errorDiv.className = this.options.errorMessageClass;
                errorDiv.style.display = 'none';
                input.parentElement.appendChild(errorDiv);
            }
        });
    }

    /**
     * Handle input validation (real-time).
     */
    handleInputValidation(event) {
        if (event.target.type === 'submit') return;

        // Debounce validation for better performance
        clearTimeout(event.target.validationTimeout);
        event.target.validationTimeout = setTimeout(() => {
            this.validateField(event.target);
        }, 300);
    }

    /**
     * Handle blur validation.
     */
    handleBlurValidation(event) {
        if (event.target.type === 'submit') return;
        this.validateField(event.target);
    }

    /**
     * Handle form submission.
     */
    handleSubmit(event) {
        if (this.isSubmitting) {
            event.preventDefault();
            return;
        }

        const isValid = this.validateForm();

        if (!isValid) {
            event.preventDefault();
            this.focusFirstError();
            return;
        }

        // Prevent double submission
        this.isSubmitting = true;
        const submitButton = this.form.querySelector('button[type="submit"]');
        if (submitButton) {
            submitButton.disabled = true;
            const originalText = submitButton.textContent;
            submitButton.textContent = 'Processing...';

            // Re-enable after 10 seconds as fallback
            setTimeout(() => {
                this.isSubmitting = false;
                submitButton.disabled = false;
                submitButton.textContent = originalText;
            }, 10000);
        }
    }

    /**
     * Validate a single field.
     */
    validateField(field) {
        const fieldName = this.getFieldValidationName(field);
        const validator = this.validators.get(fieldName);

        if (!validator) return true;

        const formData = new FormData(this.form);
        const error = validator(field.value, formData);

        this.updateFieldValidation(field, error);

        return error === null;
    }

    /**
     * Validate the entire form.
     */
    validateForm() {
        let isValid = true;
        const inputs = this.form.querySelectorAll('input, select, textarea');

        inputs.forEach(input => {
            if (!this.validateField(input)) {
                isValid = false;
            }
        });

        return isValid;
    }

    /**
     * Update field validation state and display.
     */
    updateFieldValidation(field, error) {
        const fieldName = field.name || field.id;

        if (error) {
            this.errors.set(fieldName, error);
            field.classList.add(this.options.errorClass);
            field.classList.remove(this.options.successClass);
            this.showFieldError(field, error);
        } else {
            this.errors.delete(fieldName);
            field.classList.remove(this.options.errorClass);
            field.classList.add(this.options.successClass);
            this.hideFieldError(field);
        }
    }

    /**
     * Show error message for a field.
     */
    showFieldError(field, error) {
        if (!this.options.showErrorsInline) return;

        const errorContainer = field.parentElement.querySelector(`.${this.options.errorMessageClass}`);
        if (errorContainer) {
            errorContainer.textContent = error;
            errorContainer.style.display = 'block';
        }
    }

    /**
     * Hide error message for a field.
     */
    hideFieldError(field) {
        if (!this.options.showErrorsInline) return;

        const errorContainer = field.parentElement.querySelector(`.${this.options.errorMessageClass}`);
        if (errorContainer) {
            errorContainer.style.display = 'none';
        }
    }

    /**
     * Focus the first field with an error.
     */
    focusFirstError() {
        const firstErrorField = this.form.querySelector(`.${this.options.errorClass}`);
        if (firstErrorField) {
            firstErrorField.focus();
            firstErrorField.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    /**
     * Get validation name for a field.
     */
    getFieldValidationName(field) {
        // Map field names to validation types
        const name = field.name || field.id;
        const nameMapping = {
            'name': 'customerName',
            'customerName': 'customerName',
            'email': 'email',
            'phone': 'phone',
            'phoneNumber': 'phone',
            'amount': 'paymentAmount',
            'paymentAmount': 'paymentAmount',
            'externalTransactionId': 'externalTransactionId',
            'notes': 'confirmationNotes',
            'confirmationNotes': 'confirmationNotes'
        };

        return nameMapping[name] || name;
    }

    /**
     * Reset form validation state.
     */
    reset() {
        this.errors.clear();
        this.isSubmitting = false;

        const inputs = this.form.querySelectorAll('input, select, textarea');
        inputs.forEach(input => {
            input.classList.remove(this.options.errorClass, this.options.successClass);
            this.hideFieldError(input);
        });

        const submitButton = this.form.querySelector('button[type="submit"]');
        if (submitButton) {
            submitButton.disabled = false;
        }
    }

    /**
     * Get current validation errors.
     */
    getErrors() {
        return Array.from(this.errors.entries()).map(([field, error]) => ({ field, error }));
    }

    /**
     * Check if form is valid.
     */
    isValid() {
        return this.errors.size === 0 && this.validateForm();
    }
}

// Auto-initialize form validation on page load
document.addEventListener('DOMContentLoaded', function() {
    // Initialize validation for customer forms
    const customerForms = document.querySelectorAll('form[data-validation="customer"]');
    customerForms.forEach(form => {
        new FormValidator(form, {
            validateOnInput: true,
            validateOnBlur: true,
            showErrorsInline: true
        });
    });

    // Initialize validation for payment forms
    const paymentForms = document.querySelectorAll('form[data-validation="payment"]');
    paymentForms.forEach(form => {
        new FormValidator(form, {
            validateOnInput: true,
            validateOnBlur: true,
            showErrorsInline: true
        });
    });

    // Initialize validation for confirmation forms
    const confirmationForms = document.querySelectorAll('form[data-validation="confirmation"]');
    confirmationForms.forEach(form => {
        new FormValidator(form, {
            validateOnInput: false, // Don't validate on input for confirmation forms
            validateOnBlur: true,
            showErrorsInline: true
        });
    });
});

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = FormValidator;
}