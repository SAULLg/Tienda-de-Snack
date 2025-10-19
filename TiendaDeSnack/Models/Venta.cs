using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TiendaDeSnack.Models
{
    public class Venta
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        [MaxLength(120)] public string? ClienteNombre { get; set; }

        public Guid? EmpleadoId { get; set; }
        public Empleado? Empleado { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Total { get; set; }

        public List<VentaDetalle> Detalles { get; set; } = new();
    }

    public class VentaDetalle
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();

        public Guid VentaId { get; set; }
        public Venta? Venta { get; set; }

        public Guid ProductoId { get; set; }
        public Producto? Producto { get; set; }

        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal PrecioUnitario { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }
    }
}
