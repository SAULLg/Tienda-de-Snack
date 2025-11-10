using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TiendaDeSnack.Models
{
    public class CarritoItem
    {
        // Llave Primaria
        public Guid Id { get; set; } = Guid.NewGuid();

        // 🚨 CORRECCIÓN CLAVE: Asegurar que la SessionId no se trunque.
        [MaxLength(450)]
        public string SessionId { get; set; } = string.Empty;

        // Clave Foránea al producto
        public Guid ProductoId { get; set; }

        // Propiedades del ítem
        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal PrecioUnitario { get; set; }

        // Propiedad de Navegación (para la inclusión en EF Core)
        [ForeignKey("ProductoId")]
        public Producto Producto { get; set; }
    }
}