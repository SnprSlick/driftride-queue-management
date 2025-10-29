const express = require('express');
const cors = require('cors');
const app = express();
const port = 3001;

// Middleware
app.use(cors());
app.use(express.json());

// Mock data
let customers = [];
let payments = [];
let queue = [];
let paymentConfigurations = {
    CashApp: {
        displayName: 'CashApp Payment',
        paymentUrl: 'https://cash.app/$driftride',
        isEnabled: true,
        pricePerRide: 25.00,
        apiIntegrationEnabled: true
    },
    PayPal: {
        displayName: 'PayPal Payment',
        paymentUrl: 'https://paypal.me/driftride',
        isEnabled: true,
        pricePerRide: 27.50,
        apiIntegrationEnabled: false
    },
    CashInHand: {
        displayName: 'Cash Payment',
        paymentUrl: '',
        isEnabled: true,
        pricePerRide: 20.00,
        apiIntegrationEnabled: false
    }
};

let nextCustomerId = 1;
let nextPaymentId = 1;
let nextQueueId = 1;

// Helper functions
function createResponse(success, message, data = null, error = null) {
    return {
        success,
        message,
        data,
        error,
        timestamp: new Date().toISOString()
    };
}

// Customer endpoints
app.post('/api/customers', (req, res) => {
    const { name, phoneNumber } = req.body;

    if (!name || !phoneNumber) {
        return res.status(400).json(createResponse(false, 'Name and phone number are required', null, {
            code: 'VALIDATION_FAILED',
            message: 'Name and phone number are required'
        }));
    }

    const customer = {
        id: nextCustomerId++,
        name,
        phoneNumber,
        createdAt: new Date().toISOString()
    };

    customers.push(customer);
    res.json(createResponse(true, 'Customer created successfully', customer));
});

app.get('/api/customers/:id', (req, res) => {
    const customer = customers.find(c => c.id === parseInt(req.params.id));
    if (!customer) {
        return res.status(404).json(createResponse(false, 'Customer not found', null, {
            code: 'NOT_FOUND',
            message: 'Customer not found'
        }));
    }
    res.json(createResponse(true, 'Customer retrieved successfully', customer));
});

// Payment endpoints
app.post('/api/payments', (req, res) => {
    const { customerId, amount, paymentMethod, externalTransactionId } = req.body;

    const payment = {
        id: nextPaymentId++,
        customerId,
        amount,
        paymentMethod,
        externalTransactionId,
        status: 'Pending',
        createdAt: new Date().toISOString(),
        notes: null,
        confirmedBy: null
    };

    payments.push(payment);

    // Add customer info for response
    const customer = customers.find(c => c.id === customerId);
    const paymentWithCustomer = { ...payment, customer };

    res.json(createResponse(true, 'Payment created successfully', paymentWithCustomer));
});

app.post('/api/payments/:id/confirm', (req, res) => {
    const { confirmed, notes } = req.body;
    const payment = payments.find(p => p.id === parseInt(req.params.id));

    if (!payment) {
        return res.status(404).json(createResponse(false, 'Payment not found'));
    }

    payment.status = confirmed ? 'Confirmed' : 'Denied';
    payment.notes = notes;
    payment.confirmedBy = 'sales@example.com'; // Mock staff
    payment.confirmedAt = new Date().toISOString();

    // If confirmed, add to queue
    if (confirmed) {
        const customer = customers.find(c => c.id === payment.customerId);
        const queueEntry = {
            id: nextQueueId++,
            customerId: payment.customerId,
            paymentId: payment.id,
            position: queue.length + 1,
            status: 'Waiting',
            queuedAt: new Date().toISOString(),
            customer,
            payment
        };
        queue.push(queueEntry);
    }

    res.json(createResponse(true, 'Payment confirmed successfully', payment));
});

app.get('/api/payments/pending', (req, res) => {
    const pendingPayments = payments
        .filter(p => p.status === 'Pending')
        .map(p => {
            const customer = customers.find(c => c.id === p.customerId);
            return { ...p, customer };
        });

    res.json(createResponse(true, 'Pending payments retrieved', pendingPayments));
});

// Queue endpoints
app.get('/api/queue', (req, res) => {
    res.json(createResponse(true, 'Queue retrieved successfully', queue));
});

app.get('/api/queue/current', (req, res) => {
    const currentCustomer = queue.find(q => q.status === 'Waiting');
    if (!currentCustomer) {
        return res.status(204).send();
    }
    res.json(createResponse(true, 'Current customer retrieved', currentCustomer));
});

app.post('/api/queue/:id/complete', (req, res) => {
    const queueEntry = queue.find(q => q.id === parseInt(req.params.id));
    if (!queueEntry) {
        return res.status(404).json(createResponse(false, 'Queue entry not found'));
    }

    queueEntry.status = 'Completed';
    queueEntry.completedAt = new Date().toISOString();
    queueEntry.completedBy = 'driver@example.com'; // Mock driver

    // Recalculate positions
    const waitingEntries = queue.filter(q => q.status === 'Waiting');
    waitingEntries.forEach((entry, index) => {
        entry.position = index + 1;
    });

    res.json(createResponse(true, 'Ride completed successfully', queueEntry));
});

app.post('/api/queue/reorder', (req, res) => {
    const { queueOrder } = req.body;

    // Update positions based on new order
    queueOrder.forEach((queueId, index) => {
        const entry = queue.find(q => q.id === queueId);
        if (entry) {
            entry.position = index + 1;
        }
    });

    res.json(createResponse(true, 'Queue reordered successfully', queue));
});

// Configuration endpoints
app.get('/api/configuration/payment-methods', (req, res) => {
    res.json(createResponse(true, 'Payment configurations retrieved', Object.values(paymentConfigurations)));
});

app.get('/api/configuration/payment-methods/enabled', (req, res) => {
    const enabled = Object.values(paymentConfigurations).filter(config => config.isEnabled);
    res.json(createResponse(true, 'Enabled payment methods retrieved', enabled));
});

app.put('/api/configuration/payment-methods', (req, res) => {
    const { paymentMethod, displayName, paymentUrl, isEnabled, pricePerRide, apiIntegrationEnabled } = req.body;

    if (paymentConfigurations[paymentMethod]) {
        paymentConfigurations[paymentMethod] = {
            ...paymentConfigurations[paymentMethod],
            displayName,
            paymentUrl,
            isEnabled,
            pricePerRide,
            apiIntegrationEnabled
        };

        res.json(createResponse(true, 'Payment configuration updated', paymentConfigurations[paymentMethod]));
    } else {
        res.status(404).json(createResponse(false, 'Payment method not found'));
    }
});

// Auth endpoint (mock)
app.post('/api/auth/login', (req, res) => {
    const { username, password } = req.body;

    // Mock authentication
    if (username === 'sales@example.com' && password === 'password') {
        res.json(createResponse(true, 'Login successful', {
            accessToken: 'mock-jwt-token',
            refreshToken: 'mock-refresh-token',
            expiresIn: 3600,
            user: {
                id: 1,
                username: 'sales@example.com',
                displayName: 'Sales User',
                role: 'Sales'
            }
        }));
    } else if (username === 'driver@example.com' && password === 'password') {
        res.json(createResponse(true, 'Login successful', {
            accessToken: 'mock-jwt-token',
            refreshToken: 'mock-refresh-token',
            expiresIn: 3600,
            user: {
                id: 2,
                username: 'driver@example.com',
                displayName: 'Driver User',
                role: 'Driver'
            }
        }));
    } else {
        res.status(401).json(createResponse(false, 'Invalid credentials', null, {
            code: 'UNAUTHORIZED',
            message: 'Invalid username or password'
        }));
    }
});

// Statistics endpoint
app.get('/api/stats', (req, res) => {
    const stats = {
        totalCustomers: customers.length,
        pendingPayments: payments.filter(p => p.status === 'Pending').length,
        confirmedPayments: payments.filter(p => p.status === 'Confirmed').length,
        queueLength: queue.filter(q => q.status === 'Waiting').length,
        completedRides: queue.filter(q => q.status === 'Completed').length,
        totalRevenue: payments
            .filter(p => p.status === 'Confirmed')
            .reduce((sum, p) => sum + p.amount, 0)
    };

    res.json(createResponse(true, 'Statistics retrieved', stats));
});

// Seed some initial data
function seedData() {
    // Add some test customers
    const testCustomers = [
        { name: 'John Smith', phoneNumber: '555-0101' },
        { name: 'Jane Doe', phoneNumber: '555-0102' },
        { name: 'Mike Johnson', phoneNumber: '555-0103' },
        { name: 'John Smith', phoneNumber: '555-0104' } // Duplicate name
    ];

    testCustomers.forEach(({ name, phoneNumber }) => {
        const customer = {
            id: nextCustomerId++,
            name,
            phoneNumber,
            createdAt: new Date().toISOString()
        };
        customers.push(customer);

        // Add some payments
        const payment = {
            id: nextPaymentId++,
            customerId: customer.id,
            amount: paymentConfigurations.CashApp.pricePerRide,
            paymentMethod: 'CashApp',
            externalTransactionId: `tx_${Math.random().toString(36).substr(2, 9)}`,
            status: Math.random() > 0.5 ? 'Confirmed' : 'Pending',
            createdAt: new Date().toISOString(),
            notes: null,
            confirmedBy: null
        };
        payments.push(payment);

        // Add confirmed payments to queue
        if (payment.status === 'Confirmed') {
            const queueEntry = {
                id: nextQueueId++,
                customerId: customer.id,
                paymentId: payment.id,
                position: queue.length + 1,
                status: 'Waiting',
                queuedAt: new Date().toISOString(),
                customer,
                payment
            };
            queue.push(queueEntry);
        }
    });

    console.log('Seeded data:');
    console.log(`- ${customers.length} customers`);
    console.log(`- ${payments.length} payments`);
    console.log(`- ${queue.length} queue entries`);
}

// Start server
app.listen(port, () => {
    console.log(`🚀 DriftRide Mock API Server running at http://localhost:${port}`);
    console.log('📋 Available endpoints:');
    console.log('   POST /api/customers');
    console.log('   GET  /api/customers/:id');
    console.log('   POST /api/payments');
    console.log('   POST /api/payments/:id/confirm');
    console.log('   GET  /api/payments/pending');
    console.log('   GET  /api/queue');
    console.log('   GET  /api/queue/current');
    console.log('   POST /api/queue/:id/complete');
    console.log('   POST /api/queue/reorder');
    console.log('   GET  /api/configuration/payment-methods');
    console.log('   PUT  /api/configuration/payment-methods');
    console.log('   POST /api/auth/login');
    console.log('   GET  /api/stats');
    console.log('');
    console.log('🎮 Demo credentials:');
    console.log('   Sales: sales@example.com / password');
    console.log('   Driver: driver@example.com / password');

    seedData();
});