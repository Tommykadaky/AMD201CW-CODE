using Microsoft.EntityFrameworkCore;
using Sensor.Api.Models;

namespace Sensor.Api.Data;

public class SensorDbContext(DbContextOptions<SensorDbContext> options) : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<SensorReading> Readings => Set<SensorReading>();
    public DbSet<PumpRun> PumpRuns => Set<PumpRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("Devices");
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasMaxLength(64);
            e.Property(d => d.KeyHash).HasMaxLength(64);
            e.Property(d => d.Name).HasMaxLength(64);
            e.Property(d => d.IpAddress).HasMaxLength(45);
            e.Property(d => d.Firmware).HasMaxLength(30);
            e.HasIndex(d => d.OwnerUserId);               // "list my devices"
        });

        modelBuilder.Entity<SensorReading>(e =>
        {
            e.ToTable("Readings");
            e.Property(r => r.DeviceId).HasMaxLength(64);
            // Every history query is "one device, a time range": this index covers it
            e.HasIndex(r => new { r.DeviceId, r.RecordedAtUtc });
        });

        modelBuilder.Entity<PumpRun>(e =>
        {
            e.ToTable("PumpRuns");
            e.Property(p => p.DeviceId).HasMaxLength(64);
            e.Property(p => p.Trigger).HasMaxLength(10);
            e.HasIndex(p => new { p.DeviceId, p.StartedAtUtc });
        });
    }
}
