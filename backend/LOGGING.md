# DriftRide API Logging Implementation

## Overview

This document describes the comprehensive logging implementation for the DriftRide API, which provides structured logging, performance monitoring, business event tracking, and audit trails for User Story 1 operations.

## Logging Framework

**Primary Framework**: Serilog with ASP.NET Core integration
**Configuration**: JSON-based configuration with environment-specific overrides
**Output Formats**: Structured JSON logging with human-readable console output

## Key Features Implemented

### 1. Structured Logging with Correlation IDs
- **Correlation ID Generation**: Automatic generation and propagation of correlation IDs across requests
- **Request Tracking**: Full request/response lifecycle logging with timing information
- **Context Enrichment**: Automatic enrichment with user information, environment details, and process information

### 2. Service Layer Logging

#### Customer Service Logging
- **Customer Creation**: Full lifecycle logging from validation to database persistence
- **Manual Queue Addition**: Comprehensive logging for staff-initiated customer additions
- **Business Events**: Structured business event logging for customer registration and manual additions
- **Error Handling**: Detailed error logging with context for troubleshooting

#### Payment Service Logging
- **Payment Processing**: Complete payment lifecycle from creation to confirmation/denial
- **Payment Confirmation**: Staff action logging with audit trail capabilities
- **Business Events**: Payment creation, confirmation, and denial event logging
- **Transaction Logging**: Database transaction start/commit/rollback logging

### 3. Controller Layer Logging
- **Request/Response Logging**: Automatic logging via middleware for all API endpoints
- **User Action Logging**: Authentication and authorization event logging
- **Performance Monitoring**: Automatic detection and logging of slow requests (>1000ms)
- **Security Event Logging**: Failed authentication, authorization failures, and suspicious activity

### 4. Business Event Logging
Dedicated business event logging for compliance and analytics:

#### Customer Events
```
Business Event: Customer Registration - {CustomerId} {CustomerName} {CustomerEmail}
Business Event: Manual Queue Entry - {CustomerId} {CustomerName} Position:{Position} Staff:{StaffUsername} Reason:{Reason}
```

#### Payment Events
```
Business Event: Payment Creation - {PaymentId} {CustomerId} {Amount:C} {PaymentMethod} {ExternalTransactionId}
Business Event: Payment Confirmation - {PaymentId} {CustomerId} {Amount:C} Staff:{StaffUsername} Notes:{Notes}
Business Event: Payment Denial - {PaymentId} {CustomerId} {Amount:C} Staff:{StaffUsername} Notes:{Notes}
```

### 5. Performance Monitoring
- **Request Timing**: Automatic timing of all requests with millisecond precision
- **Slow Request Detection**: Automatic flagging of requests taking longer than 1000ms
- **Database Operation Timing**: EF Core query performance monitoring
- **Resource Usage Tracking**: Memory and thread usage monitoring

## Configuration

### Environment-Specific Configurations

#### Development (`appsettings.Development.json`)
- **Log Level**: Debug for DriftRide components, Information for Microsoft components
- **Console Output**: Detailed with correlation IDs and source context
- **File Logging**: Daily rolling files in `logs/development/` with 7-day retention
- **Seq Integration**: Optional Seq server integration for log analysis

#### Production (`appsettings.Production.json`)
- **Log Level**: Information for all components, Warning for Microsoft/System
- **Console Output**: Minimal structured output for container environments
- **File Logging**: Daily rolling files in `/app/logs/` with 90-day retention
- **Business Event Logs**: Separate log files for business events with 365-day retention
- **Size Limits**: 100MB application logs, 50MB business event logs with automatic rollover

### Log File Structure

```
logs/
├── development/
│   └── driftride-YYYYMMDD.log
├── driftride-YYYYMMDD.log (production application logs)
└── business-events/
    └── business-events-YYYYMMDD.log (production business events)
```

## Logging Middleware

### Features
- **Correlation ID Management**: Generates and propagates correlation IDs
- **Request/Response Logging**: Captures HTTP method, path, status code, and timing
- **User Context Enrichment**: Adds user ID, role, and authentication status
- **Sensitive Data Masking**: Automatically masks passwords, tokens, and transaction IDs
- **Performance Monitoring**: Identifies and logs slow requests

### Security Considerations
- **Header Filtering**: Excludes sensitive headers (Authorization, Cookie, API keys)
- **Parameter Filtering**: Excludes sensitive query parameters
- **Body Masking**: JSON body masking for sensitive fields
- **PII Protection**: Careful handling of customer personal information

## Log Context Enrichment

### Automatic Enrichment
- **Environment Information**: Machine name, environment name, process ID
- **Request Context**: Correlation ID, request ID, user information
- **Application Context**: Application name, version, component name
- **Timing Information**: Request start time, duration, operation timing

### Scoped Enrichment
- **Operation Scope**: Each service operation includes operation-specific context
- **Business Context**: Customer IDs, payment IDs, staff usernames
- **Transaction Context**: Database transaction IDs and status

## Monitoring and Alerting

### Log Levels and Usage

#### Debug
- Detailed flow information for development
- Database query details
- Transaction boundaries
- Internal state changes

#### Information
- Business events and successful operations
- User actions and API calls
- Performance metrics
- Normal application flow

#### Warning
- Validation failures
- Business rule violations
- Slow performance
- Recoverable errors

#### Error
- System exceptions
- Database errors
- External service failures
- Unhandled exceptions

#### Fatal
- Application startup/shutdown failures
- Critical system errors

### Key Metrics to Monitor
1. **Request Volume**: Number of API requests per minute/hour
2. **Response Times**: 95th percentile response times
3. **Error Rates**: 4xx and 5xx response percentages
4. **Business Events**: Customer registration and payment confirmation rates
5. **Security Events**: Failed authentication attempts

## Integration with Monitoring Systems

### Seq Integration (Development)
- Real-time log analysis and querying
- Dashboard creation for business metrics
- Alert configuration for error patterns

### Production Monitoring
- Log file shipping to centralized logging systems
- Integration with APM tools
- Alert configuration for critical events

## Compliance and Audit Trail

### Audit Requirements Met
- **User Actions**: Complete audit trail of staff actions
- **Data Changes**: Before/after state logging for critical operations
- **Business Events**: Immutable business event log for compliance
- **System Events**: Authentication, authorization, and access logging

### Data Retention
- **Application Logs**: 90 days in production, 7 days in development
- **Business Events**: 365 days for compliance requirements
- **Archive Strategy**: Automated compression and archival of old logs

## Troubleshooting Guide

### Common Log Analysis Queries

#### Find all operations for a customer
```
CorrelationId = "xxx" OR CustomerId = "xxx"
```

#### Performance analysis
```
@Level = "Warning" AND @Message like "*Slow request*"
```

#### Business events for a time period
```
@Message like "Business Event:*" AND @Timestamp >= "2024-01-01"
```

#### Error analysis
```
@Level in ["Error", "Fatal"] AND @Timestamp >= "2024-01-01"
```

### Log File Locations
- **Development**: `./logs/development/`
- **Production**: `/app/logs/`
- **Business Events**: `/app/logs/business-events/`

## Implementation Status

✅ **Completed Features**
- Serilog configuration and setup
- Request/response middleware with correlation IDs
- Service layer logging (Customer and Payment services)
- Controller layer logging via base controller
- Business event logging
- Environment-specific configurations
- Performance monitoring
- Security event logging

## Future Enhancements

### Planned Features
1. **Metrics Integration**: Prometheus metrics for operational dashboards
2. **Distributed Tracing**: OpenTelemetry integration for microservices
3. **Log Analytics**: Enhanced query capabilities and dashboards
4. **Automated Alerting**: Proactive alerting for system issues
5. **Log Encryption**: Encryption at rest for sensitive log data

### Monitoring Improvements
1. **Real-time Dashboards**: Business and operational metrics dashboards
2. **SLA Monitoring**: Service level agreement compliance tracking
3. **Capacity Planning**: Resource usage trending and forecasting

## Variables and Definitions

### Functions and Variables Used
| Name | Type | Description | References |
|------|------|-------------|------------|
| `LoggingMiddleware` | Class | Request/response logging middleware | Program.cs, middleware pipeline |
| `CorrelationId` | Property | Unique request identifier | LoggingMiddleware, all log contexts |
| `BeginScope` | Method | Creates scoped logging context | CustomerService, PaymentService |
| `LogInformation` | Method | Logs informational messages | All services and controllers |
| `LogWarning` | Method | Logs warning messages | Validation failures, business rule violations |
| `LogError` | Method | Logs error messages | Exception handling throughout application |
| `LogDebug` | Method | Logs debug information | Development troubleshooting |
| `Business Event:` | Pattern | Business event log prefix | Customer and Payment services |
| `Operation` | Scope Property | Current operation name | Service layer scoped logging |
| `Serilog` | Framework | Primary logging framework | Program.cs configuration |

This comprehensive logging implementation provides full observability into the DriftRide API operations, supporting debugging, monitoring, compliance, and business analytics requirements.