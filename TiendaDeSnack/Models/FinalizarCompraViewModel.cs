using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TiendaDeSnack.Models
{
    // Modelo que contendrá todos los datos para la confirmación del pedido.
    public class FinalizarCompraViewModel // ⬅️ Nombre en español
    {
        // ----------------------------------------------------
        // I. DATOS DEL CARRITO (Para mostrar el resumen)
        // ----------------------------------------------------

        public List<CarritoItem> ItemsDelCarrito { get; set; } = new List<CarritoItem>(); // ⬅️ Nombre en español

        public decimal TotalPagar { get; set; }

        // Identificador de la orden creada (se establece después de persistir)
        public Guid? OrderId { get; set; }

        // ----------------------------------------------------
        // II. DATOS DEL ENVÍO (Capturados del formulario [HttpPost])
        // ----------------------------------------------------

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        [Display(Name = "Nombre completo")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La calle y número son obligatorios.")]
        [Display(Name = "Calle y Número")]
        [StringLength(100)]
        public string CalleNumero { get; set; } = string.Empty;

        [Required(ErrorMessage = "La ciudad es obligatoria.")]
        [StringLength(50)]
        public string Ciudad { get; set; } = string.Empty;

        [Required(ErrorMessage = "El código postal es obligatorio.")]
        [Display(Name = "Código Postal")]
        [RegularExpression(@"^\d{5}$", ErrorMessage = "El código postal debe tener 5 dígitos.")]
        public string CodigoPostal { get; set; } = string.Empty;

        // ----------------------------------------------------
        // III. DATOS DEL PAGO (Capturados del formulario [HttpPost])
        // ----------------------------------------------------

        [Required(ErrorMessage = "Debe seleccionar un método de pago.")]
        [Display(Name = "Método de Pago")]
        public string MetodoPago { get; set; } = string.Empty; // Ej: "Tarjeta", "Efectivo"

        // Campo auxiliar para simular la tarjeta
        [Display(Name = "Número de Tarjeta")]
        [CreditCard(ErrorMessage = "El número de tarjeta no es válido.")]
        public string? NumeroTarjeta { get; set; }

        [StringLength(4, MinimumLength = 3, ErrorMessage = "El CVV debe tener 3 o 4 dígitos.")]
        public string? CVV { get; set; }

        public bool CompraFinalizada { get; set; } = false;
        public Venta? VentaCreada { get; set; }
        
    }
}