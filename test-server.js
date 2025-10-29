const express = require('express');
const cors = require('cors');
const app = express();
const port = 3002;

app.use(cors());
app.use(express.json());

app.get('/test', (req, res) => {
    res.json({ message: 'Test endpoint working' });
});

app.post('/api/customers', (req, res) => {
    res.json({ success: true, message: 'Customer endpoint working', data: req.body });
});

// Catch-all for undefined routes
app.use((req, res) => {
    res.status(404).json({
        error: "Endpoint not found",
        path: req.path,
        timestamp: new Date().toISOString()
    });
});

app.listen(port, () => {
    console.log(`Test server running at http://localhost:${port}`);
});