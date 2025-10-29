using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using DriftRide.Models;

namespace DriftRide.Hubs;

/// <summary>
/// SignalR hub for real-time queue management and notifications.
/// Handles communication between server and connected clients for payment workflows and queue updates.
/// </summary>
[Authorize]
public class QueueHub : Hub
{
    /// <summary>
    /// Called when a client connects to the hub.
    /// Adds the connection to appropriate groups based on user role.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirst("id")?.Value;
            var userRole = user.FindFirst("role")?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                // Add to user-specific group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            }

            if (!string.IsNullOrEmpty(userRole))
            {
                // Add to role-specific group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Role_{userRole}");
            }
        }
        else
        {
            // For unauthenticated users (customers), add to general customers group
            await Groups.AddToGroupAsync(Context.ConnectionId, "Customers");
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Removes the connection from all groups.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirst("id")?.Value;
            var userRole = user.FindFirst("role")?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
            }

            if (!string.IsNullOrEmpty(userRole))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Role_{userRole}");
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Allows clients to join additional groups for targeted notifications.
    /// </summary>
    /// <param name="groupName">Name of the group to join</param>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Allows clients to leave groups.
    /// </summary>
    /// <param name="groupName">Name of the group to leave</param>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Allows customer to join payment-specific group for targeted notifications.
    /// </summary>
    /// <param name="paymentId">Payment ID to receive updates for</param>
    public async Task JoinPaymentGroup(string paymentId)
    {
        if (!string.IsNullOrEmpty(paymentId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Payment_{paymentId}");
        }
    }

    /// <summary>
    /// Allows customer to leave payment-specific group.
    /// </summary>
    /// <param name="paymentId">Payment ID to stop receiving updates for</param>
    public async Task LeavePaymentGroup(string paymentId)
    {
        if (!string.IsNullOrEmpty(paymentId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Payment_{paymentId}");
        }
    }

    /// <summary>
    /// Allows customer to join customer-specific group for targeted notifications.
    /// </summary>
    /// <param name="customerId">Customer ID to receive updates for</param>
    public async Task JoinCustomerGroup(string customerId)
    {
        if (!string.IsNullOrEmpty(customerId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Customer_{customerId}");
        }
    }

    /// <summary>
    /// Heartbeat method for connection monitoring.
    /// </summary>
    public async Task Heartbeat()
    {
        await Clients.Caller.SendAsync("HeartbeatResponse", DateTime.UtcNow);
    }

    /// <summary>
    /// Allows clients to request current connection status.
    /// </summary>
    public async Task RequestConnectionStatus()
    {
        var user = Context.User;
        var connectionInfo = new
        {
            ConnectionId = Context.ConnectionId,
            IsAuthenticated = user?.Identity?.IsAuthenticated ?? false,
            UserRole = user?.FindFirst("role")?.Value,
            ConnectedAt = DateTime.UtcNow
        };

        await Clients.Caller.SendAsync("ConnectionStatusResponse", connectionInfo);
    }
}