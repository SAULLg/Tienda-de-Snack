// Archivo: ViewModels/MenuVM.cs (o Models/MenuVM.cs)
using System.Collections.Generic;
using TiendaDeSnack.Models;

namespace TiendaDeSnack.ViewModels // O el namespace que uses para tus ViewModels
{
    public class MenuVM
    {
        public List<Producto> Productos { get; set; } = new List<Producto>();
        public List<Promocion> Promociones { get; set; } = new List<Promocion>();
    }
}