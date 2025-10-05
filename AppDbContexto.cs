using Microsoft.EntityFrameworkCore;
using Tienda_de_Snack.Models;

namespace Tienda_de_Snack.Data;

public class AppDbContexto : DbContext
{
    public AppDbContexto(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Producto> Productos => Set<Producto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.Property(p => p.Nombre).HasMaxLength(120).IsRequired();
            entity.Property(p => p.Precio).HasPrecision(10, 2);
            entity.Property(p => p.Stock).HasDefaultValue(0);
        });
    }
}
