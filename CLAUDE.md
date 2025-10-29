# Drift Car Queue Management System - Development Context

## Project Overview
Cloud-based drift car queue management system with local desktop synchronization. Multi-role architecture supporting Customer, Sales, and Driver workflows with real-time updates.

## Technology Stack
- **Backend**: ASP.NET Core Web API (.NET 8.0)
- **Database**: Entity Framework Core with SQL Server
- **Real-time**: SignalR for live updates
- **Authentication**: JWT with role-based authorization
- **Frontend**: Mobile-responsive web (ASP.NET Core MVC)
- **Desktop**: WPF synchronization application
- **Testing**: xUnit, ASP.NET Core TestHost
- **Deployment**: Azure App Service, Azure SQL Database

## API Schema Map

### Core Entities
- **Customer**: `{id, name, phoneNumber, createdAt}` - Person wanting ride (duplicates allowed, distinguished by timestamp)
- **Payment**: `{id, customerId, amount, paymentMethod, status, externalTransactionId}` - Payment records with manual/API verification
- **QueueEntry**: `{id, customerId, paymentId, position, status, queuedAt}` - Queue positions (desktop-authoritative)
- **PaymentConfiguration**: `{id, paymentMethod, displayName, paymentUrl, isEnabled, pricePerRide, apiIntegrationEnabled, apiCredentials}` - Payment setup with optional API integration
- **User**: `{id, username, displayName, role, isActive}` - Sales/Driver accounts

### API Endpoints
- `POST /auth/login` - User authentication
- `POST /customers` - Create customer record
- `POST /customers/manual` - Sales manually adds customer (payment failure fallback)
- `POST /payments` - Record payment attempt
- `POST /payments/{id}/confirm` - Sales confirms/denies payment (manual verification default)
- `GET /payments/pending` - Sales view pending payments
- `GET /queue` - Current queue state
- `POST /queue/{id}/complete` - Driver completes ride
- `DELETE /queue/{id}/remove` - Sales removes customer (no-show handling)
- `POST /queue/reorder` - Sales reorders queue
- `GET /queue/current` - Driver current customer view
- `GET /configuration/payment-methods` - Get payment config
- `PUT /configuration/payment-methods` - Update payment config (includes API integration settings)

## File Structure Index

### Specification Files (`/specs/001-creeate-an-app/`)
- `spec.md` - Feature requirements and user stories
- `plan.md` - Implementation plan and technical context
- `research.md` - Technology decisions and architecture rationale
- `data-model.md` - Entity definitions and relationships
- `quickstart.md` - End-to-end testing scenarios
- `contracts/api.yaml` - OpenAPI specification

### Project Structure (To Be Created)
```
backend/
├── src/
│   ├── models/ - Entity classes (Customer, Payment, QueueEntry, etc.)
│   ├── services/ - Business logic services
│   ├── api/ - Controllers and endpoints
│   └── data/ - EF Core DbContext and configurations
└── tests/
    ├── contract/ - API contract tests
    ├── integration/ - End-to-end integration tests
    └── unit/ - Unit tests for services/models

frontend/
├── src/
│   ├── components/ - Shared UI components
│   ├── pages/ - Customer/Sales/Driver views
│   └── services/ - API communication services
└── tests/

desktop/
├── src/
│   ├── models/ - Data models matching API
│   ├── services/ - Sync and offline services
│   └── views/ - WPF UI components
└── tests/
```

## Variables & Functions Tracking

### Core Functions (Implemented)
- `CustomerService.CreateAsync(name, phone)` - Creates customer record (allows duplicates) - /backend/src/services/CustomerService.cs
- `CustomerService.AddManuallyAsync(name, phone, reason, staffId)` - Manual customer addition for payment failures - /backend/src/services/CustomerService.cs
- `CustomerService.GetByIdAsync(customerId)` - Retrieves customer by ID - /backend/src/services/CustomerService.cs
- `CustomerService.GetByNameAsync(name, fromDate, toDate)` - Retrieves customers by name with date filters - /backend/src/services/CustomerService.cs
- `CustomerService.ValidateCustomerDataAsync(name, phone)` - Validates customer data before creation - /backend/src/services/CustomerService.cs
- `PaymentService.ProcessAsync(customerId, amount, method)` - Records payment attempt - /backend/src/services/PaymentService.cs
- `PaymentService.ConfirmAsync(paymentId, confirmed, notes, staffUsername)` - Sales confirmation (manual/API) - /backend/src/services/PaymentService.cs
- `PaymentService.VerifyAutomaticallyAsync(paymentId)` - API-based payment verification (placeholder) - /backend/src/services/PaymentService.cs
- `PaymentService.GetPendingAsync()` - Gets payments awaiting confirmation - /backend/src/services/PaymentService.cs
- `PaymentService.GetByIdAsync(paymentId, includeCustomer)` - Retrieves payment by ID - /backend/src/services/PaymentService.cs
- `PaymentService.GetByCustomerAsync(customerId)` - Gets payment history for customer - /backend/src/services/PaymentService.cs
- `PaymentService.ValidatePaymentDataAsync()` - Validates payment data - /backend/src/services/PaymentService.cs
- `PaymentService.HasActivePendingPaymentAsync(customerId)` - Checks for pending payments - /backend/src/services/PaymentService.cs
- `QueueService.AddToQueueAsync(paymentId)` - Adds confirmed payment to queue - /backend/src/services/QueueService.cs
- `QueueService.CompleteRideAsync(queueEntryId, driverUsername)` - Marks ride complete - /backend/src/services/QueueService.cs
- `QueueService.RemoveCustomerAsync(queueEntryId, reason, staffUsername)` - Remove customer (no-show handling) - /backend/src/services/QueueService.cs
- `QueueService.ReorderAsync(queueOrder, staffUsername)` - Manual queue reordering - /backend/src/services/QueueService.cs
- `QueueService.SyncFromDesktopAsync(queueState)` - Desktop-to-cloud sync (desktop authoritative) - /backend/src/services/QueueService.cs
- `QueueService.GetCurrentQueueAsync(includeCompleted)` - Gets current queue state - /backend/src/services/QueueService.cs
- `QueueService.GetNextCustomerAsync()` - Gets next customer for driver - /backend/src/services/QueueService.cs
- `QueueService.StartRideAsync(queueEntryId, driverUsername)` - Starts a ride - /backend/src/services/QueueService.cs
- `QueueService.RecalculatePositionsAsync()` - Fixes queue position continuity - /backend/src/services/QueueService.cs
- `UserService.AuthenticateAsync(username, password)` - Authenticates user with credentials - /backend/src/services/UserService.cs
- `UserService.CreateAsync(username, password, displayName, role)` - Creates new user account - /backend/src/services/UserService.cs
- `UserService.UpdateAsync(userId, displayName, role, isActive)` - Updates user information - /backend/src/services/UserService.cs
- `UserService.ChangePasswordAsync(userId, currentPassword, newPassword)` - Changes user password - /backend/src/services/UserService.cs
- `UserService.GetActiveUsersAsync(role)` - Gets active users by role - /backend/src/services/UserService.cs
- `UserService.ValidateUserDataAsync()` - Validates user data - /backend/src/services/UserService.cs
- `UserService.ValidatePasswordComplexityAsync(password)` - Validates password requirements - /backend/src/services/UserService.cs
- `UserService.IsUsernameAvailableAsync(username, existingUserId)` - Checks username availability - /backend/src/services/UserService.cs
- `NotificationService.NotifyQueueUpdateAsync(queueEntries)` - Broadcasts queue updates via SignalR - /backend/src/services/NotificationService.cs
- `NotificationService.NotifyPaymentStatusAsync(payment)` - Notifies payment status changes - /backend/src/services/NotificationService.cs
- `NotificationService.NotifyRideStatusAsync(queueEntry)` - Notifies ride status changes - /backend/src/services/NotificationService.cs
- `NotificationService.NotifySystemMessageAsync(message, targetRole, priority)` - Sends system messages - /backend/src/services/NotificationService.cs
- `NotificationService.NotifyCustomerAttentionAsync(customer, reason)` - Alerts sales about customer attention needed - /backend/src/services/NotificationService.cs
- `NotificationService.NotifyDriverQueueUpdateAsync(nextCustomer, queueLength)` - Updates driver queue view - /backend/src/services/NotificationService.cs
- `NotificationService.SendUserNotificationAsync(userId, message, type, data)` - Sends targeted user notifications - /backend/src/services/NotificationService.cs
- `NotificationService.NotifyConfigurationChangeAsync(configType, configData)` - Broadcasts config changes
- `NotificationService.NotifyNewPaymentAsync(payment)` - Notifies sales staff about new payment submissions - Enhanced for User Story 1
- `NotificationService.NotifyCustomerQueuePositionAsync(customerId, position, waitTime)` - Notifies customer about queue position updates - Enhanced for User Story 1
- `NotificationService.NotifyCustomerAlertAsync(customerId, alertType, message, priority)` - Sends customer attention alerts to sales staff - Enhanced for User Story 1
- `NotificationService.NotifyQueueStatisticsAsync(totalInQueue, pendingPayments, avgWaitTime)` - Broadcasts real-time queue statistics - Enhanced for User Story 1
- `NotificationService.NotifyServiceIssueAsync(issueType, message, severity)` - Notifies about connection/service disruptions - Enhanced for User Story 1 - /backend/src/services/NotificationService.cs

### JWT Authentication Functions (Implemented)
- `JwtService.GenerateToken(user)` - Creates JWT access token with role-based claims for authenticated user
- `JwtService.ValidateToken(token)` - Validates JWT token and extracts user information for middleware
- `JwtService.RefreshToken(refreshToken, user)` - Refreshes access token using refresh token
- `JwtService.GenerateRefreshToken()` - Generates secure refresh token for token renewal
- `JwtMiddleware.InvokeAsync(context, jwtService)` - Validates JWT tokens from Authorization header and sets HttpContext.User
- `JwtMiddleware.ExtractTokenFromHeader(context)` - Extracts Bearer token from Authorization header

### Service Interfaces (Implemented)
- `ICustomerService` - Customer management operations interface - /backend/src/services/ICustomerService.cs
- `IPaymentService` - Payment processing and verification interface - /backend/src/services/IPaymentService.cs
- `IQueueService` - Queue management and operations interface - /backend/src/services/IQueueService.cs
- `IUserService` - User authentication and management interface - /backend/src/services/IUserService.cs
- `INotificationService` - SignalR notifications interface - /backend/src/services/INotificationService.cs

### SignalR Hubs (Enhanced for User Story 1)
- `QueueHub` - Real-time communication hub with role-based groups and payment workflow support - /backend/src/hubs/QueueHub.cs
- `QueueHub.OnConnectedAsync()` - Adds connections to role-based groups and customer groups for unauthenticated users
- `QueueHub.OnDisconnectedAsync()` - Removes connections from groups
- `QueueHub.JoinGroup(groupName)` - Allows clients to join additional groups
- `QueueHub.LeaveGroup(groupName)` - Allows clients to leave groups
- `QueueHub.JoinPaymentGroup(paymentId)` - Allows customers to join payment-specific groups for targeted notifications
- `QueueHub.LeavePaymentGroup(paymentId)` - Allows customers to leave payment-specific groups
- `QueueHub.JoinCustomerGroup(customerId)` - Allows customers to join customer-specific groups
- `QueueHub.Heartbeat()` - Connection monitoring method for real-time health checks
- `QueueHub.RequestConnectionStatus()` - Allows clients to request current connection information

### SignalR Notification Models (Enhanced for User Story 1)
- `QueueUpdateNotification` - Queue position changes and updates - /backend/src/models/QueueUpdateNotification.cs
- `PaymentNotification` - Payment status changes and confirmations with enhanced customer targeting - /backend/src/models/PaymentNotification.cs
- `CustomerNotification` - Customer-related events and alerts - /backend/src/models/CustomerNotification.cs
- `RideNotification` - Ride status changes and driver updates - /backend/src/models/RideNotification.cs
- `NotificationDisplayInfo` - Comprehensive notification display model for UI rendering - /backend/src/models/NotificationDisplayInfo.cs
- `QueueUpdateType` - Enumeration of queue update types (CustomerAdded, CustomerRemoved, QueueReordered, etc.)
- `CustomerNotificationType` - Enumeration of customer notification types (PaymentVerificationRequired, PaymentDenied, etc.)
- `RideNotificationType` - Enumeration of ride notification types (CustomerReady, RideStarted, RideCompleted, etc.)
- `NotificationType` - Enumeration for UI notification types (PaymentStatusUpdate, NewPayment, QueuePositionUpdate, etc.)
- `SoundType` - Enumeration for notification sound types (Default, Success, Warning, Error, Critical, etc.)
- `NotificationStyle` - Enumeration for visual notification styles (Default, Compact, Expanded, Banner, Toast, Modal)
- `NotificationAction` - Model for notification action buttons with configurable behavior

### Dependency Injection Configuration (Implemented)
- `ICustomerService` registered as Scoped - Per HTTP request lifecycle - /backend/DriftRide.Api/Program.cs
- `IPaymentService` registered as Scoped - Per HTTP request lifecycle - /backend/DriftRide.Api/Program.cs
- `IQueueService` registered as Scoped - Per HTTP request lifecycle - /backend/DriftRide.Api/Program.cs
- `IUserService` registered as Scoped - Per HTTP request lifecycle - /backend/DriftRide.Api/Program.cs
- `INotificationService` registered as Singleton - SignalR hub context requires singleton - /backend/DriftRide.Api/Program.cs
- `IJwtService` registered as Scoped - Per HTTP request lifecycle - /backend/DriftRide.Api/Program.cs
- `DriftRideDbContext` registered as Scoped - Entity Framework context lifecycle - /backend/DriftRide.Api/Program.cs
- `QueueHub` mapped at `/queueHub` - SignalR endpoint configuration - /backend/DriftRide.Api/Program.cs
- SignalR configured with 5-second keep-alive for real-time sync requirement - /backend/DriftRide.Api/Program.cs
- JWT authentication configured for SignalR connections via access_token query parameter - /backend/DriftRide.Api/Program.cs

### Key Constants
- `PaymentMethod.CashApp, PayPal, CashInHand` - Payment types
- `PaymentStatus.Pending, Confirmed, Denied` - Payment states
- `QueueStatus.Waiting, InProgress, Completed, Cancelled` - Queue states
- `UserRole.Sales, Driver` - User access roles
- `NotificationPriority.Info, Warning, Error, Critical` - Notification priority levels

### JWT Models (Implemented)
- `LoginRequest` - User authentication request model with username and password
- `LoginResponse` - Authentication response with access token, refresh token, expiration, and user info
- `UserInfo` - User information subset for login response (id, username, displayName, role)
- `TokenValidationResult` - JWT validation result with user claims, principal, and error handling
- `DriftRideAuthorizeAttribute` - Custom authorization attribute for role-based access control
- `DriftRideAuthorizationFilter` - Authorization filter implementing role-based access control logic

### API Request/Response Models (Implemented)
- `CreateCustomerRequest` - Customer creation request model with validation - /backend/DriftRide.Api/Models/CreateCustomerRequest.cs
- `Customer` - Customer API response model (differs from domain model) - /backend/DriftRide.Api/Models/Customer.cs

### Security Middleware (Implemented)
- `SecurityHeadersMiddleware` - Applies security headers (CSP, HSTS, X-Frame-Options, etc.) - /backend/src/api/middleware/SecurityHeadersMiddleware.cs
- `RateLimitingMiddleware` - API rate limiting per client/endpoint type - /backend/src/api/middleware/RateLimitingMiddleware.cs
- `InputValidationMiddleware` - Request validation and attack prevention - /backend/src/api/middleware/InputValidationMiddleware.cs
- `SecuritySettings` - Configuration model for security headers
- `RateLimitSettings` - Configuration model for rate limiting policies
- `InputValidationSettings` - Configuration model for input validation rules

### Security Features (Implemented)
- **CORS Configuration**: Frontend origins (localhost:3000, 5000, 8080), SignalR headers, credentials support
- **Content Security Policy**: Restrictive CSP with self-origin, inline scripts/styles for development
- **Security Headers**: X-Frame-Options (DENY), X-Content-Type-Options (nosniff), Referrer-Policy, Permissions-Policy
- **Rate Limiting**: Tiered limits (General: 60/min 1000/hr, Auth: 5/min 20/hr, Payment: 10/min 50/hr)
- **Input Validation**: Request size limits (1MB), SQL injection protection, XSS protection, URL/query length limits
- **Request Pipeline**: Optimized security middleware order for performance and protection

### API Infrastructure (Implemented)
- **BaseApiController**: Abstract base controller with authentication, common response methods, error handling - /backend/src/api/controllers/BaseApiController.cs
- **ApiResponse<T>**: Generic response wrapper with success/error states, timestamps - /backend/src/models/ApiResponse.cs
- **ErrorResponse**: Structured error details with codes, messages, and optional debugging info - /backend/src/models/ErrorResponse.cs
- **PagedResponse<T>**: Paginated response with navigation metadata and links - /backend/src/models/PagedResponse.cs
- **PaginationRequest**: Request model for pagination parameters with validation - /backend/src/models/PagedResponse.cs
- **ValidationErrorResponse**: Specialized error response for model validation failures - /backend/src/models/ErrorResponse.cs
- **Controller Configuration**: JSON serialization, model validation, and automatic error formatting - /backend/DriftRide.Api/Program.cs

### API Controller Helpers (Implemented)
- `BaseApiController.CurrentUserId` - Gets authenticated user ID from JWT claims
- `BaseApiController.CurrentUsername` - Gets authenticated username from JWT claims
- `BaseApiController.CurrentUserRole` - Gets authenticated user role from JWT claims
- `BaseApiController.CurrentUserDisplayName` - Gets authenticated user display name from JWT claims
- `BaseApiController.HasRole(role)` - Checks if current user has specific role
- `BaseApiController.HasAnyRole(roles...)` - Checks if current user has any of specified roles
- `BaseApiController.Success<T>(data, message)` - Creates successful response with data
- `BaseApiController.Success(message)` - Creates successful response without data
- `BaseApiController.SuccessPaged<T>()` - Creates successful paginated response
- `BaseApiController.BadRequestError()` - Creates 400 error response
- `BaseApiController.NotFoundError()` - Creates 404 error response
- `BaseApiController.UnauthorizedError()` - Creates 401 error response
- `BaseApiController.ForbiddenError()` - Creates 403 error response
- `BaseApiController.ConflictError()` - Creates 409 error response
- `BaseApiController.InternalServerError()` - Creates 500 error response
- `BaseApiController.ValidationError()` - Creates validation error from ModelState
- `BaseApiController.ExecuteAsync<T>()` - Executes operations with standard error handling

### API Controllers (Implemented)
- `CustomersController` - Customer management endpoints following OpenAPI specification - /backend/src/api/controllers/CustomersController.cs
- `CustomersController.CreateCustomer(request)` - POST /api/customers - Creates new customer with Sales role authorization
- `CustomersController.GetCustomer(id)` - GET /api/customers/{id} - Retrieves customer by ID with Sales/Driver role authorization
- `PaymentsController` - Payment processing and verification endpoints - /backend/src/api/controllers/PaymentsController.cs
- `PaymentsController.CreatePayment(request)` - POST /api/payments - Records payment attempt (Sales role required)
- `PaymentsController.ConfirmPayment(id, request)` - POST /api/payments/{id}/confirm - Sales confirms/denies payment
- `PaymentsController.GetPendingPayments()` - GET /api/payments/pending - Retrieves payments awaiting confirmation

### Frontend Controllers (Implemented)
- `CustomerController` - Customer registration and payment flow - /frontend/DriftRide.Web/Controllers/CustomerController.cs
- `SalesController` - Sales staff operations and payment verification - /frontend/DriftRide.Web/Controllers/SalesController.cs
- `SalesController.Login(model)` - Sales staff authentication with JWT token management
- `SalesController.Dashboard()` - Main sales dashboard with pending payments and queue management
- `SalesController.ConfirmPayment(model)` - AJAX endpoint for payment confirmation/denial
- `SalesController.AddCustomerManually(model)` - Manual customer addition for payment failures
- `SalesController.SearchCustomers(searchTerm)` - Customer lookup functionality
- `SalesController.GetPendingPayments()` - AJAX endpoint for real-time payment updates
- `SalesController.GetQueueStatus()` - AJAX endpoint for queue status updates

### Frontend Services (Implemented)
- `IDriftRideApiService` - API communication interface - /frontend/DriftRide.Web/Services/IDriftRideApiService.cs
- `DriftRideApiService` - API service implementation with sales endpoints - /frontend/DriftRide.Web/Services/DriftRideApiService.cs
- `DriftRideApiService.LoginAsync(request)` - Sales staff authentication
- `DriftRideApiService.GetPendingPaymentsAsync(authToken)` - Retrieves pending payments (Sales role required)
- `DriftRideApiService.ConfirmPaymentAsync(paymentId, request, authToken)` - Confirms/denies payments
- `DriftRideApiService.AddCustomerManuallyAsync(request, authToken)` - Manual customer addition
- `DriftRideApiService.SearchCustomersAsync(searchTerm, authToken)` - Customer search functionality

### Frontend Models (Implemented)
- `CustomerViewModel` - Customer registration and payment view model - /frontend/DriftRide.Web/Models/CustomerViewModel.cs
- `SalesDashboardViewModel` - Sales dashboard view model with performance metrics - /frontend/DriftRide.Web/Models/SalesDashboardViewModel.cs
- `SalesLoginViewModel` - Sales authentication view model - /frontend/DriftRide.Web/Controllers/SalesController.cs
- `PaymentConfirmationModel` - AJAX model for payment verification - /frontend/DriftRide.Web/Controllers/SalesController.cs
- `ManualCustomerModel` - Manual customer addition model - /frontend/DriftRide.Web/Controllers/SalesController.cs
- `ManualCustomerAdditionViewModel` - Manual customer form view model - /frontend/DriftRide.Web/Models/SalesDashboardViewModel.cs
- `PaymentVerificationViewModel` - Payment verification workflow model - /frontend/DriftRide.Web/Models/SalesDashboardViewModel.cs

### Sales-Specific API Models (Implemented)
- `SalesLoginRequest` - Sales authentication request - /frontend/DriftRide.Web/Services/IDriftRideApiService.cs
- `SalesLoginResponse` - Sales authentication response with JWT token - /frontend/DriftRide.Web/Services/IDriftRideApiService.cs
- `SalesUserInfo` - Sales user information - /frontend/DriftRide.Web/Services/IDriftRideApiService.cs
- `PendingPaymentResponse` - Pending payment with customer details and timing - /frontend/DriftRide.Web/Services/IDriftRideApiService.cs
- `PaymentConfirmationRequest` - Payment confirmation request - /frontend/DriftRide.Web/Services/IDriftRideApiService.cs
- `ManualCustomerRequest` - Manual customer addition request - /frontend/DriftRide.Web/Services/IDriftRideApiService.cs

### Frontend Views (Implemented)
- `/Views/Sales/Login.cshtml` - Sales staff authentication page with enhanced security
- `/Views/Sales/Dashboard.cshtml` - Comprehensive sales dashboard with tabs for different functions
- `/Views/Sales/_PaymentItem.cshtml` - Partial view for payment verification items with priority indicators
- Updated `/Views/Shared/_Layout.cshtml` - Navigation links for customer portal and sales dashboard

### JavaScript Components (Enhanced for User Story 1)
- `sales-dashboard.js` - Sales dashboard controller with comprehensive SignalR integration - /frontend/DriftRide.Web/wwwroot/js/sales-dashboard.js
- `SalesDashboard` class - Enhanced with robust connection management, heartbeat monitoring, and error handling
- `customer-workflow.js` - Customer interface with enhanced real-time payment status updates - /frontend/DriftRide.Web/wwwroot/js/customer-workflow.js
- `CustomerWorkflow` class - Enhanced with automatic reconnection, payment group joining, and queue position updates
- `notification-system.js` - Comprehensive notification display system with sound alerts - /frontend/DriftRide.Web/wwwroot/js/notification-system.js
- `NotificationSystem` class - Advanced notification management with priority handling, accessibility features, and responsive design
- Enhanced SignalR integration with automatic reconnection using exponential backoff ([0, 2000, 10000, 30000, 60000]ms)
- Heartbeat monitoring every 30 seconds for connection health tracking
- Payment-specific and customer-specific group management for targeted notifications
- Comprehensive error handling for connection failures with retry logic
- Sound notification system with priority-based alerts and volume control
- Visual indicators for connection status, queue statistics, and customer alerts
- Keyboard shortcuts: A (approve), D (deny), Space (next), Esc (clear), F5 (refresh), U (urgent filter), F9 (toggle sounds)
- Accessibility features: ARIA labels, keyboard navigation, screen reader support

### Sales Dashboard Features (Implemented)
- **Performance Metrics Dashboard**: Real-time counters for pending payments, urgent alerts (>5min, >10min), queue length
- **Payment Verification Interface**: Priority-sorted payments (urgent > moderate > recent) with one-click approval/denial
- **Queue Management**: Real-time queue display with customer status, position management, no-show removal
- **Manual Customer Addition**: Form for payment failure fallbacks with common reason templates
- **Customer Search**: Real-time search with debounced input, customer history access
- **Keyboard Shortcuts**: Full keyboard navigation optimized for 30-second confirmation target
- **Mobile Responsive Design**: Tablet-optimized interface with touch-friendly controls
- **Real-time Updates**: SignalR integration for live payment notifications and queue changes
- **Session Management**: Secure JWT token storage, automatic expiration handling, role verification

### Sales Workflow Optimization (Implemented)
- **30-Second Target**: Optimized UI for rapid payment processing with keyboard shortcuts and bulk operations
- **Priority System**: Urgent payments (>10min) highlighted with pulsing badges and separate display section
- **Quick Actions**: Bulk approve, next payment focus, urgent filtering, keyboard navigation
- **Performance Monitoring**: Real-time metrics tracking payments over time thresholds
- **Sound Alerts**: Configurable audio notifications for new payments and urgent status changes
- **Auto-refresh**: 30-second intervals maintaining real-time sync requirement from plan.md

### Enhanced SignalR Real-Time Features (User Story 1)
- **Comprehensive Payment Workflow Integration**: Real-time notifications for payment status changes, confirmations, and denials
- **Customer-Specific Targeting**: Payment and customer group subscriptions for precise notification delivery
- **Sales Staff Alerts**: Immediate notifications for new payments, customer attention requests, and queue statistics
- **Connection Resilience**: Automatic reconnection with exponential backoff, heartbeat monitoring, and connection health indicators
- **Visual Status Indicators**: Real-time connection status, queue counters, and pending payment badges
- **Sound Alert System**: Priority-based audio notifications with volume control and accessibility support
- **Error Handling**: Comprehensive error recovery, retry logic, and service issue notifications
- **Notification Display System**: Advanced toast notifications with animations, priority indicators, and accessibility features
- **Mobile Responsiveness**: Touch-friendly controls and responsive notification positioning
- **Accessibility Support**: ARIA labels, keyboard navigation, high contrast support, and reduced motion preferences

## Development Guidelines
- Always reference this document before creating new functions/variables
- Follow constitution requirements for .NET development
- Use async/await for all database operations
- Implement comprehensive error handling
- Write tests first (TDD approach)
- Validate all API inputs against schema
- Maintain real-time sync requirements (5-second updates)

## Code Quality & Linting

### Build Commands
```bash
# Restore packages
dotnet restore

# Build with code analysis
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release

# Run complete quality check
./scripts/lint.sh        # Mac/Linux
./scripts/lint.ps1       # Windows PowerShell
```

### Code Analysis Configuration
- **EditorConfig**: `.editorconfig` - Consistent formatting across editors
- **Analyzers**: Microsoft.CodeAnalysis.NetAnalyzers, StyleCop.Analyzers
- **Ruleset**: `Custom.ruleset` - Balanced quality rules without excessive restrictions
- **Global Config**: `.globalanalyzer.config` - Solution-wide analyzer settings
- **StyleCop Config**: `stylecop.json` - StyleCop-specific settings

### Key Quality Standards
- **Error Level**: CA1000, CA1001, CA1049, CA1063, CA1065, CA2200, CA2219
- **Warning Level**: Most design, performance, security, and usage rules
- **Disabled**: CA1303 (localization), CA1707 (underscores), CA1812 (internal classes), CA2007 (ConfigureAwait)
- **StyleCop**: Enforced spacing, ordering, naming, layout rules (documentation disabled)

### Quality Rules Summary
1. **Null Safety**: Nullable reference types enabled, proper null handling required
2. **Performance**: Avoid unnecessary allocations, use efficient patterns
3. **Security**: SQL injection protection, proper exception handling
4. **Maintainability**: Avoid excessive complexity, use meaningful names
5. **Design**: Follow SOLID principles, proper inheritance patterns

### Pre-commit Requirements
- Zero build errors or warnings in Release configuration
- All enabled analyzer rules must pass
- Code formatting must match EditorConfig standards
- Tests must pass with no failures

## Clarification-Driven Decisions (2025-10-09)
- **Payment Verification**: Manual by default, optional API integration configurable
- **Duplicate Names**: Allowed, distinguished by timestamp, display with arrival context
- **No-Show Policy**: No refunds by default, manual override with audit trail
- **Offline Architecture**: Desktop authoritative for queue order, cloud for new customers
- **Payment Failures**: Sales staff can manually add customers with reason tracking