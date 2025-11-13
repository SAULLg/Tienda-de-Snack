using System.Collections.Generic;
using TiendaDeSnack.Models;

namespace TiendaDeSnack.ViewModels
{
    public class MenuVM
    {
        public List<Producto> Productos { get; set; } = new();
        public List<Promocion> Promociones { get; set; } = new();
    }
}
