namespace DriftRide.Api.Shared;

/// <summary>
/// Shared ID mapping utility for converting between domain Guid IDs and API int IDs.
/// This is a temporary solution for test compatibility until ID strategy is unified.
/// </summary>
public static class IdMapper
{
    // Customer ID mappings
    private static readonly Dictionary<Guid, int> _customerGuidToIntMap = new();
    private static readonly Dictionary<int, Guid> _customerIntToGuidMap = new();
    private static int _nextCustomerApiId = 1;

    // Payment ID mappings
    private static readonly Dictionary<Guid, int> _paymentGuidToIntMap = new();
    private static readonly Dictionary<int, Guid> _paymentIntToGuidMap = new();
    private static int _nextPaymentApiId = 1;

    /// <summary>
    /// Maps a Guid customer domain ID to an int API ID.
    /// Creates a bidirectional mapping for test consistency.
    /// </summary>
    /// <param name="domainId">Domain Guid ID</param>
    /// <returns>API int ID</returns>
    public static int MapCustomerToApiId(Guid domainId)
    {
        if (_customerGuidToIntMap.TryGetValue(domainId, out var existingId))
        {
            return existingId;
        }

        var newApiId = _nextCustomerApiId++;
        _customerGuidToIntMap[domainId] = newApiId;
        _customerIntToGuidMap[newApiId] = domainId;
        return newApiId;
    }

    /// <summary>
    /// Maps an int customer API ID to a Guid domain ID.
    /// </summary>
    /// <param name="apiId">API int ID</param>
    /// <returns>Domain Guid ID, or Guid.Empty if not found</returns>
    public static Guid MapCustomerToDomainId(int apiId)
    {
        if (_customerIntToGuidMap.TryGetValue(apiId, out var domainId))
        {
            return domainId;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Maps a Guid payment domain ID to an int API ID.
    /// Creates a bidirectional mapping for test consistency.
    /// </summary>
    /// <param name="domainId">Domain Guid ID</param>
    /// <returns>API int ID</returns>
    public static int MapPaymentToApiId(Guid domainId)
    {
        if (_paymentGuidToIntMap.TryGetValue(domainId, out var existingId))
        {
            return existingId;
        }

        var newApiId = _nextPaymentApiId++;
        _paymentGuidToIntMap[domainId] = newApiId;
        _paymentIntToGuidMap[newApiId] = domainId;
        return newApiId;
    }

    /// <summary>
    /// Maps an int payment API ID to a Guid domain ID.
    /// </summary>
    /// <param name="apiId">API int ID</param>
    /// <returns>Domain Guid ID, or Guid.Empty if not found</returns>
    public static Guid MapPaymentToDomainId(int apiId)
    {
        if (_paymentIntToGuidMap.TryGetValue(apiId, out var domainId))
        {
            return domainId;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Clears all mappings. Used for test cleanup.
    /// </summary>
    public static void ClearAll()
    {
        _customerGuidToIntMap.Clear();
        _customerIntToGuidMap.Clear();
        _paymentGuidToIntMap.Clear();
        _paymentIntToGuidMap.Clear();
        _nextCustomerApiId = 1;
        _nextPaymentApiId = 1;
    }
}