using Microsoft.EntityFrameworkCore;
using ECFD.Domain.Entities;

namespace ECFD.Infrastructure.Persistence;

public class EcfdDbContext : DbContext
{
    public EcfdDbContext(DbContextOptions<EcfdDbContext> options) : base(options)
    {
    }

    public DbSet<CallSession> CallSessions => Set<CallSession>();
    public DbSet<CallParticipant> CallParticipants => Set<CallParticipant>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();
    public DbSet<Evidence> EvidenceItems => Set<Evidence>();
    public DbSet<AttackEvent> AttackEvents => Set<AttackEvent>();
    public DbSet<RiskSnapshot> RiskSnapshots => Set<RiskSnapshot>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CallSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExternalCallId).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.CurrentStage).HasConversion<string>();
        });

        modelBuilder.Entity<Evidence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasConversion<string>();
        });

        modelBuilder.Entity<RiskSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Severity).HasConversion<string>();
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Severity).HasConversion<string>();
        });
    }
}
