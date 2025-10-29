# DriftRide Queue Management System

🚗 **A cloud-based drift car queue management system with real-time updates and multi-role architecture.**

## 🌟 Features

- **Customer Portal**: Registration, payment submission, and real-time queue tracking
- **Sales Dashboard**: Payment verification, queue management, and customer prioritization
- **Driver Interface**: Current customer display, queue preview, and ride completion
- **Real-Time Updates**: SignalR integration for live synchronization across all interfaces
- **Queue Override**: Drag-and-drop queue reordering for sales staff
- **Payment Integration**: Multiple payment methods with manual/API verification

## 🚀 Quick Start

### Prerequisites
- Node.js (for mock API server)
- .NET 8.0 (for production backend)
- Modern web browser

### Running the Demo

1. **Start the Mock API Server**
   ```bash
   npm install express cors
   node simple-backend.js
   ```
   Server will run at `http://localhost:3001`

2. **Open the Dashboards**
   ```bash
   # Customer Interface
   open customer-demo.html

   # Sales Interface
   open sales-queue-demo.html

   # Driver Interface
   open driver-api-demo.html
   ```

3. **Demo Credentials**
   - Sales: `sales@example.com` / `password`
   - Driver: `driver@example.com` / `password`

## 📱 User Interfaces

### Customer Dashboard
- **File**: `customer-demo.html`
- **Features**: Registration, payment submission, queue position tracking
- **Payment Methods**: CashApp, PayPal, Cash-in-hand

### Sales Dashboard
- **File**: `sales-queue-demo.html`
- **Features**: Payment verification, queue management, customer prioritization
- **Shortcuts**: A (approve), D (deny), R (remove), F5 (refresh)

### Driver Dashboard
- **File**: `driver-api-demo.html`
- **Features**: Current customer display, ride completion, queue preview
- **Shortcuts**: SPACE/ENTER (complete ride), F5 (refresh)

## 🛠 Technology Stack

### Frontend
- **HTML5/CSS3/JavaScript**: Responsive web interfaces
- **Drag & Drop API**: Queue reordering functionality
- **Fetch API**: RESTful API communication
- **Real-time**: Live updates across all interfaces

### Backend
- **Mock API**: Node.js/Express (development)
- **Production**: ASP.NET Core Web API (.NET 8.0)
- **Database**: Entity Framework Core with SQL Server
- **Authentication**: JWT with role-based authorization
- **Real-time**: SignalR for live updates

### Architecture
- **Multi-Role**: Customer, Sales, Driver workflows
- **Cloud-based**: Centralized queue management
- **Real-time Sync**: 5-second update requirement
- **Mobile-Responsive**: Touch-friendly interfaces

## 📋 API Endpoints

### Customer Management
- `POST /api/customers` - Create customer record
- `GET /api/customers/:id` - Retrieve customer details

### Payment Processing
- `POST /api/payments` - Submit payment
- `POST /api/payments/:id/confirm` - Verify payment (Sales)
- `GET /api/payments/pending` - Get pending payments

### Queue Operations
- `GET /api/queue` - Current queue status
- `GET /api/queue/current` - Next customer for driver
- `POST /api/queue/:id/complete` - Complete ride
- `POST /api/queue/reorder` - Reorder queue (Sales)

### Configuration
- `GET /api/configuration/payment-methods` - Payment options
- `PUT /api/configuration/payment-methods` - Update payment config

## 🎯 User Stories

### ✅ User Story 1: Customer Payment
Customers can register and submit payments with real-time status tracking.

### ✅ User Story 2: Driver Queue Management
Drivers can view current customers and complete rides efficiently.

### ✅ User Story 3: Sales Payment Configuration
Sales staff can verify payments and manage customer flow.

### ✅ User Story 4: Sales Queue Override
Sales staff can reorder queue positions via drag-and-drop interface.

## 📁 Project Structure

```
DriftRide-standalone/
├── README.md                 # This file
├── simple-backend.js         # Mock API server
├── customer-demo.html        # Customer interface
├── sales-queue-demo.html     # Sales interface
├── driver-api-demo.html      # Driver interface
├── backend/                  # .NET backend (development)
│   └── DriftRide.Api/        # ASP.NET Core API
├── CLAUDE.md                 # Development context
└── *.md                      # Documentation files
```

## 🧪 Testing

### End-to-End Workflow
1. **Customer**: Register and submit payment
2. **Sales**: Verify and approve payment
3. **Queue**: Customer automatically added
4. **Sales**: Reorder queue if needed
5. **Driver**: Complete ride
6. **Queue**: Positions automatically recalculate

### API Testing
```bash
# Test customer creation
curl -X POST http://localhost:3001/api/customers \
  -H "Content-Type: application/json" \
  -d '{"name": "Test Customer", "phoneNumber": "555-1234"}'

# Test queue reordering
curl -X POST http://localhost:3001/api/queue/reorder \
  -H "Content-Type: application/json" \
  -d '{"queueOrder": [3, 1, 2]}'
```

## 📊 Performance Requirements

- **Sales Confirmation**: Target <30 seconds per payment
- **Driver Completion**: Target <15 seconds per ride
- **Real-Time Sync**: 5-second update intervals
- **Mobile Responsive**: Touch-friendly on tablets

## 🔧 Development

### Building the .NET Backend
```bash
cd backend/DriftRide.Api
dotnet restore
dotnet build
dotnet run
```

### Code Quality
```bash
# Run linting
./scripts/lint.sh

# Run tests
dotnet test
```

## 📝 License

This project is part of the DriftRide Queue Management System development.

## 🤝 Contributing

This is a demonstration project showcasing a complete queue management system with real-time capabilities.

---

**🚗 DriftRide - Streamlining the drift car experience with modern queue management technology.**