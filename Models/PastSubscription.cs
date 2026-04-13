using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    /// <summary>
    /// Immutable audit record written whenever a TenantSubscription is
    /// deactivated — whether by expiry, replacement, or cancellation.
    /// Fields are snapshotted so history survives plan renames or deletes.
    /// </summary>
    public class PastSubscription
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // ── Tenant ────────────────────────────────────────────────────
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        // ── Plan reference (FK kept for reporting; Name snapshotted) ──
        public Guid PlanId { get; set; }
        public Plan Plan { get; set; } = null!;

        /// <summary>Pointer back to the original TenantSubscription row.</summary>
        public Guid? OriginalSubscriptionId { get; set; }

        // ── Snapshots (preserved even if Plan/Tenant is later edited) ─
        [MaxLength(200)]
        public string PlanName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string TenantName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string BillingCycle { get; set; } = string.Empty;

        public int Price { get; set; }
        public bool IsTrial { get; set; }

        // ── Period ────────────────────────────────────────────────────
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // ── Archival metadata ─────────────────────────────────────────
        /// <summary>UTC timestamp when this record was written.</summary>
        public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// "Expired"  — end date passed, auto-detected by SubscriptionExpiryService.
        /// "Replaced" — a new subscription was created before this one ended.
        /// "Cancelled"— manually deactivated via admin UI.
        /// </summary>
        [Required, MaxLength(20)]
        public string Reason { get; set; } = "Expired";

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
