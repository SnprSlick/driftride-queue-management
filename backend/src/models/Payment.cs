using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriftRide.Models
{
    /// <summary>
    /// Records customer payment attempts and verification status
    /// </summary>
    public class Payment
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Foreign key to Customer, required
        /// </summary>
        [Required]
        public Guid CustomerId { get; set; }

        /// <summary>
        /// Payment amount in USD, precision 18,2
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Payment method used
        /// </summary>
        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        /// <summary>
        /// Payment verification status
        /// </summary>
        [Required]
        public PaymentStatus Status { get; set; }

        /// <summary>
        /// Reference from payment provider, max 255 chars
        /// </summary>
        [MaxLength(255)]
        public string? ExternalTransactionId { get; set; }

        /// <summary>
        /// When payment was initiated
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When sales staff confirmed/denied
        /// </summary>
        public DateTime? ConfirmedAt { get; set; }

        /// <summary>
        /// Sales staff username who confirmed, max 100 chars
        /// </summary>
        [MaxLength(100)]
        public string? ConfirmedBy { get; set; }

        /// <summary>
        /// Optional notes from sales staff, max 500 chars
        /// </summary>
        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>
        /// Concurrency token
        /// </summary>
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;

        // Navigation properties
        /// <summary>
        /// Many-to-one with Customer
        /// </summary>
        public virtual Customer Customer { get; set; } = null!;

        /// <summary>
        /// One-to-one with QueueEntry (when payment confirmed)
        /// </summary>
        public virtual QueueEntry? QueueEntry { get; set; }
    }
}