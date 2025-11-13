using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaDeSnack.Models;
using TiendaDeSnack.Data;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Data;
using TiendaDeSnack.ViewModels;
using System.Threading.Tasks; // Agregado para usar Task

namespace TiendaDeSnack.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        // Inyección del Factory
        private readonly IDbContextFactory<AppDbContexto> _contextFactory;

        // Constructor que recibe el Factory
        public HomeController(ILogger<HomeController> logger, IDbContextFactory<AppDbContexto> contextFactory)
        {
            _logger = logger;
            _contextFactory = contextFactory;
        }

        // Página principal (clientes)
        public IActionResult Index()
        {
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario");
            ViewBag.Rol = HttpContext.Session.GetString("Rol");
            return View();
        }

        // Carga los productos de la base de datos para la vista de menú
        public async Task<IActionResult> Menu()
        {
            using (var dbContext = _contextFactory.CreateDbContext())
            {
                var productos = await dbContext.Productos
                    .Where(p => p.Activo)
                    .ToListAsync();

                var promociones = await dbContext.Promociones
                    .Where(p => p.Activo)
                    .ToListAsync();

                var viewModel = new MenuVM
                {
                    Productos = productos,
                    Promociones = promociones
                };

                return View(viewModel);
            }
        }

        // Carga la vista Pedidos (muestra el checkout con el modelo)
        public async Task<IActionResult> Pedidos()
        {
            var sessionId = HttpContext.Session.Id;

            using (var dbContext = _contextFactory.CreateDbContext())
            {
                var cartItems = await dbContext.CarritoItems
                    .Include(c => c.Producto)
                    .Include(c => c.Promocion)
                    .Where(c => c.SessionId == sessionId)
                    .ToListAsync();

                var vm = new FinalizarCompraViewModel
                {
                    ItemsDelCarrito = cartItems,
                    TotalPagar = cartItems.Sum(i => i.PrecioUnitario * i.Cantidad)
                };

                return View(vm);
            }
        }

        public IActionResult Resenas()
        {
            return View();
        }

        // ---------------------------------------------------------------------
        // FUNCIÓN DE CARRITO: Añadir producto (Usa contexto independiente)
        // ---------------------------------------------------------------------

        [HttpPost]
        public async Task<IActionResult> AddToCart([FromBody] CartRequest request)
        {
            if (request.productId == null || request.productId == Guid.Empty)
            {
                return Json(new { success = false, message = "ID de producto no especificado." });
            }

            HttpContext.Session.SetString("CartInit", "1");
            var sessionId = HttpContext.Session.Id;
            Guid itemIdGuid = request.productId.Value;

            try
            {
                using (var dbContext = _contextFactory.CreateDbContext())
                {
                    var producto = await dbContext.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == itemIdGuid);
                    Promocion? promo = null;
                    bool isPromo = false;

                    if (producto == null)
                    {
                        promo = await dbContext.Promociones.AsNoTracking().FirstOrDefaultAsync(p => p.Id == itemIdGuid && p.Activo);
                        if (promo == null)
                        {
                            return Json(new { success = false, message = "No se encontró producto ni promoción con ese Id." });
                        }
                        isPromo = true;
                    }

                    CarritoItem? cartItem;
                    Guid? productoId = isPromo ? null : (Guid?)producto!.Id;
                    Guid? promocionId = isPromo ? promo!.Id : null;
                    decimal precioUnitario = isPromo ? promo!.Precio : producto!.Precio;
                    string nombreItem = isPromo ? promo!.Nombre + " (Promo)" : producto!.Nombre;


                    // 3) Buscar y Consolidar
                    cartItem = await dbContext.CarritoItems
                        .FirstOrDefaultAsync(c => c.ProductoId == productoId && c.PromocionId == promocionId && c.SessionId == sessionId);

                    if (cartItem != null)
                    {
                        cartItem.Cantidad++;
                        dbContext.CarritoItems.Update(cartItem);
                    }
                    else
                    {
                        cartItem = new CarritoItem
                        {
                            SessionId = sessionId,
                            ProductoId = productoId,
                            PromocionId = promocionId,
                            PrecioUnitario = precioUnitario,
                            Cantidad = 1
                        };
                        dbContext.CarritoItems.Add(cartItem);
                    }

                    await dbContext.SaveChangesAsync();
                    return Json(new { success = true, message = $"{nombreItem} agregado al carrito." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al añadir al carrito.");
                return StatusCode(500, Json(new { success = false, message = "Error interno del servidor al procesar el pedido." }));
            }
        }

        // Carga los ítems del carrito para el Offcanvas
        [HttpGet]
        public async Task<IActionResult> GetCartItems()
        {
            var sessionId = HttpContext.Session.Id;

            using (var dbContext = _contextFactory.CreateDbContext())
            {
                using (var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadUncommitted))
                {
                    var cartItems = await dbContext.CarritoItems
                        .Include(c => c.Producto)
                        .Include(c => c.Promocion)
                        .Where(c => c.SessionId == sessionId)
                        .OrderByDescending(c => c.Id)
                        .ToListAsync();

                    await transaction.CommitAsync();

                    var carritoData = cartItems.Select(item => new
                    {
                        Id = item.Id,
                        Nombre = item.Producto?.Nombre ?? item.Promocion?.Nombre + " (Promo)" ?? "Item Desconocido",
                        Precio = item.PrecioUnitario,
                        Cantidad = item.Cantidad,
                        Subtotal = item.PrecioUnitario * item.Cantidad
                    }).ToList();

                    decimal subtotal = carritoData.Sum(item => item.Subtotal);

                    return Json(new { items = carritoData, subtotal = subtotal });
                }
            }
        }

        // Función auxiliar para cargar el carrito en el ViewModel
        private async Task<List<CarritoItem>> GetCartItemsForProcessing(string sessionId)
        {
            using (var dbContext = _contextFactory.CreateDbContext())
            {
                return await dbContext.CarritoItems
                    .Include(c => c.Producto)
                    .Include(c => c.Promocion)
                    .Where(c => c.SessionId == sessionId)
                    .ToListAsync();
            }
        }

        // ---------------------------------------------------------------------
        // FINALIZAR COMPRA
        // ---------------------------------------------------------------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizarCompra(FinalizarCompraViewModel model)
        {
            // 1. Recargar items del carrito en el modelo antes de validar
            model.ItemsDelCarrito = await GetCartItemsForProcessing(HttpContext.Session.Id);
            model.TotalPagar = model.ItemsDelCarrito.Sum(i => i.PrecioUnitario * i.Cantidad);

            // 2. Verificar validaciones y estado del carrito
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Por favor, corrige los errores en los campos de dirección o pago.";
                return View("Pedidos", model);
            }

            if (model.ItemsDelCarrito.Count == 0)
            {
                ViewBag.Error = "El carrito está vacío. Vuelve a empezar.";
                return RedirectToAction("Menu");
            }

            try
            {
                using (var dbContext = _contextFactory.CreateDbContext())
                {
                    var nuevaVenta = new Venta
                    {
                        Id = Guid.NewGuid(),
                        Fecha = DateTime.UtcNow,
                        Total = model.TotalPagar,
                        ClienteNombre = HttpContext.Session.GetString("Usuario") ?? (model.Nombre ?? "Invitado"),
                        Estado = "Completada",
                        CalleNumero = model.CalleNumero,
                        Ciudad = model.Ciudad,
                        CodigoPostal = model.CodigoPostal,
                        MetodoPago = model.MetodoPago
                    };

                    dbContext.Ventas.Add(nuevaVenta);

                    foreach (var item in model.ItemsDelCarrito)
                    {
                        var detalle = new VentaDetalle
                        {
                            Id = Guid.NewGuid(),
                            VentaId = nuevaVenta.Id,
                            ProductoId = item.ProductoId ?? Guid.Empty,
                            Cantidad = item.Cantidad,
                            PrecioUnitario = item.PrecioUnitario,
                            Subtotal = item.PrecioUnitario * item.Cantidad
                        };
                        dbContext.VentasDetalle.Add(detalle);
                    }

                    // Eliminar items del carrito
                    dbContext.CarritoItems.RemoveRange(model.ItemsDelCarrito);

                    await dbContext.SaveChangesAsync();

                    // Limpiar la sesión si deseas
                    HttpContext.Session.Remove("CartInit");

                    var modelConfirm = new FinalizarCompraViewModel
                    {
                        OrderId = nuevaVenta.Id,
                        ItemsDelCarrito = model.ItemsDelCarrito,
                        TotalPagar = model.TotalPagar,
                        CalleNumero = model.CalleNumero,
                        Ciudad = model.Ciudad,
                        CodigoPostal = model.CodigoPostal,
                        MetodoPago = model.MetodoPago
                    };

                    return View("Confirmacion", modelConfirm);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al finalizar la compra para la sesión {SessionId}", HttpContext.Session.Id);
                ViewBag.Error = "Ocurrió un error al procesar el pago. Inténtalo de nuevo.";
                model.ItemsDelCarrito = await GetCartItemsForProcessing(HttpContext.Session.Id);
                return View("Pedidos", model);
            }
        }

        // Opcional: Vista de confirmación
        public IActionResult Confirmacion(Guid orderId)
        {
            ViewBag.OrderId = orderId;
            return View(); // Necesitas crear Views/Home/Confirmacion.cshtml
        }


        // ---------------------------------------------------------------------
        // FUNCIONES DE INCREMENTO/DECREMENTO CANTIDAD EN CARRITO
        // ---------------------------------------------------------------------

        [HttpPost]
        public async Task<IActionResult> IncrementCartItem([FromBody] CartItemChangeRequest request)
        {
            if (request?.itemId == null || request.itemId == Guid.Empty)
            {
                return Json(new { success = false, message = "ID de item no especificado." });
            }

            HttpContext.Session.SetString("CartInit", "1");
            var sessionId = HttpContext.Session.Id;

            try
            {
                using (var dbContext = _contextFactory.CreateDbContext())
                {
                    var item = await dbContext.CarritoItems.FirstOrDefaultAsync(c => c.Id == request.itemId && c.SessionId == sessionId);
                    if (item == null)
                    {
                        return Json(new { success = false, message = "Item no encontrado." });
                    }

                    item.Cantidad++;
                    dbContext.CarritoItems.Update(item);
                    await dbContext.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Cantidad incrementada." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al incrementar item del carrito.");
                return StatusCode(500, Json(new { success = false, message = "Error interno al incrementar." }));
            }
        }

        [HttpPost]
        public async Task<IActionResult> DecrementCartItem([FromBody] CartItemChangeRequest request)
        {
            if (request?.itemId == null || request.itemId == Guid.Empty)
            {
                return Json(new { success = false, message = "ID de item no especificado." });
            }

            HttpContext.Session.SetString("CartInit", "1");
            var sessionId = HttpContext.Session.Id;

            try
            {
                using (var dbContext = _contextFactory.CreateDbContext())
                {
                    var item = await dbContext.CarritoItems.FirstOrDefaultAsync(c => c.Id == request.itemId && c.SessionId == sessionId);
                    if (item == null)
                    {
                        return Json(new { success = false, message = "Item no encontrado." });
                    }

                    if (item.Cantidad > 1)
                    {
                        item.Cantidad--;
                        dbContext.CarritoItems.Update(item);
                    }
                    else
                    {
                        dbContext.CarritoItems.Remove(item);
                    }

                    await dbContext.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Cantidad actualizada." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al decrementar item del carrito.");
                return StatusCode(500, Json(new { success = false, message = "Error interno al decrementar." }));
            }
        }

        // ---------------------------------------------------------------------
        // MÉTODOS DE AUTENTICACIÓN Y ADMINISTRACIÓN
        // ---------------------------------------------------------------------

        [HttpGet]
        public IActionResult RegistroEmpleado() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistroEmpleado(string nombre, string apellido, string usuario, string password, string tipoEmpleado)
        {
            using (var dbContext = _contextFactory.CreateDbContext())
            {
                // NOTA: La lógica de registro de empleado debe ir aquí
                return RedirectToAction("Panel"); // Temporal
            }
        }


        [HttpGet]
        public IActionResult Login() => View();

        [HttpGet]
        public IActionResult Panel(string? tab = null)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index");

            ViewBag.Tab = string.IsNullOrWhiteSpace(tab) ? "Productos" : tab;
            return View("Panel");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string usuario, string password)
        {
            using (var dbContext = _contextFactory.CreateDbContext())
            {
                if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
                {
                    ViewBag.Error = "Por favor, completa todos los campos.";
                    return View();
                }

                var cliente = await dbContext.Clientes.AsNoTracking().SingleOrDefaultAsync(c => c.Usuario == usuario);

                if (cliente != null && cliente.Contraseña == password)
                {
                    HttpContext.Session.SetString("Usuario", cliente.Usuario ?? cliente.Nombre);
                    HttpContext.Session.SetString("Rol", "Cliente");
                    return RedirectToAction("Index");
                }

                var empleado = await dbContext.Empleados.AsNoTracking().SingleOrDefaultAsync(e => e.Usuario == usuario);

                if (empleado != null && empleado.Contraseña == password)
                {
                    HttpContext.Session.SetString("Usuario", empleado.Usuario ?? empleado.Nombre);
                    HttpContext.Session.SetString("Rol", empleado.TipoUsuario ?? "Empleado");

                    if (string.Equals(empleado.TipoUsuario, "Admin", StringComparison.OrdinalIgnoreCase))
                        return RedirectToAction("Panel", "Home");

                    return RedirectToAction("Index");
                }
                ViewBag.Error = "Usuario o contraseña incorrectos.";
                return View();
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // ---------------------------------------------------------------------
        // NUEVAS ACCIONES Y VISTAS
        // ---------------------------------------------------------------------

        // Acción para mostrar el formulario de checkout (usada antes por el menú)
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var sessionId = HttpContext.Session.Id;
            using (var dbContext = _contextFactory.CreateDbContext())
            {
                var cartItems = await dbContext.CarritoItems
                    .Include(c => c.Producto)
                    .Include(c => c.Promocion)
                    .Where(c => c.SessionId == sessionId)
                    .ToListAsync();

                if (cartItems == null || !cartItems.Any())
                    return RedirectToAction("Menu");

                var vm = new FinalizarCompraViewModel
                {
                    ItemsDelCarrito = cartItems,
                    TotalPagar = cartItems.Sum(i => i.PrecioUnitario * i.Cantidad)
                };

                return View("Pedidos", vm); // Reutilizamos la vista Pedidos como formulario de checkout
            }
        }

        // ---------------------------------------------------------------------
        // CLASES AUXILIARES NECESARIAS PARA AJAX
        // ---------------------------------------------------------------------

        public class CartRequest
        {
            public Guid? productId { get; set; }
        }

        public class CartItemChangeRequest
        {
            public Guid? itemId { get; set; }
        }
    }
}