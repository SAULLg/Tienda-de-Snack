using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TiendaDeSnack.Models
{
    public class CarritoItem
    {
        // Llave Primaria
        public Guid Id { get; set; } = Guid.NewGuid();

        // Sesión (cliente no logueado)
        [MaxLength(450)]
        public string SessionId { get; set; } = string.Empty;

        // ========= PRODUCTO =========
        public Guid? ProductoId { get; set; }

        [ForeignKey("ProductoId")]
        public Producto? Producto { get; set; }

        // ========= PROMOCIÓN =========
        public Guid? PromocionId { get; set; }

        [ForeignKey("PromocionId")]
        public Promocion? Promocion { get; set; }

        // ========= DATOS DEL CARRITO =========
        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal PrecioUnitario { get; set; }
    }
}
