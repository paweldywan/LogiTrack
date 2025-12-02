using LogiTrack.Data.Models;

using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Data;

public class LogiTrackContext(DbContextOptions<LogiTrackContext> options) : DbContext(options)
{
    public DbSet<InventoryItem> InventoryItems { get; set; }

    public DbSet<Order> Orders { get; set; }
}
