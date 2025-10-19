using System;

namespace TiendaDeSnack.Models
{
    public class Producto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public string? ImagenUrl { get; set; }
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    }
}
