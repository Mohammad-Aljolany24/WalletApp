using Microsoft.EntityFrameworkCore;
using WalletApp.Data.Entities;

namespace WalletApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<EventRecord> Events => Set<EventRecord>();
    public DbSet<WalletReadRecord> WalletReadModel => Set<WalletReadRecord>();

     public DbSet<UserRecord> Users => Set<UserRecord>(); 

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventRecord>(e =>
        {
            e.ToTable("Events");
            e.HasKey(x => x.Id);
             e.HasIndex(x => new { x.AggregateId, x.Version }).IsUnique();
            e.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Data).IsRequired();
        });

        modelBuilder.Entity<WalletReadRecord>(e =>
        {
            e.ToTable("WalletReadModel");
            e.HasKey(x => x.WalletId);
            e.Property(x => x.Balance).HasColumnType("decimal(18,2)");
        });

          modelBuilder.Entity<UserRecord>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
        });
    }
}