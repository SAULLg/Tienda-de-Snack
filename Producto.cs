namespace Tienda_de_Snack.Models;

public class Producto
{
    public string Nombre { get; set; } = default!;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
}
