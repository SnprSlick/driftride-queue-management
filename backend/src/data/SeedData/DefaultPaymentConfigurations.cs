using DriftRide.Models;

namespace DriftRide.Data.SeedData
{
    /// <summary>
    /// Provides default payment configuration data for application seeding
    /// </summary>
    public static class DefaultPaymentConfigurations
    {
        /// <summary>
        /// Gets default payment configurations for all supported payment methods
        /// </summary>
        /// <returns>Array of PaymentConfiguration entities with default settings</returns>
        public static PaymentConfiguration[] GetDefaultConfigurations()
        {
            return new[]
            {
                new PaymentConfiguration
                {
                    Id = Guid.NewGuid(),
                    PaymentMethod = PaymentMethod.CashApp,
                    DisplayName = "CashApp",
                    PaymentUrl = "https://cash.app/$DriftRide",
                    IsEnabled = true,
                    PricePerRide = 20.00m,
                    ApiIntegrationEnabled = false,
                    ApiCredentials = null,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "System"
                },
                new PaymentConfiguration
                {
                    Id = Guid.NewGuid(),
                    PaymentMethod = PaymentMethod.PayPal,
                    DisplayName = "PayPal",
                    PaymentUrl = "https://paypal.me/DriftRide/20",
                    IsEnabled = true,
                    PricePerRide = 20.00m,
                    ApiIntegrationEnabled = false,
                    ApiCredentials = null,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "System"
                },
                new PaymentConfiguration
                {
                    Id = Guid.NewGuid(),
                    PaymentMethod = PaymentMethod.CashInHand,
                    DisplayName = "Cash in Hand",
                    PaymentUrl = null,
                    IsEnabled = true,
                    PricePerRide = 20.00m,
                    ApiIntegrationEnabled = false,
                    ApiCredentials = null,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "System"
                }
            };
        }
    }
}