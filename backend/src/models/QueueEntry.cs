using System.ComponentModel.DataAnnotations;

namespace DriftRide.Models
{
    /// <summary>
    /// Represents customer position in the ride queue
    /// </summary>
    public class QueueEntry
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
        /// Foreign key to Payment, required
        /// </summary>
        [Required]
        public Guid PaymentId { get; set; }

        /// <summary>
        /// Current position in queue (1 = next)
        /// </summary>
        [Required]
        public int Position { get; set; }

        /// <summary>
        /// Queue entry status
        /// </summary>
        [Required]
        public QueueEntryStatus Status { get; set; }

        /// <summary>
        /// When added to queue
        /// </summary>
        public DateTime QueuedAt { get; set; }

        /// <summary>
        /// When ride started
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When ride completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Driver username who completed ride, max 100 chars
        /// </summary>
        [MaxLength(100)]
        public string? CompletedBy { get; set; }

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
        /// One-to-one with Payment
        /// </summary>
        public virtual Payment Payment { get; set; } = null!;
    }
}