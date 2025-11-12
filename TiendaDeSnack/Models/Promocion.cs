using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TiendaDeSnack.Models
{
    [Flags]
    public enum DiasSemana { Ninguno = 0, Lunes = 1, Martes = 2, Miercoles = 4, Jueves = 8, Viernes = 16, Sabado = 32, Domingo = 64 }

    public class Promocion
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(80)]
        public string Nombre { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal Precio { get; set; }

        [MaxLength(300)]
        public string? Descripcion { get; set; }

        public string? ImagenUrl { get; set; }

        // Ventana por días/horas (para que NO se venda fuera de horario)
        public DiasSemana DiasPermitidos { get; set; } = DiasSemana.Lunes | DiasSemana.Martes | DiasSemana.Miercoles | DiasSemana.Jueves | DiasSemana.Viernes | DiasSemana.Sabado | DiasSemana.Domingo;
        public TimeSpan? HoraInicio { get; set; }
        public TimeSpan? HoraFin { get; set; }

        public bool Activo { get; set; } = true;
    }
}

