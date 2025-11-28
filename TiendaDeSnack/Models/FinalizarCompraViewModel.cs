using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

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
        public string? NumeroTarjeta { get; set; }
        [Display(Name  ="CVV")]
        public string? CVV { get; set; }

        public bool CompraFinalizada { get; set; } = false;
        public Venta? VentaCreada { get; set; }


        // ----------------------------------------------------
        // Validación condicional y limpieza de datos
        // ----------------------------------------------------
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Solo validar tarjeta si el método elegido es Tarjeta
            if (MetodoPago?.Equals("Tarjeta", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Numero de tarjeta requerido
                var raw = NumeroTarjeta ?? string.Empty;
                var digits = new string(raw.Where(char.IsDigit).ToArray());

                if (string.IsNullOrWhiteSpace(digits))
                {
                    yield return new ValidationResult("Ingrese el número de tarjeta.", new[] { nameof(NumeroTarjeta) });
                }
                else
                {
                    if (digits.Length < 13 || digits.Length > 19 || !LuhnValido(digits))
                    {
                        yield return new ValidationResult("El número de tarjeta no es válido.", new[] { nameof(NumeroTarjeta) });
                    }
                }

                // CVV requerido (3 o 4 dígitos)
                if (string.IsNullOrWhiteSpace(CVV) || !Regex.IsMatch(CVV, @"^\d{3,4}$"))
                {
                    yield return new ValidationResult("El CVV debe tener 3 o 4 dígitos.", new[] { nameof(CVV) });
                }

                // Normalizamos el número (solo dígitos) para evitar errores posteriores
                NumeroTarjeta = digits;
            }
        }

        private static bool LuhnValido(string digits)
        {
            int sum = 0;
            bool alternate = false;

            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int n = digits[i] - '0';
                if (alternate)
                {
                    n *= 2;
                    if (n > 9) n -= 9;
                }
                sum += n;
                alternate = !alternate;
            }

            return (sum % 10) == 0;
        }
    }

}
