using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using TiendaDeSnack.Models;

namespace TiendaDeSnack.Data
{
    public class AppDbContexto : DbContext
    {
        public AppDbContexto(DbContextOptions<AppDbContexto> options) : base(options) { }

        public DbSet<Producto> Productos => Set<Producto>();
        public DbSet<Empleado> Empleados => Set<Empleado>();
        public DbSet<Cliente>  Clientes  => Set<Cliente>();
        public DbSet<Venta>    Ventas    => Set<Venta>();
        public DbSet<VentaDetalle> VentasDetalle => Set<VentaDetalle>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // Precisión monetaria
            mb.Entity<Producto>().Property(p => p.Precio).HasPrecision(10, 2);
            mb.Entity<Venta>().Property(v => v.Total).HasPrecision(10, 2);
            mb.Entity<VentaDetalle>().Property(d => d.PrecioUnitario).HasPrecision(10, 2);
            mb.Entity<VentaDetalle>().Property(d => d.Subtotal).HasPrecision(10, 2);

            // Relaciones
            mb.Entity<VentaDetalle>()
              .HasOne(d => d.Venta).WithMany(v => v.Detalles)
              .HasForeignKey(d => d.VentaId);

            mb.Entity<VentaDetalle>()
              .HasOne(d => d.Producto).WithMany()
              .HasForeignKey(d => d.ProductoId);
            base.OnModelCreating(mb);

            mb.Entity<Cliente>()
            .HasIndex(c => c.Usuario)
            .IsUnique();

            mb.Entity<Empleado>()
            .HasIndex(e => e.Usuario)
            .IsUnique();
        }
    }
}
