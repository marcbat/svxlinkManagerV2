using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SvxlinkManagerV2.Domain.Aggregates.AudioConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration;
using SvxlinkManagerV2.Domain.Aggregates.Reflector;
using SvxlinkManagerV2.Domain.Aggregates.SA818;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

public class SvxlinkDbContext : IdentityDbContext<IdentityUser>
{
    public SvxlinkDbContext(DbContextOptions<SvxlinkDbContext> options) : base(options) { }

    public DbSet<SalonAggregate> Salons => Set<SalonAggregate>();
    public DbSet<SA818Aggregate> SA818 => Set<SA818Aggregate>();
    public DbSet<ReflectorAggregate> Reflectors => Set<ReflectorAggregate>();
    public DbSet<GeneralConfigurationAggregate> GeneralConfigurations => Set<GeneralConfigurationAggregate>();
    public DbSet<AudioConfigurationAggregate> AudioConfigurations => Set<AudioConfigurationAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SalonAggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<SA818Aggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<ReflectorAggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<GeneralConfigurationAggregate>().Ignore(e => e.DomainEvents);
        modelBuilder.Entity<AudioConfigurationAggregate>().Ignore(e => e.DomainEvents);

        modelBuilder.Entity<SalonAggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name);
            entity.Property(e => e.IsDefault);
            entity.Property(e => e.IsDeleted);
            entity.Property(e => e.DtmfCode);
            entity.Property(e => e.SalonType);
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
            entity.Property(e => e.DefaultRxFrequency);
            entity.Property(e => e.DefaultTxFrequency);
        });

        modelBuilder.Entity<AudioConfigurationAggregate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CaptureControl);
            entity.Property(e => e.CaptureLevel);
            entity.Property(e => e.PlaybackControl);
            entity.Property(e => e.PlaybackLevel);
        });
    }
}
