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
using System.Threading.Tasks;

namespace TiendaDeSnack.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IDbContextFactory<AppDbContexto> _contextFactory;
        private readonly AppDbContexto _db;

        public HomeController(ILogger<HomeController> logger, IDbContextFactory<AppDbContexto> contextFactory, AppDbContexto db)
        {
            _logger = logger;
            _contextFactory = contextFactory;
            _db = db;
        }

        public IActionResult Index()
        {
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario");
            ViewBag.Rol = HttpContext.Session.GetString("Rol");
            return View();
        }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizarCompra(FinalizarCompraViewModel model)
        {
            model.ItemsDelCarrito = await GetCartItemsForProcessing(HttpContext.Session.Id);
            model.TotalPagar = model.ItemsDelCarrito.Sum(i => i.PrecioUnitario * i.Cantidad);

            var usuario = HttpContext.Session.GetString("Usuario");

            if (!string.IsNullOrWhiteSpace(usuario) && string.IsNullOrWhiteSpace(model.Nombre))
            {
                using (var lookupDb = _contextFactory.CreateDbContext())
                {
                    var cliente = await lookupDb.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Usuario == usuario);
                    if (cliente != null)
                    {
                        model.Nombre = string.IsNullOrWhiteSpace(cliente.Apellido_P)
                            ? cliente.Nombre
                            : $"{cliente.Nombre} {cliente.Apellido_P}";
                    }
                }
            }

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
                        ClienteUsuario = usuario,
                        ClienteNombre = model.Nombre ?? usuario ?? "Invitado",
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

                    var ids = model.ItemsDelCarrito.Select(i => i.Id).ToList();
                    var itemsToDelete = await dbContext.CarritoItems.Where(c => ids.Contains(c.Id)).ToListAsync();
                    dbContext.CarritoItems.RemoveRange(itemsToDelete);

                    await dbContext.SaveChangesAsync();

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

        public IActionResult Confirmacion(Guid orderId)
        {

            ViewBag.OrderId = orderId;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> MisPedidos()
        {
            var usuario = HttpContext.Session.GetString("Usuario");
            if (string.IsNullOrWhiteSpace(usuario))
                return RedirectToAction("Login");

            using (var dbContext = _contextFactory.CreateDbContext())
            {
                var pedidos = await dbContext.Ventas
                    .Where(v => v.ClienteUsuario == usuario)
                    .OrderByDescending(v => v.Fecha)
                    .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                    .ToListAsync();

                return View(pedidos);
            }
        }

        [HttpGet]
        public async Task<IActionResult> DetallePedido(Guid id)
        {
            var usuario = HttpContext.Session.GetString("Usuario");
            using (var dbContext = _contextFactory.CreateDbContext())
            {
                var venta = await dbContext.Ventas
                    .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.Id == id);

                if (venta == null) return NotFound();

                var esAdmin = HttpContext.Session.GetString("Rol") == "Admin";
                if (!esAdmin && venta.ClienteNombre != usuario)
                    return Forbid();

                return View(venta);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditarDetallePedido(Guid id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            using (var dbContext = _contextFactory.CreateDbContext())
            {
                var venta = await dbContext.Ventas
                    .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.Id == id);

                if (venta == null) return NotFound();

                return View("DetallePedido", venta);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarDetallePedido(Venta model)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            using (var dbContext = _contextFactory.CreateDbContext())
            {
                var ventaExistente = await dbContext.Ventas.FindAsync(model.Id);

                if (ventaExistente == null)
                {
                    TempData["Err"] = "Pedido no encontrado para actualizar.";
                    return RedirectToAction("Panel", new { tab = "Pedidos" });
                }

                ventaExistente.ClienteNombre = model.ClienteNombre;
                ventaExistente.CalleNumero = model.CalleNumero;
                ventaExistente.Ciudad = model.Ciudad;
                ventaExistente.CodigoPostal = model.CodigoPostal;
                ventaExistente.Estado = model.Estado;

                await dbContext.SaveChangesAsync();
                TempData["Ok"] = $"Pedido {model.Id.ToString().Substring(0, 8)}... actualizado con éxito.";

                return RedirectToAction("DetallePedido", new { id = model.Id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarEstadoPedido(Guid id, string estado)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var estadosValidos = new[] { "Preparando", "Enviado", "Entregado", "Cancelado", "Completada" };


            if (!estadosValidos.Contains(estado))
                return BadRequest("Estado no válido.");

            using (var dbContext = _contextFactory.CreateDbContext())
            {
                var venta = await dbContext.Ventas.FirstOrDefaultAsync(v => v.Id == id);
                if (venta == null) return NotFound();

                venta.Estado = estado;
                await dbContext.SaveChangesAsync();
                TempData["Ok"] = $"Estado del pedido {id.ToString().Substring(0, 8)}... actualizado a {estado}.";
            }

            return RedirectToAction("Panel", new { tab = "Pedidos" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPedido(Guid id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            using (var dbContext = _contextFactory.CreateDbContext())
            {
                var detalles = await dbContext.VentasDetalle.Where(d => d.VentaId == id).ToListAsync();
                if (detalles.Any())
                {
                    dbContext.VentasDetalle.RemoveRange(detalles);
                }

                var venta = await dbContext.Ventas.FirstOrDefaultAsync(v => v.Id == id);

                if (venta != null)
                {
                    dbContext.Ventas.Remove(venta);
                }
                else
                {
                    TempData["Err"] = "Pedido no encontrado para eliminar.";
                    return RedirectToAction("Panel", new { tab = "Pedidos" });
                }

                await dbContext.SaveChangesAsync();
                TempData["Ok"] = $"Pedido {id.ToString().Substring(0, 8)}... eliminado con éxito.";

                return RedirectToAction("Panel", new { tab = "Pedidos" });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarDetalleItem(Guid detalleId, Guid ventaId)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            using (var dbContext = _contextFactory.CreateDbContext())
            {
                var detalle = await dbContext.VentasDetalle.FindAsync(detalleId);
                var venta = await dbContext.Ventas.FindAsync(ventaId);

                if (detalle == null || venta == null)
                    return NotFound();

                venta.Total -= detalle.Subtotal;

                dbContext.VentasDetalle.Remove(detalle);
                dbContext.Ventas.Update(venta);
                await dbContext.SaveChangesAsync();

                TempData["Ok"] = "Producto eliminado del pedido y total actualizado.";

                return RedirectToAction("EditarDetallePedido", new { id = ventaId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarDetalleItem(Guid detalleId, Guid ventaId, int nuevaCantidad)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            if (nuevaCantidad <= 0)
                return RedirectToAction("EliminarDetalleItem", new { detalleId, ventaId });

            using (var dbContext = _contextFactory.CreateDbContext())
            {
                var detalle = await dbContext.VentasDetalle.FindAsync(detalleId);
                var venta = await dbContext.Ventas.FindAsync(ventaId);

                if (detalle == null || venta == null)
                    return NotFound();

                decimal subtotalAnterior = detalle.Subtotal;

                detalle.Cantidad = nuevaCantidad;
                detalle.Subtotal = detalle.PrecioUnitario * nuevaCantidad;

                venta.Total = venta.Total - subtotalAnterior + detalle.Subtotal;

                dbContext.VentasDetalle.Update(detalle);
                dbContext.Ventas.Update(venta);
                await dbContext.SaveChangesAsync();

                TempData["Ok"] = $"Cantidad actualizada. Nuevo Total: ${venta.Total.ToString("N2")}";

                return RedirectToAction("EditarDetallePedido", new { id = ventaId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditarEmpleado(Guid id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var empleado = await _db.Empleados.FindAsync(id);

            if (empleado == null) return NotFound();

            return View(empleado); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarEmpleado(Empleado model)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["Err"] = "Error de validación al guardar empleado.";
                return View("EditarEmpleado", model);
            }

            var empleadoExistente = await _db.Empleados.FindAsync(model.Id);

            if (empleadoExistente == null)
            {
                TempData["Err"] = "Empleado no encontrado.";
                return RedirectToAction("Panel", new { tab = "Empleados" });
            }

            empleadoExistente.Nombre = model.Nombre;
            empleadoExistente.Apellido_P = model.Apellido_P;
            empleadoExistente.Usuario = model.Usuario;
            empleadoExistente.Contraseña = model.Contraseña;
            empleadoExistente.TipoUsuario = model.TipoUsuario;

            await _db.SaveChangesAsync();
            TempData["Ok"] = $"Empleado {model.Nombre} actualizado con éxito.";

            return RedirectToAction("Panel", new { tab = "Empleados" });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarEmpleado(Guid id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var empleado = await _db.Empleados.FindAsync(id);

            if (empleado != null)
            {
                _db.Empleados.Remove(empleado);
                await _db.SaveChangesAsync();
                TempData["Ok"] = $"Empleado {empleado.Nombre} eliminado con éxito.";
            }
            else
            {
                TempData["Err"] = "Empleado no encontrado.";
            }

            return RedirectToAction("Panel", new { tab = "Empleados" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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

        [HttpGet]
        public IActionResult RegistroEmpleado() => View();
        [HttpGet]
        public IActionResult Registro() => View();


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(string nombre, string apellido, string usuario, string password)
        {
            nombre = nombre?.Trim();
            apellido = apellido?.Trim();
            usuario = usuario?.Trim();

            var nombreRegex = new Regex(@"^[\p{L}\p{M}\s'-]+$");
            var usuarioRegex = new Regex(@"^[A-Za-z0-9_.-]+$");

            var errores = new List<string>();

            if (string.IsNullOrWhiteSpace(nombre)) errores.Add("El Nombre es obligatorio.");
            if (string.IsNullOrWhiteSpace(apellido)) errores.Add("El Apellido es obligatorio.");
            if (string.IsNullOrWhiteSpace(usuario)) errores.Add("El Usuario es obligatorio.");
            if (string.IsNullOrWhiteSpace(password)) errores.Add("La contraseña es obligatoria.");

            if (!string.IsNullOrWhiteSpace(nombre) && !nombreRegex.IsMatch(nombre))
                errores.Add("El Nombre contiene caracteres no válidos.");
            if (!string.IsNullOrWhiteSpace(apellido) && !nombreRegex.IsMatch(apellido))
                errores.Add("El Apellido  contiene caracteres no válidos.");
            if (!string.IsNullOrWhiteSpace(usuario) && !usuarioRegex.IsMatch(usuario))
                errores.Add("El Usuario solo puede contener letras, números, punto, guion y guion bajo.");

            if (errores.Count > 0)
            {
                ViewBag.Error = string.Join(" ", errores);
                return View();
            }

            var usuarioOcupado =
                await _db.Clientes.AsNoTracking().AnyAsync(c => c.Usuario == usuario) ||
                await _db.Empleados.AsNoTracking().AnyAsync(e => e.Usuario == usuario);

            if (usuarioOcupado)
            {
                ViewBag.Error = "El nombre de usuario ya está en uso.";
                return View();
            }

            var cliente = new Cliente
            {
                Nombre = nombre!,
                Apellido_P = apellido!,
                Usuario = usuario!,
                Contraseña = password
            };
            try
            {
                _db.Clientes.Add(cliente);
                await _db.SaveChangesAsync();

                HttpContext.Session.SetString("Usuario", cliente.Usuario ?? cliente.Nombre);
                HttpContext.Session.SetString("Rol", "Cliente");

                return RedirectToAction("Index");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error al registrar cliente con usuario {Usuario}", usuario);
                ViewBag.Error = "Ocurrió un error guardando el usuario. Inténtalo de nuevo.";
                return View();
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistroEmpleado(string nombre, string apellido, string usuario, string password, string tipoEmpleado)
        {
            nombre = nombre?.Trim();
            apellido = apellido?.Trim();
            usuario = usuario?.Trim();
            tipoEmpleado = tipoEmpleado?.Trim();

            var nombreRegex = new Regex(@"^[\p{L}\p{M}\s'-]+$");
            var usuarioRegex = new Regex(@"^[A-Za-z0-9_.-]+$");

            var errores = new List<string>();

            if (string.IsNullOrWhiteSpace(nombre)) errores.Add("El Nombre es obligatorio.");
            if (string.IsNullOrWhiteSpace(apellido)) errores.Add("El Apellido es obligatorio.");
            if (string.IsNullOrWhiteSpace(usuario)) errores.Add("El Usuario es obligatorio.");
            if (string.IsNullOrWhiteSpace(password)) errores.Add("La contraseña es obligatoria.");

            if (!string.IsNullOrWhiteSpace(nombre) && !nombreRegex.IsMatch(nombre))
                errores.Add("El Nombre contiene caracteres no válidos.");
            if (!string.IsNullOrWhiteSpace(apellido) && !nombreRegex.IsMatch(apellido))
                errores.Add("El Apellido  contiene caracteres no válidos.");

            if (!string.IsNullOrWhiteSpace(usuario) && !usuarioRegex.IsMatch(usuario))
                errores.Add("El Usuario solo puede contener letras, números, punto, guion y guion bajo.");

            if (errores.Count > 0)
            {
                ViewBag.Error = string.Join(" ", errores);
                return View();
            }


            var usuarioOcupado =
                await _db.Clientes.AsNoTracking().AnyAsync(c => c.Usuario == usuario) ||
                await _db.Empleados.AsNoTracking().AnyAsync(e => e.Usuario == usuario);

            if (usuarioOcupado)
            {
                ViewBag.Error = "El nombre de usuario ya está en uso.";
                return View();
            }

            var empleado = new Empleado
            {
                Nombre = nombre!,
                Apellido_P = apellido!,
                Usuario = usuario!,
                Contraseña = password,
                TipoUsuario = tipoEmpleado!
            };

            try
            {
                _db.Empleados.Add(empleado);
                await _db.SaveChangesAsync();


                return RedirectToAction("Panel");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error al registrar empleado con usuario {Usuario}", usuario);
                ViewBag.Error = "Ocurrió un error guardando el usuario. Inténtalo de nuevo.";
                return View();
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

            tab = string.IsNullOrWhiteSpace(tab) ? "Productos" : tab;
            ViewBag.Tab = tab;

            using (var dbContext = _contextFactory.CreateDbContext())
            {
                if (string.Equals(tab, "Pedidos", StringComparison.OrdinalIgnoreCase))
                {
                    var pedidos = dbContext.Ventas
                        .OrderByDescending(v => v.Fecha)
                        .ToList();

                    ViewBag.Pedidos = pedidos;
                }
                else if (string.Equals(tab, "Empleados", StringComparison.OrdinalIgnoreCase))
                {
                    var empleados = dbContext.Empleados
                        .OrderBy(e => e.Nombre)
                        .ToList();

                    ViewBag.Empleados = empleados;
                }
            }
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

                return View("Pedidos", vm);
            }
        }

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