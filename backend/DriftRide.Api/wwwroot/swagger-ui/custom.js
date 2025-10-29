// DriftRide API Swagger UI Custom JavaScript

(function() {
    'use strict';

    // Wait for Swagger UI to initialize
    window.addEventListener('DOMContentLoaded', function() {
        // Add custom functionality after a short delay to ensure Swagger UI is ready
        setTimeout(initializeDriftRideEnhancements, 1000);
    });

    function initializeDriftRideEnhancements() {
        console.log('Initializing DriftRide Swagger UI enhancements...');

        // Add JWT token management
        addJWTTokenManagement();

        // Add custom header with authentication status
        addAuthenticationStatusHeader();

        // Add keyboard shortcuts
        addKeyboardShortcuts();

        // Add response time tracking
        addResponseTimeTracking();

        // Add local storage for API preferences
        addLocalStorageSupport();

        // Add copy buttons for examples
        addCopyButtons();

        // Add enhanced error handling
        addEnhancedErrorHandling();

        console.log('DriftRide Swagger UI enhancements initialized');
    }

    function addJWTTokenManagement() {
        // Store JWT token in localStorage when authorized
        const originalAuthorize = window.ui?.authActions?.authorize;
        if (originalAuthorize) {
            window.ui.authActions.authorize = function(auth) {
                const result = originalAuthorize.call(this, auth);

                // Check if Bearer token was set
                if (auth && auth.Bearer && auth.Bearer.value) {
                    localStorage.setItem('driftride_jwt_token', auth.Bearer.value);
                    showNotification('JWT token saved successfully', 'success');
                    updateAuthenticationStatus(true);
                }

                return result;
            };
        }

        // Auto-populate JWT token from localStorage
        const savedToken = localStorage.getItem('driftride_jwt_token');
        if (savedToken) {
            // Wait for UI to be ready and then set the token
            setTimeout(() => {
                const authInput = document.querySelector('input[placeholder="Value"]');
                if (authInput && !authInput.value) {
                    authInput.value = savedToken;
                    updateAuthenticationStatus(true);
                }
            }, 2000);
        }

        // Clear token on logout
        const originalLogout = window.ui?.authActions?.logout;
        if (originalLogout) {
            window.ui.authActions.logout = function(auth) {
                const result = originalLogout.call(this, auth);
                localStorage.removeItem('driftride_jwt_token');
                showNotification('JWT token cleared', 'info');
                updateAuthenticationStatus(false);
                return result;
            };
        }
    }

    function addAuthenticationStatusHeader() {
        const topbar = document.querySelector('.swagger-ui .topbar');
        if (!topbar) return;

        const statusContainer = document.createElement('div');
        statusContainer.id = 'auth-status-container';
        statusContainer.style.cssText = `
            position: absolute;
            right: 20px;
            top: 50%;
            transform: translateY(-50%);
            display: flex;
            align-items: center;
            gap: 10px;
            color: white;
            font-weight: 500;
        `;

        const statusIcon = document.createElement('span');
        statusIcon.id = 'auth-status-icon';
        statusIcon.style.fontSize = '1.2em';

        const statusText = document.createElement('span');
        statusText.id = 'auth-status-text';

        statusContainer.appendChild(statusIcon);
        statusContainer.appendChild(statusText);
        topbar.appendChild(statusContainer);

        updateAuthenticationStatus(!!localStorage.getItem('driftride_jwt_token'));
    }

    function updateAuthenticationStatus(isAuthenticated) {
        const statusIcon = document.getElementById('auth-status-icon');
        const statusText = document.getElementById('auth-status-text');

        if (!statusIcon || !statusText) return;

        if (isAuthenticated) {
            statusIcon.textContent = '🔓';
            statusText.textContent = 'Authenticated';
            statusIcon.title = 'API requests will include JWT token';
        } else {
            statusIcon.textContent = '🔒';
            statusText.textContent = 'Not Authenticated';
            statusIcon.title = 'Login required for protected endpoints';
        }
    }

    function addKeyboardShortcuts() {
        document.addEventListener('keydown', function(event) {
            // Ctrl+Enter or Cmd+Enter to execute current request
            if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
                const executeButton = document.querySelector('.swagger-ui .btn.execute');
                if (executeButton && !executeButton.disabled) {
                    executeButton.click();
                    event.preventDefault();
                }
            }

            // Escape to close modals
            if (event.key === 'Escape') {
                const modal = document.querySelector('.swagger-ui .modal-ux');
                if (modal) {
                    const closeButton = modal.querySelector('.modal-ux-header .close-modal');
                    if (closeButton) {
                        closeButton.click();
                    }
                }
            }

            // Ctrl+/ or Cmd+/ to focus search (if implemented)
            if ((event.ctrlKey || event.metaKey) && event.key === '/') {
                const searchInput = document.querySelector('input[placeholder*="search"]');
                if (searchInput) {
                    searchInput.focus();
                    event.preventDefault();
                }
            }
        });

        // Add keyboard shortcut hints
        addKeyboardShortcutHelp();
    }

    function addKeyboardShortcutHelp() {
        const info = document.querySelector('.swagger-ui .info');
        if (!info) return;

        const shortcutsDiv = document.createElement('div');
        shortcutsDiv.style.cssText = `
            margin-top: 15px;
            padding: 10px;
            background: #f0f9ff;
            border-left: 4px solid #0ea5e9;
            border-radius: 4px;
            font-size: 0.9em;
        `;

        shortcutsDiv.innerHTML = `
            <strong>⌨️ Keyboard Shortcuts:</strong><br>
            <code>Ctrl+Enter</code> Execute current request<br>
            <code>Escape</code> Close modals<br>
            <code>Ctrl+/</code> Focus search
        `;

        info.appendChild(shortcutsDiv);
    }

    function addResponseTimeTracking() {
        // Store original fetch function
        const originalFetch = window.fetch;

        window.fetch = function(...args) {
            const startTime = performance.now();

            return originalFetch.apply(this, args).then(response => {
                const endTime = performance.now();
                const responseTime = Math.round(endTime - startTime);

                // Add response time to the response display
                setTimeout(() => {
                    addResponseTimeDisplay(responseTime);
                }, 100);

                return response;
            });
        };
    }

    function addResponseTimeDisplay(responseTime) {
        const responseSection = document.querySelector('.swagger-ui .responses-wrapper .live-responses-table');
        if (!responseSection) return;

        // Remove existing response time display
        const existingDisplay = responseSection.querySelector('.response-time-display');
        if (existingDisplay) {
            existingDisplay.remove();
        }

        // Add new response time display
        const timeDisplay = document.createElement('div');
        timeDisplay.className = 'response-time-display';
        timeDisplay.style.cssText = `
            margin: 10px 0;
            padding: 8px 12px;
            background: #f0f9ff;
            border-radius: 4px;
            font-size: 0.9em;
            color: #0369a1;
            border-left: 3px solid #0ea5e9;
        `;
        timeDisplay.innerHTML = `⏱️ Response time: <strong>${responseTime}ms</strong>`;

        responseSection.insertBefore(timeDisplay, responseSection.firstChild);
    }

    function addLocalStorageSupport() {
        // Save and restore Try It Out states
        const originalTryItOut = document.querySelectorAll.bind(document);

        // Monitor Try It Out button clicks
        document.addEventListener('click', function(event) {
            if (event.target.classList.contains('try-out__btn')) {
                const operation = event.target.closest('.opblock');
                const operationId = operation?.querySelector('.opblock-summary-operation-id')?.textContent;

                if (operationId) {
                    const tryItOutStates = JSON.parse(localStorage.getItem('driftride_tryitout_states') || '{}');
                    const isActive = event.target.textContent.trim() === 'Cancel';

                    if (isActive) {
                        delete tryItOutStates[operationId];
                    } else {
                        tryItOutStates[operationId] = true;
                    }

                    localStorage.setItem('driftride_tryitout_states', JSON.stringify(tryItOutStates));
                }
            }
        });

        // Restore Try It Out states on page load
        setTimeout(restoreTryItOutStates, 2000);
    }

    function restoreTryItOutStates() {
        const tryItOutStates = JSON.parse(localStorage.getItem('driftride_tryitout_states') || '{}');

        Object.keys(tryItOutStates).forEach(operationId => {
            const operation = document.querySelector(`[data-operation-id="${operationId}"]`);
            if (operation) {
                const tryItOutBtn = operation.querySelector('.try-out__btn');
                if (tryItOutBtn && tryItOutBtn.textContent.trim() === 'Try it out') {
                    tryItOutBtn.click();
                }
            }
        });
    }

    function addCopyButtons() {
        // Add copy buttons for code examples
        setTimeout(() => {
            const codeBlocks = document.querySelectorAll('.swagger-ui .highlight-code');

            codeBlocks.forEach(block => {
                if (block.querySelector('.copy-button')) return; // Already has copy button

                const copyButton = document.createElement('button');
                copyButton.className = 'copy-button';
                copyButton.textContent = '📋 Copy';
                copyButton.style.cssText = `
                    position: absolute;
                    top: 10px;
                    right: 10px;
                    background: #2563eb;
                    color: white;
                    border: none;
                    padding: 4px 8px;
                    border-radius: 4px;
                    font-size: 0.8em;
                    cursor: pointer;
                    z-index: 10;
                `;

                copyButton.addEventListener('click', () => {
                    const code = block.textContent;
                    navigator.clipboard.writeText(code).then(() => {
                        showNotification('Code copied to clipboard', 'success');
                        copyButton.textContent = '✅ Copied';
                        setTimeout(() => {
                            copyButton.textContent = '📋 Copy';
                        }, 2000);
                    });
                });

                block.style.position = 'relative';
                block.appendChild(copyButton);
            });
        }, 1000);
    }

    function addEnhancedErrorHandling() {
        // Monitor for error responses and provide helpful feedback
        const observer = new MutationObserver(function(mutations) {
            mutations.forEach(function(mutation) {
                mutation.addedNodes.forEach(function(node) {
                    if (node.nodeType === 1 && node.classList?.contains('response')) {
                        setTimeout(() => checkForErrors(node), 100);
                    }
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    function checkForErrors(responseNode) {
        const statusElement = responseNode.querySelector('.response-col_status');
        if (!statusElement) return;

        const status = statusElement.textContent;
        const isError = ['400', '401', '403', '404', '409', '422', '500'].includes(status);

        if (isError) {
            const helpText = getErrorHelp(status);
            if (helpText) {
                addErrorHelp(responseNode, helpText);
            }
        }
    }

    function getErrorHelp(status) {
        const errorHelp = {
            '400': 'Check your request parameters and body format. Ensure all required fields are included.',
            '401': 'Authentication required. Click the Authorize button and provide your JWT token.',
            '403': 'You don\'t have permission for this operation. Check your user role.',
            '404': 'The requested resource was not found. Verify the ID or endpoint path.',
            '409': 'Resource conflict. This often means the resource already exists.',
            '422': 'Request is valid but business rules prevent processing. Check the error details.',
            '500': 'Server error. Please try again or contact support if the issue persists.'
        };

        return errorHelp[status];
    }

    function addErrorHelp(responseNode, helpText) {
        const existingHelp = responseNode.querySelector('.error-help');
        if (existingHelp) return;

        const helpDiv = document.createElement('div');
        helpDiv.className = 'error-help';
        helpDiv.style.cssText = `
            margin-top: 10px;
            padding: 10px;
            background: #fef2f2;
            border-left: 4px solid #ef4444;
            border-radius: 4px;
            color: #991b1b;
            font-size: 0.9em;
        `;
        helpDiv.innerHTML = `<strong>💡 Help:</strong> ${helpText}`;

        responseNode.appendChild(helpDiv);
    }

    function showNotification(message, type = 'info') {
        const notification = document.createElement('div');
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            padding: 12px 20px;
            border-radius: 6px;
            color: white;
            font-weight: 500;
            z-index: 9999;
            max-width: 300px;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
            transform: translateX(100%);
            transition: transform 0.3s ease;
        `;

        const colors = {
            success: '#059669',
            error: '#dc2626',
            warning: '#d97706',
            info: '#2563eb'
        };

        notification.style.backgroundColor = colors[type] || colors.info;
        notification.textContent = message;

        document.body.appendChild(notification);

        // Animate in
        setTimeout(() => {
            notification.style.transform = 'translateX(0)';
        }, 100);

        // Remove after 3 seconds
        setTimeout(() => {
            notification.style.transform = 'translateX(100%)';
            setTimeout(() => {
                document.body.removeChild(notification);
            }, 300);
        }, 3000);
    }

    // Add global helper functions
    window.DriftRideSwagger = {
        copyAuthToken: function() {
            const token = localStorage.getItem('driftride_jwt_token');
            if (token) {
                navigator.clipboard.writeText(token);
                showNotification('JWT token copied to clipboard', 'success');
            } else {
                showNotification('No JWT token found', 'warning');
            }
        },

        clearAuthToken: function() {
            localStorage.removeItem('driftride_jwt_token');
            showNotification('JWT token cleared', 'info');
            updateAuthenticationStatus(false);
        },

        exportApiSpec: function() {
            fetch('/swagger/v1/swagger.json')
                .then(response => response.json())
                .then(spec => {
                    const blob = new Blob([JSON.stringify(spec, null, 2)], { type: 'application/json' });
                    const url = URL.createObjectURL(blob);
                    const a = document.createElement('a');
                    a.href = url;
                    a.download = 'driftride-api-spec.json';
                    a.click();
                    URL.revokeObjectURL(url);
                    showNotification('API specification exported', 'success');
                });
        }
    };

    console.log('DriftRide Swagger helpers available at window.DriftRideSwagger');
})();