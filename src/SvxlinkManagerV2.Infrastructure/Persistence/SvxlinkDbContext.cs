using Microsoft.EntityFrameworkCore;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Sound;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

public class SvxlinkDbContext : DbContext
{
    public SvxlinkDbContext(DbContextOptions<SvxlinkDbContext> options) : base(options) { }

    public DbSet<SalonAggregate> Salons => Set<SalonAggregate>();
    public DbSet<SA818Aggregate> SA818 => Set<SA818Aggregate>();
    public DbSet<SoundAggregate> Sounds => Set<SoundAggregate>();
    public DbSet<ReflectorAggregate> Reflectors => Set<ReflectorAggregate>();
    public DbSet<GeneralConfigurationAggregate> GeneralConfigurations => Set<GeneralConfigurationAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ignore DomainEvents on all aggregate entities (not persisted in DB)
        modelBuilder.Entity<SalonAggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<SA818Aggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<SoundAggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<ReflectorAggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<GeneralConfigurationAggregate>().Ignore(e => e.DomainEvents);

        modelBuilder.Entity<SalonAggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name);
            entity.Property(e => e.IsDefault);
            entity.Property(e => e.IsTemporized);
            entity.Property(e => e.IsDeleted);
            entity.Property(e => e.DtmfCode);
            entity.Property(e => e.SoundId);
            entity.HasOne<SoundAggregate>()
                .WithOne()
                .HasForeignKey<SalonAggregate>(e => e.SoundId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            entity.OwnsOne(e => e.Configuration, cfg => cfg.ToJson());
        });

        modelBuilder.Entity<SA818Aggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Volume);
            entity.Property(e => e.Squelch);
            entity.Property(e => e.Bandwidth);
            entity.Property(e => e.PreEmph);
            entity.Property(e => e.HighPass);
            entity.Property(e => e.LowPass);
        });

        modelBuilder.Entity<SoundAggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name);
            entity.Property(e => e.FileContent);
            entity.Property(e => e.Duration);
            entity.Property(e => e.SampleRate);
            entity.Property(e => e.Channels);
            entity.Property(e => e.CreatedAt);
            entity.Property(e => e.UpdatedAt);
        });

        modelBuilder.Entity<ReflectorAggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name);
            entity.Property(e => e.Config);
            entity.Property(e => e.IsDeleted);
        });

        modelBuilder.Entity<GeneralConfigurationAggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StartReflectorOnStartup);
            entity.Property(e => e.StartDefaultSalonOnStartup);
        });
    }
}
