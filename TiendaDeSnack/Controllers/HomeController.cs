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
using System.Threading.Tasks;
using TiendaDeSnack.ViewModels;

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

        // ==========================================
        // 1. VISTAS PRINCIPALES
        // ==========================================

        public IActionResult Index()
        {
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario");
            ViewBag.Rol = HttpContext.Session.GetString("Rol");
            return View();
        }

        // --- BUSCADOR ARREGLADO ---
        // Esta función recibe el texto del input "name='buscar'" de tu barra de navegación
        [HttpGet]
        public async Task<IActionResult> Menu(string? buscar)
        {
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario");
            ViewBag.Rol = HttpContext.Session.GetString("Rol");

            using (var db = _contextFactory.CreateDbContext())
            {
                // 1. Iniciamos la consulta base (solo activos)
                var queryProd = db.Productos.AsNoTracking().Where(p => p.Activo);
                var queryPromo = db.Promociones.AsNoTracking().Where(p => p.Activo);

                // 2. Si hay texto de búsqueda, aplicamos el filtro
                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    // Convertimos a minúsculas para evitar problemas si la DB es sensible a mayúsculas
                    string term = buscar.ToLower();

                    queryProd = queryProd.Where(p =>
                        p.Nombre.ToLower().Contains(term) ||
                        (p.Descripcion != null && p.Descripcion.ToLower().Contains(term)));

                    queryPromo = queryPromo.Where(p =>
                        p.Nombre.ToLower().Contains(term) ||
                        (p.Descripcion != null && p.Descripcion.ToLower().Contains(term)));

                    ViewBag.BusquedaActual = buscar; // Para que no se borre lo que escribiste
                }

                // 3. Ejecutamos la consulta
                var viewModel = new MenuVM
                {
                    Productos = await queryProd.ToListAsync(),
                    Promociones = await queryPromo.ToListAsync()
                };

                return View(viewModel);
            }
        }

        // ==========================================
        // 2. PANEL DE GESTIÓN
        // ==========================================

        [HttpGet]
        public IActionResult Panel(string? tab = null)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index");

            ViewBag.Usuario = HttpContext.Session.GetString("Usuario");
            tab = string.IsNullOrWhiteSpace(tab) ? "Productos" : tab.ToLower();
            ViewBag.Tab = tab;

            using (var db = _contextFactory.CreateDbContext())
            {
                if (tab == "pedidos")
                {
                    ViewBag.Pedidos = db.Ventas.OrderByDescending(v => v.Fecha).ToList();
                }
                else if (tab == "empleados")
                {
                    ViewBag.Empleados = db.Empleados.OrderBy(e => e.Nombre).ToList();
                }
            }
            return View("Panel");
        }

        // ==========================================
        // 3. GESTIÓN DE PEDIDOS (ADMIN)
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> DetallePedido(Guid id)
        {
            var usuario = HttpContext.Session.GetString("Usuario");
            using (var db = _contextFactory.CreateDbContext())
            {
                var venta = await db.Ventas
                    .Include(v => v.Detalles).ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.Id == id);

                if (venta == null) return NotFound();

                var esAdmin = HttpContext.Session.GetString("Rol") == "Admin";
                // Permitir ver si es admin O si es el dueño del pedido (por usuario o nombre)
                bool esDuenio = (venta.ClienteUsuario == usuario) || (venta.ClienteNombre == usuario);

                if (!esAdmin && !esDuenio) return Forbid();

                return View(venta);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditarDetallePedido(Guid id)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin") return Forbid();

            using (var db = _contextFactory.CreateDbContext())
            {
                var venta = await db.Ventas
                    .Include(v => v.Detalles).ThenInclude(d => d.Producto)
                    .FirstOrDefaultAsync(v => v.Id == id);

                if (venta == null) return NotFound();

                return View("DetallePedido", venta);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarDetallePedido(Venta model)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin") return Forbid();

            using (var db = _contextFactory.CreateDbContext())
            {
                var venta = await db.Ventas.FindAsync(model.Id);
                if (venta == null) return NotFound();

                venta.ClienteNombre = model.ClienteNombre;
                venta.CalleNumero = model.CalleNumero;
                venta.Ciudad = model.Ciudad;
                venta.CodigoPostal = model.CodigoPostal;
                venta.Estado = model.Estado;

                await db.SaveChangesAsync();
                TempData["Ok"] = "Pedido actualizado.";
                return RedirectToAction("DetallePedido", new { id = model.Id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarEstadoPedido(Guid id, string estado)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin") return Forbid();

            using (var db = _contextFactory.CreateDbContext())
            {
                var venta = await db.Ventas.FindAsync(id);
                if (venta != null)
                {
                    venta.Estado = estado;
                    await db.SaveChangesAsync();
                    TempData["Ok"] = "Estado actualizado.";
                }
            }
            return RedirectToAction("Panel", new { tab = "Pedidos" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPedido(Guid id)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin") return Forbid();

            using (var db = _contextFactory.CreateDbContext())
            {
                var detalles = await db.VentasDetalle.Where(d => d.VentaId == id).ToListAsync();
                db.VentasDetalle.RemoveRange(detalles);

                var venta = await db.Ventas.FindAsync(id);
                if (venta != null) db.Ventas.Remove(venta);

                await db.SaveChangesAsync();
                TempData["Ok"] = "Pedido eliminado.";
                return RedirectToAction("Panel", new { tab = "Pedidos" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarDetalleItem(Guid detalleId, Guid ventaId, int nuevaCantidad)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin") return Forbid();
            if (nuevaCantidad <= 0) return RedirectToAction("EliminarDetalleItem", new { detalleId, ventaId });

            using (var db = _contextFactory.CreateDbContext())
            {
                var detalle = await db.VentasDetalle.FindAsync(detalleId);
                var venta = await db.Ventas.FindAsync(ventaId);

                if (detalle == null || venta == null) return NotFound();

                decimal subtotalAnterior = detalle.Subtotal;
                detalle.Cantidad = nuevaCantidad;
                detalle.Subtotal = detalle.PrecioUnitario * nuevaCantidad;
                venta.Total = venta.Total - subtotalAnterior + detalle.Subtotal;

                await db.SaveChangesAsync();
                return RedirectToAction("EditarDetallePedido", new { id = ventaId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarDetalleItem(Guid detalleId, Guid ventaId)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin") return Forbid();

            using (var db = _contextFactory.CreateDbContext())
            {
                var detalle = await db.VentasDetalle.FindAsync(detalleId);
                var venta = await db.Ventas.FindAsync(ventaId);

                if (detalle != null && venta != null)
                {
                    venta.Total -= detalle.Subtotal;
                    db.VentasDetalle.Remove(detalle);
                    await db.SaveChangesAsync();
                }
                return RedirectToAction("EditarDetallePedido", new { id = ventaId });
            }
        }

        // ==========================================
        // 4. GESTIÓN DE EMPLEADOS
        // ==========================================

        [HttpGet]
        public IActionResult RegistroEmpleado() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistroEmpleado(string nombre, string apellido, string usuario, string password, string tipoEmpleado)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin") return Forbid();

            var empleado = new Empleado
            {
                Nombre = nombre,
                Apellido_P = apellido,
                Usuario = usuario,
                Contraseña = password,
                TipoUsuario = tipoEmpleado
            };

            try
            {
                using (var db = _contextFactory.CreateDbContext())
                {
                    db.Empleados.Add(empleado);
                    await db.SaveChangesAsync();
                }
                TempData["Ok"] = "Empleado registrado.";
                return RedirectToAction("Panel", new { tab = "Empleados" });
            }
            catch
            {
                ViewBag.Error = "Error al registrar. Verifica que el usuario no exista.";
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditarEmpleado(Guid id)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin") return Forbid();

            using (var db = _contextFactory.CreateDbContext())
            {
                var emp = await db.Empleados.FindAsync(id);
                if (emp == null) return NotFound();
                return View("EditarEmpleado", emp);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarEmpleado(Empleado model)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin") return Forbid();

            using (var db = _contextFactory.CreateDbContext())
            {
                var emp = await db.Empleados.FindAsync(model.Id);
                if (emp != null)
                {
                    emp.Nombre = model.Nombre;
                    emp.Apellido_P = model.Apellido_P;
                    emp.Usuario = model.Usuario;
                    emp.Contraseña = model.Contraseña;
                    emp.TipoUsuario = model.TipoUsuario;
                    await db.SaveChangesAsync();
                    TempData["Ok"] = "Empleado actualizado.";
                }
                return RedirectToAction("Panel", new { tab = "Empleados" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarEmpleado(Guid id)
        {
            if (HttpContext.Session.GetString("Rol") != "Admin") return Forbid();

            using (var db = _contextFactory.CreateDbContext())
            {
                var emp = await db.Empleados.FindAsync(id);
                if (emp != null)
                {
                    db.Empleados.Remove(emp);
                    await db.SaveChangesAsync();
                    TempData["Ok"] = "Empleado eliminado.";
                }
                return RedirectToAction("Panel", new { tab = "Empleados" });
            }
        }

        // ==========================================
        // 5. AUTENTICACIÓN
        // ==========================================

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string usuario, string password)
        {
            using (var db = _contextFactory.CreateDbContext())
            {
                var cliente = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(c => c.Usuario == usuario && c.Contraseña == password);
                if (cliente != null)
                {
                    HttpContext.Session.SetString("Usuario", cliente.Usuario);
                    HttpContext.Session.SetString("Rol", "Cliente");
                    return RedirectToAction("Index");
                }

                var empleado = await db.Empleados.AsNoTracking().SingleOrDefaultAsync(e => e.Usuario == usuario && e.Contraseña == password);
                if (empleado != null)
                {
                    HttpContext.Session.SetString("Usuario", empleado.Usuario);
                    HttpContext.Session.SetString("Rol", empleado.TipoUsuario);

                    // Si es Admin, puede ir al Index normal o Panel, ambos funcionan
                    return RedirectToAction("Index");
                }

                ViewBag.Error = "Credenciales incorrectas.";
                return View();
            }
        }

        [HttpGet]
        public IActionResult Registro() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(string nombre, string apellido, string usuario, string password)
        {
            try
            {
                using (var db = _contextFactory.CreateDbContext())
                {
                    db.Clientes.Add(new Cliente { Nombre = nombre, Apellido_P = apellido, Usuario = usuario, Contraseña = password });
                    await db.SaveChangesAsync();
                }
                HttpContext.Session.SetString("Usuario", usuario);
                HttpContext.Session.SetString("Rol", "Cliente");
                return RedirectToAction("Index");
            }
            catch
            {
                ViewBag.Error = "Error al registrarse. Usuario ocupado.";
                return View();
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        // ==========================================
        // 6. CARRITO (API JSON)
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> AddToCart([FromBody] CartRequest request)
        {
            if (request?.productId == null) return Json(new { success = false });

            HttpContext.Session.SetString("CartInit", "1");
            var sessionId = HttpContext.Session.Id;

            try
            {
                using (var db = _contextFactory.CreateDbContext())
                {
                    var prod = await db.Productos.FindAsync(request.productId);
                    var promo = (prod == null) ? await db.Promociones.FindAsync(request.productId) : null;

                    if (prod == null && promo == null) return Json(new { success = false });

                    var pId = prod?.Id;
                    var prId = promo?.Id;
                    var precio = prod?.Precio ?? promo!.Precio;

                    var item = await db.CarritoItems.FirstOrDefaultAsync(c => c.ProductoId == pId && c.PromocionId == prId && c.SessionId == sessionId);

                    if (item != null)
                    {
                        item.Cantidad++;
                        db.CarritoItems.Update(item);
                    }
                    else
                    {
                        db.CarritoItems.Add(new CarritoItem { SessionId = sessionId, ProductoId = pId, PromocionId = prId, PrecioUnitario = precio, Cantidad = 1 });
                    }
                    await db.SaveChangesAsync();
                    return Json(new { success = true });
                }
            }
            catch { return StatusCode(500); }
        }

        [HttpGet]
        public async Task<IActionResult> GetCartItems()
        {
            try
            {
                var sessionId = HttpContext.Session.Id;
                using (var db = _contextFactory.CreateDbContext())
                {
                    var items = await db.CarritoItems
                        .Include(c => c.Producto).Include(c => c.Promocion)
                        .Where(c => c.SessionId == sessionId).ToListAsync();

                    var data = items.Select(i => new {
                        Id = i.Id,
                        Nombre = i.Producto?.Nombre ?? i.Promocion?.Nombre,
                        Precio = i.PrecioUnitario,
                        Cantidad = i.Cantidad,
                        Subtotal = i.PrecioUnitario * i.Cantidad
                    });

                    return Json(new { items = data, subtotal = data.Sum(x => x.Subtotal) });
                }
            }
            catch { return StatusCode(500, Json(new { success = false })); }
        }

        [HttpPost]
        public async Task<IActionResult> IncrementCartItem([FromBody] CartItemChangeRequest r)
        {
            using (var db = _contextFactory.CreateDbContext()) { var i = await db.CarritoItems.FindAsync(r.itemId); if (i != null) { i.Cantidad++; await db.SaveChangesAsync(); } }
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DecrementCartItem([FromBody] CartItemChangeRequest r)
        {
            using (var db = _contextFactory.CreateDbContext()) { var i = await db.CarritoItems.FindAsync(r.itemId); if (i != null) { if (i.Cantidad > 1) i.Cantidad--; else db.CarritoItems.Remove(i); await db.SaveChangesAsync(); } }
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var sessionId = HttpContext.Session.Id;
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario"); // Para que el Layout no falle

            using (var db = _contextFactory.CreateDbContext())
            {
                var items = await db.CarritoItems.Include(c => c.Producto).Include(c => c.Promocion)
                    .Where(c => c.SessionId == sessionId).ToListAsync();

                if (!items.Any()) return RedirectToAction("Menu");

                return View("Pedidos", new FinalizarCompraViewModel
                {
                    ItemsDelCarrito = items,
                    TotalPagar = items.Sum(x => x.PrecioUnitario * x.Cantidad)
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizarCompra(FinalizarCompraViewModel model)
        {
            var sessionId = HttpContext.Session.Id;
            var usuario = HttpContext.Session.GetString("Usuario");

            using (var db = _contextFactory.CreateDbContext())
            {
                var items = await db.CarritoItems.Include(c => c.Producto).Include(c => c.Promocion).Where(c => c.SessionId == sessionId).ToListAsync();
                if (!items.Any()) return RedirectToAction("Menu");

                // Si hay usuario, intentamos rellenar el nombre si viene vacío
                if (!string.IsNullOrWhiteSpace(usuario) && string.IsNullOrWhiteSpace(model.Nombre))
                {
                    var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Usuario == usuario);
                    if (cliente != null) model.Nombre = cliente.Nombre;
                }

                var venta = new Venta
                {
                    Id = Guid.NewGuid(),
                    Fecha = DateTime.UtcNow,
                    Total = items.Sum(i => i.PrecioUnitario * i.Cantidad),
                    ClienteUsuario = usuario,
                    ClienteNombre = model.Nombre ?? "Invitado",
                    Estado = "Completada",
                    MetodoPago = model.MetodoPago,
                    CalleNumero = model.CalleNumero,
                    Ciudad = model.Ciudad,
                    CodigoPostal = model.CodigoPostal
                };

                db.Ventas.Add(venta);

                foreach (var i in items)
                {
                    db.VentasDetalle.Add(new VentaDetalle
                    {
                        VentaId = venta.Id,
                        ProductoId = i.ProductoId ?? Guid.Empty,
                        Cantidad = i.Cantidad,
                        PrecioUnitario = i.PrecioUnitario,
                        Subtotal = i.PrecioUnitario * i.Cantidad
                    });
                }

                db.CarritoItems.RemoveRange(items);
                await db.SaveChangesAsync();

                // Pasamos el modelo lleno para que la vista Confirmacion no salga vacía
                var confirm = new FinalizarCompraViewModel
                {
                    OrderId = venta.Id,
                    TotalPagar = venta.Total,
                    ItemsDelCarrito = items,
                    CalleNumero = model.CalleNumero,
                    MetodoPago = model.MetodoPago
                };

                return View("Confirmacion", confirm);
            }
        }

        // CORRECCIÓN MIS PEDIDOS (Para que no salga error de servidor)
        [HttpGet]
        public async Task<IActionResult> MisPedidos()
        {
            var usuario = HttpContext.Session.GetString("Usuario");

            if (string.IsNullOrWhiteSpace(usuario))
            {
                return RedirectToAction("Login");
            }

            // Importante: Pasar datos al ViewBag para el Layout
            ViewBag.Usuario = usuario;
            ViewBag.Rol = HttpContext.Session.GetString("Rol");

            using (var db = _contextFactory.CreateDbContext())
            {
                var pedidos = await db.Ventas
                    .Where(v => v.ClienteUsuario == usuario)
                    .OrderByDescending(v => v.Fecha)
                    .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                    .ToListAsync();

                return View(pedidos);
            }
        }

        public class CartRequest { public Guid? productId { get; set; } }
        public class CartItemChangeRequest { public Guid? itemId { get; set; } }
    }
}