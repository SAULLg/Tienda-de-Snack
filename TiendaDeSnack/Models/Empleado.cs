using System;
using System.ComponentModel.DataAnnotations;

namespace TiendaDeSnack.Models
{
    public class Empleado
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(50)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Apellido_P { get; set; }

        [MaxLength(50)]
        public string? Usuario { get; set; }

        [MaxLength(100)]
        public string? Contraseña { get; set; }

        // "Empleado" o "Repartidor"
        [Required, MaxLength(20)]
        public string TipoUsuario { get; set; } = "Empleado";
    }
}
