# DriftRide User Story 1 Testing Guide

## 🎯 User Story 1: Customer Payment and Queue Entry

**Goal**: Test the complete customer payment workflow from registration to queue entry.

## Testing Approaches

### Option 1: API Testing with Postman/curl (Recommended)

Since the full application has build issues, test the core logic via API endpoints:

#### 1. Database Setup
```bash
# Start SQL Server container for testing
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=DriftRide123!" \
  -p 1433:1433 --name driftride-sql \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

#### 2. Test the Service Layer Directly

Create a simple test console app:

```csharp
// TestConsole/Program.cs
using DriftRide.Models;
using DriftRide.Services;

var customer = new Customer
{
    Name = "Test Customer",
    PhoneNumber = "555-1234",
    Email = "test@example.com"
};

// Test customer creation
Console.WriteLine($"Testing customer: {customer.Name}");

var payment = new Payment
{
    CustomerId = customer.Id,
    Amount = 20.00m,
    PaymentMethod = PaymentMethod.CashApp,
    ExternalTransactionId = "TEST123",
    Status = PaymentStatus.Pending
};

Console.WriteLine($"Testing payment: ${payment.Amount} via {payment.PaymentMethod}");
```

### Option 2: Frontend Testing (Visual Validation)

Test the user interfaces directly:

#### Customer Interface
1. Open `frontend/DriftRide.Web/Views/Customer/Index.cshtml` in browser
2. Test the 4-step workflow:
   - Step 1: Enter customer info
   - Step 2: Select payment method
   - Step 3: Initiate payment
   - Step 4: View queue status

#### Sales Interface
1. Open `frontend/DriftRide.Web/Views/Sales/Dashboard.cshtml` in browser
2. Test sales workflow:
   - View pending payments
   - One-click approval/denial
   - Manual customer addition
   - Queue management

### Option 3: Manual Testing Scenarios

#### Scenario 1: Happy Path Customer Flow
```
1. Customer arrives at drift event
2. Opens mobile web app
3. Enters name: "John Doe", phone: "555-1234"
4. Selects payment method: "CashApp"
5. Enters payment amount: $20
6. Initiates payment via external app
7. Returns to web app
8. Views queue position and estimated wait
```

#### Scenario 2: Sales Staff Payment Confirmation
```
1. Sales staff opens dashboard
2. Sees new payment notification
3. Verifies payment received externally
4. Clicks "Confirm Payment"
5. Customer automatically added to queue
6. Real-time notification sent to drivers
```

#### Scenario 3: Payment Denial Flow
```
1. Sales staff reviews payment
2. Payment not received or invalid
3. Clicks "Deny Payment"
4. Customer notified of denial
5. Sales staff can manually add customer if needed
```

### Option 4: Database Testing

Verify data persistence and relationships:

```sql
-- Check customer data
SELECT * FROM Customers ORDER BY CreatedAt DESC;

-- Check payment workflow
SELECT c.Name, p.Amount, p.Status, p.PaymentMethod, p.CreatedAt
FROM Customers c
JOIN Payments p ON c.Id = p.CustomerId
ORDER BY p.CreatedAt DESC;

-- Check queue entries
SELECT c.Name, qe.Position, qe.Status, qe.QueuedAt
FROM Customers c
JOIN QueueEntries qe ON c.Id = qe.CustomerId
ORDER BY qe.Position;
```

## 🎯 Success Criteria Testing

### Performance Tests
- ⏱️ **Customer Flow**: Complete workflow in under 3 minutes
- ⏱️ **Sales Confirmation**: Payment approval in under 30 seconds
- 📱 **Mobile Responsive**: Test on phone/tablet screen sizes
- 🔔 **Real-time Updates**: Notifications appear within 5 seconds

### Business Logic Tests
- ✅ **Payment Validation**: Only one pending payment per customer
- ✅ **Queue Ordering**: Customers added in payment confirmation order
- ✅ **Role Security**: Sales staff can confirm, customers cannot
- ✅ **Error Handling**: Graceful failure for network issues

### User Experience Tests
- 📱 **Mobile Interface**: Touch-friendly buttons and forms
- 🔔 **Notifications**: Clear status updates and alerts
- ⌨️ **Keyboard Shortcuts**: Sales staff efficiency features
- ♿ **Accessibility**: Screen reader and keyboard navigation

## 🐛 Common Issues & Solutions

### Frontend Testing
- **CORS Issues**: Check browser console for blocked requests
- **SignalR Connection**: Verify WebSocket connection in dev tools
- **Form Validation**: Test with invalid data to see error handling

### Backend Testing
- **Database Connection**: Verify SQL Server is running
- **JWT Authentication**: Check token generation and validation
- **API Endpoints**: Use Swagger UI at `/swagger` for testing

### Integration Testing
- **Real-time Updates**: Open multiple browser tabs to test notifications
- **Payment Flow**: Test complete workflow with actual payment apps
- **Error Scenarios**: Test network failures and timeout handling

## 📊 Test Results Template

Track your testing results:

```
✅ Customer Registration: PASS/FAIL
✅ Payment Method Selection: PASS/FAIL
✅ Payment Initiation: PASS/FAIL
✅ Sales Confirmation: PASS/FAIL
✅ Queue Entry: PASS/FAIL
✅ Real-time Updates: PASS/FAIL
✅ Mobile Responsive: PASS/FAIL
✅ Error Handling: PASS/FAIL

Overall User Story 1: PASS/FAIL
```

## 🚀 Next Steps

After testing User Story 1:
1. **Document Issues**: Record any bugs or usability problems
2. **Performance Metrics**: Measure actual timing vs targets
3. **User Feedback**: Get real user testing feedback
4. **Iterate**: Fix issues and improve based on testing results

The implementation provides a solid foundation for testing the complete customer payment workflow!