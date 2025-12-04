using LogiTrack.Domain.Models;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Data;

public class LogiTrackContext(DbContextOptions<LogiTrackContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<InventoryItem> InventoryItems { get; set; }

    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LogiTrackContext).Assembly);
    }
}
