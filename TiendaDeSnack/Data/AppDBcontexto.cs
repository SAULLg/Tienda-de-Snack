using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using TiendaDeSnack.Models;
using System;

namespace TiendaDeSnack.Data
{
    public class AppDbContexto : DbContext
    {
        public AppDbContexto(DbContextOptions<AppDbContexto> options) : base(options) { }

        // Tablas existentes
        public DbSet<Producto> Productos => Set<Producto>();
        public DbSet<Empleado> Empleados => Set<Empleado>();
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Venta> Ventas => Set<Venta>();
        public DbSet<VentaDetalle> VentasDetalle => Set<VentaDetalle>();

        // NUEVA TABLA: Ítems del carrito
        public DbSet<CarritoItem> CarritoItems => Set<CarritoItem>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // --- Precisión Monetaria ---
            mb.Entity<Producto>().Property(p => p.Precio).HasPrecision(10, 2);
            mb.Entity<Venta>().Property(v => v.Total).HasPrecision(10, 2);
            mb.Entity<VentaDetalle>().Property(d => d.PrecioUnitario).HasPrecision(10, 2);
            mb.Entity<VentaDetalle>().Property(d => d.Subtotal).HasPrecision(10, 2);

            // NUEVO: Precisión para el precio unitario del carrito
            mb.Entity<CarritoItem>().Property(c => c.PrecioUnitario).HasPrecision(10, 2);


            // --- Relaciones ---
            mb.Entity<VentaDetalle>()
              .HasOne(d => d.Venta).WithMany(v => v.Detalles)
              .HasForeignKey(d => d.VentaId);

            mb.Entity<VentaDetalle>()
              .HasOne(d => d.Producto).WithMany()
              .HasForeignKey(d => d.ProductoId);

            // NUEVO: Relación del CarritoItem con Producto
            mb.Entity<CarritoItem>()
              .HasOne(c => c.Producto).WithMany()
              .HasForeignKey(c => c.ProductoId);

            // --- INICIALIZACIÓN DE DATOS (DATA SEEDING) ---
            SeedData(mb);

            base.OnModelCreating(mb);

            mb.Entity<Cliente>()
            .HasIndex(c => c.Usuario)
            .IsUnique();

            mb.Entity<Empleado>()
            .HasIndex(e => e.Usuario)
            .IsUnique();
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Producto>().HasData(
                new Producto
                {
                    Id = new Guid("96210f9b-6320-4311-b1e1-1a067a3637e1"),
                    Nombre = "PEPINOS LOCOS",
                    Precio = 95.00m,
                    Descripcion = "Pepinos rellenos de cacahuates y chamoy.", // Corregido
                    FechaRegistro = DateTime.UtcNow // Corregido
                },
                new Producto
                {
                    Id = new Guid("4b15d0b4-399c-4869-9f70-3d75653b6794"),
                    Nombre = "FRESAS CON CREMA",
                    Precio = 95.00m,
                    Descripcion = "Deliciosas fresas frescas bañadas en crema dulce.", // Corregido
                    FechaRegistro = DateTime.UtcNow // Corregido
                },
                new Producto
                {
                    Id = new Guid("a0d3f2a8-0e1b-4f7f-8c3b-2e9d9e6f2b4c"),
                    Nombre = "TOSTIELOTES",
                    Precio = 130.00m,
                    Descripcion = "Tostitos con elote, queso y aderezos.", // Corregido
                    FechaRegistro = DateTime.UtcNow // Corregido
                },
                new Producto
                {
                    Id = new Guid("5c49e1e3-8a3c-4d5c-9c9f-7b0d7e0b5a6c"),
                    Nombre = "CHURROS LOCOS",
                    Precio = 95.00m,
                    Descripcion = "Churros crujientes preparados con chamoy y dulces.", // Corregido
                    FechaRegistro = DateTime.UtcNow // Corregido
                }
            );
        }
    }
}