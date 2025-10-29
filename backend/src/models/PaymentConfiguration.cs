using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriftRide.Models
{
    /// <summary>
    /// Stores payment method configuration set by sales staff
    /// </summary>
    public class PaymentConfiguration
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Payment method type
        /// </summary>
        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        /// <summary>
        /// Friendly name shown to customers, max 100 chars
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Deep link URL for external apps, max 500 chars
        /// </summary>
        [MaxLength(500)]
        public string? PaymentUrl { get; set; }

        /// <summary>
        /// Whether this payment method is active
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Current price for this method, precision 18,2
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerRide { get; set; }

        /// <summary>
        /// Whether automatic API verification is enabled
        /// </summary>
        public bool ApiIntegrationEnabled { get; set; }

        /// <summary>
        /// Encrypted API credentials, max 1000 chars
        /// </summary>
        [MaxLength(1000)]
        public string? ApiCredentials { get; set; }

        /// <summary>
        /// Last configuration change
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Sales staff who updated, max 100 chars
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string UpdatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Concurrency token
        /// </summary>
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
    }
}