using System.ComponentModel.DataAnnotations;

namespace DriftRide.Models
{
    /// <summary>
    /// Represents a person wanting a drift car ride
    /// </summary>
    public class Customer
    {
        /// <summary>
        /// Primary key, unique identifier
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Customer's display name, required, max 100 chars
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Customer's email address, required, max 255 chars
        /// </summary>
        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Optional contact information, max 20 chars
        /// </summary>
        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Whether the customer account is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Timestamp when customer record created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Concurrency token for updates
        /// </summary>
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;

        // Navigation properties
        /// <summary>
        /// Customer can have multiple payment attempts
        /// </summary>
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

        /// <summary>
        /// Customer can be in queue multiple times historically
        /// </summary>
        public virtual ICollection<QueueEntry> QueueEntries { get; set; } = new List<QueueEntry>();
    }
}