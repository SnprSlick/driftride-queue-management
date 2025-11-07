const express = require('express');
const cors = require('cors');
const app = express();
const port = 3001;

// Middleware
app.use(cors());
app.use(express.json());

// Serve static files (HTML, CSS, JS)
app.use(express.static('.'));

// Debug middleware - remove for production
// app.use((req, res, next) => {
//     console.log(`${new Date().toISOString()} - ${req.method} ${req.path}`);
//     next();
// });

// Mock data
let customers = [];
let payments = [];
let queue = [];
let trackMode = 'hot'; // 'hot' for auto-progression, 'cold' for manual mode
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

// Event broadcasting helper - simulates real-time updates via localStorage
function broadcastEvent(type, data = {}) {
    const eventData = {
        type: type,
        timestamp: new Date().toISOString(),
        source: 'backend-api',
        data: data
    };

    console.log(`📡 Broadcasting event: ${type}`, eventData);

    // Note: In a real implementation, this would be handled by WebSockets/SignalR
    // For this demo, we rely on frontend polling and localStorage events
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
    // Return only waiting customers (exclude current rider)
    const waitingQueue = queue.filter(q => q.status === 'Waiting');
    res.json(createResponse(true, 'Queue retrieved successfully', waitingQueue));
});

app.get('/api/queue/current', (req, res) => {
    // Find customer currently riding (InProgress status)
    const currentCustomer = queue.find(q => q.status === 'InProgress');
    if (!currentCustomer) {
        return res.status(204).send();
    }
    res.json(createResponse(true, 'Current customer retrieved', currentCustomer));
});

app.post('/api/queue/:id/complete', (req, res) => {
    const { completedBy, reason, notes } = req.body;
    const queueEntry = queue.find(q => q.id === parseInt(req.params.id));
    if (!queueEntry) {
        return res.status(404).json(createResponse(false, 'Queue entry not found'));
    }

    queueEntry.status = 'Completed';
    queueEntry.completedAt = new Date().toISOString();
    queueEntry.completedBy = completedBy || 'driver@example.com';
    queueEntry.completionReason = reason || 'Ride completed normally';
    queueEntry.completionNotes = notes || null;

    // Auto-start next customer only if Hot Track mode is enabled
    if (trackMode === 'hot') {
        const nextCustomer = queue
            .filter(q => q.status === 'Waiting')
            .sort((a, b) => a.position - b.position)[0];

        if (nextCustomer) {
            nextCustomer.status = 'InProgress';
            nextCustomer.startedAt = new Date().toISOString();
            nextCustomer.startedBy = 'hot-track-auto-progression';
            console.log(`🔥 Hot Track Auto-progression: ${nextCustomer.customer.name} (Queue ID: ${nextCustomer.id})`);
        }
    } else {
        console.log(`❄️ Cold Track: No auto-progression after completion (manual mode)`);
    }

    // Recalculate positions for remaining waiting entries
    const waitingEntries = queue.filter(q => q.status === 'Waiting');
    waitingEntries.forEach((entry, index) => {
        entry.position = index + 1;
    });

    // Broadcast event for real-time updates
    broadcastEvent('ride_completed', {
        completedCustomer: queueEntry,
        completedBy: queueEntry.completedBy,
        trackMode: trackMode,
        nextCustomer: queue.find(q => q.status === 'InProgress'),
        waitingCount: waitingEntries.length
    });

    res.json(createResponse(true, 'Ride completed successfully', queueEntry));
});

app.post('/api/queue/:id/return-to-queue', (req, res) => {
    const { reason, notes } = req.body;
    const queueEntry = queue.find(q => q.id === parseInt(req.params.id));
    if (!queueEntry) {
        return res.status(404).json(createResponse(false, 'Queue entry not found'));
    }

    // Return to waiting status and move to end of queue
    queueEntry.status = 'Waiting';
    const waitingEntries = queue.filter(q => q.status === 'Waiting' && q.id !== queueEntry.id);
    queueEntry.position = waitingEntries.length + 1;
    queueEntry.returnedToQueueAt = new Date().toISOString();
    queueEntry.returnReason = reason || 'Customer returned to queue';
    queueEntry.returnNotes = notes || null;
    queueEntry.returnedBy = 'sales@example.com';

    // Recalculate positions for all waiting entries
    const allWaitingEntries = queue.filter(q => q.status === 'Waiting');
    allWaitingEntries.forEach((entry, index) => {
        entry.position = index + 1;
    });

    res.json(createResponse(true, 'Customer returned to queue successfully', queueEntry));
});

app.post('/api/queue/:id/complete-from-sales', (req, res) => {
    const { action, reason, notes } = req.body; // action: 'complete' or 'return'
    const queueEntry = queue.find(q => q.id === parseInt(req.params.id));
    if (!queueEntry) {
        return res.status(404).json(createResponse(false, 'Queue entry not found'));
    }

    if (action === 'complete') {
        queueEntry.status = 'Completed';
        queueEntry.completedAt = new Date().toISOString();
        queueEntry.completedBy = 'sales@example.com';
        queueEntry.completionReason = reason || 'Completed by sales staff';
        queueEntry.completionNotes = notes || null;

        // Recalculate positions for waiting entries
        const waitingEntries = queue.filter(q => q.status === 'Waiting');
        waitingEntries.forEach((entry, index) => {
            entry.position = index + 1;
        });

        // Broadcast event for real-time updates
        broadcastEvent('ride_completed_by_sales', {
            completedCustomer: queueEntry,
            completedBy: 'sales@example.com',
            waitingCount: waitingEntries.length,
            reason: reason
        });

        res.json(createResponse(true, 'Ride marked as completed by sales', queueEntry));
    } else if (action === 'return') {
        // Return to waiting status and move to end of queue
        queueEntry.status = 'Waiting';
        const waitingEntries = queue.filter(q => q.status === 'Waiting' && q.id !== queueEntry.id);
        queueEntry.position = waitingEntries.length + 1;
        queueEntry.returnedToQueueAt = new Date().toISOString();
        queueEntry.returnReason = reason || 'Returned to queue by sales';
        queueEntry.returnNotes = notes || null;
        queueEntry.returnedBy = 'sales@example.com';

        // Recalculate positions for all waiting entries
        const allWaitingEntries = queue.filter(q => q.status === 'Waiting');
        allWaitingEntries.forEach((entry, index) => {
            entry.position = index + 1;
        });

        res.json(createResponse(true, 'Customer returned to queue by sales', queueEntry));
    } else {
        res.status(400).json(createResponse(false, 'Invalid action. Must be "complete" or "return"'));
    }
});

app.post('/api/queue/:id/start-ride', (req, res) => {
    const { startedBy } = req.body;
    const queueEntry = queue.find(q => q.id === parseInt(req.params.id));
    if (!queueEntry) {
        return res.status(404).json(createResponse(false, 'Queue entry not found'));
    }

    // Check if there's already a current rider
    const currentRider = queue.find(q => q.status === 'InProgress');
    if (currentRider) {
        return res.status(409).json(createResponse(false, 'Another customer is currently riding', null, {
            code: 'CURRENT_RIDER_EXISTS',
            currentRider: currentRider
        }));
    }

    // Start the ride
    queueEntry.status = 'InProgress';
    queueEntry.rideStartedAt = new Date().toISOString();
    queueEntry.startedBy = startedBy || 'sales@example.com';

    // Recalculate positions for waiting entries
    const waitingEntries = queue.filter(q => q.status === 'Waiting');
    waitingEntries.forEach((entry, index) => {
        entry.position = index + 1;
    });

    res.json(createResponse(true, 'Ride started successfully', queueEntry));
});

app.get('/api/queue/completed', (req, res) => {
    // Return completed rides, newest first
    const completedRides = queue
        .filter(q => q.status === 'Completed')
        .sort((a, b) => new Date(b.completedAt) - new Date(a.completedAt));

    res.json(createResponse(true, 'Completed rides retrieved successfully', completedRides));
});

app.post('/api/queue/:id/restore', (req, res) => {
    const { restoredBy, reason, notes } = req.body;
    const queueEntry = queue.find(q => q.id === parseInt(req.params.id));

    if (!queueEntry) {
        return res.status(404).json(createResponse(false, 'Queue entry not found'));
    }

    if (queueEntry.status !== 'Completed') {
        return res.status(400).json(createResponse(false, 'Only completed rides can be restored'));
    }

    // Restore to waiting status and add to end of queue
    queueEntry.status = 'Waiting';
    const waitingEntries = queue.filter(q => q.status === 'Waiting' && q.id !== queueEntry.id);
    queueEntry.position = waitingEntries.length + 1;
    queueEntry.restoredAt = new Date().toISOString();
    queueEntry.restoredBy = restoredBy || 'sales@example.com';
    queueEntry.restoreReason = reason || 'Restored from completed rides';
    queueEntry.restoreNotes = notes || null;

    // Clear completion data
    delete queueEntry.completedAt;
    delete queueEntry.completedBy;
    delete queueEntry.completionReason;
    delete queueEntry.completionNotes;

    // Recalculate positions for all waiting entries
    const allWaitingEntries = queue.filter(q => q.status === 'Waiting');
    allWaitingEntries.forEach((entry, index) => {
        entry.position = index + 1;
    });

    res.json(createResponse(true, 'Ride restored to queue successfully', queueEntry));
});

app.post('/api/queue/reorder', (req, res) => {
    const { queueOrder } = req.body;

    // Update positions based on new order (only for waiting customers)
    queueOrder.forEach((queueId, index) => {
        const entry = queue.find(q => q.id === queueId && q.status === 'Waiting');
        if (entry) {
            entry.position = index + 1;
        }
    });

    const waitingQueue = queue.filter(q => q.status === 'Waiting');
    res.json(createResponse(true, 'Queue reordered successfully', waitingQueue));
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

// Track Mode Management endpoints
app.get('/api/queue/track-mode', (req, res) => {
    res.json(createResponse(true, 'Track mode retrieved', { mode: trackMode }));
});

app.put('/api/queue/track-mode', (req, res) => {
    const { mode } = req.body;

    if (!mode || !['hot', 'cold'].includes(mode)) {
        return res.status(400).json(createResponse(false, 'Invalid track mode. Must be "hot" or "cold"'));
    }

    const previousMode = trackMode;
    trackMode = mode;

    console.log(`🔄 Track mode changed from ${previousMode} to ${trackMode}`);

    // Handle mode-specific logic
    if (mode === 'cold') {
        // Cold Track: Move current customer back to queue position #1 if exists
        const currentCustomer = queue.find(q => q.status === 'InProgress');
        if (currentCustomer) {
            currentCustomer.status = 'Waiting';
            currentCustomer.position = 1;
            currentCustomer.pausedAt = new Date().toISOString();
            currentCustomer.pausedBy = 'track-mode-change';

            // Shift other waiting customers down
            const waitingEntries = queue.filter(q => q.status === 'Waiting' && q.id !== currentCustomer.id);
            waitingEntries.forEach((entry, index) => {
                entry.position = index + 2; // Start from position 2
            });

            console.log(`❄️ Cold Track: Moved ${currentCustomer.customer.name} to queue position #1`);
        }
    } else if (mode === 'hot') {
        // Hot Track: Auto-start the first customer in queue if no current customer
        const currentCustomer = queue.find(q => q.status === 'InProgress');
        if (!currentCustomer) {
            const nextCustomer = queue
                .filter(q => q.status === 'Waiting')
                .sort((a, b) => a.position - b.position)[0];

            if (nextCustomer) {
                nextCustomer.status = 'InProgress';
                nextCustomer.startedAt = new Date().toISOString();
                nextCustomer.startedBy = 'hot-track-auto-start';

                // Recalculate positions for remaining waiting entries
                const remainingWaiting = queue.filter(q => q.status === 'Waiting');
                remainingWaiting.forEach((entry, index) => {
                    entry.position = index + 1;
                });

                console.log(`🔥 Hot Track: Auto-started ${nextCustomer.customer.name} (Queue ID: ${nextCustomer.id})`);
            }
        }
    }

    res.json(createResponse(true, `Track mode set to ${mode}`, {
        mode: trackMode,
        previousMode,
        currentCustomer: queue.find(q => q.status === 'InProgress') || null
    }));
});

// Routes are configured above this point

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
    console.log('   GET  /api/queue/completed');
    console.log('   GET  /api/queue/track-mode');
    console.log('   PUT  /api/queue/track-mode');
    console.log('   POST /api/queue/:id/complete');
    console.log('   POST /api/queue/:id/return-to-queue');
    console.log('   POST /api/queue/:id/complete-from-sales');
    console.log('   POST /api/queue/:id/start-ride');
    console.log('   POST /api/queue/:id/restore');
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