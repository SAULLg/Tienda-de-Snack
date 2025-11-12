using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaDeSnack.Models;
using TiendaDeSnack.Data;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System;
using System.Linq;// Necesario para .Sum()
using System.Text.RegularExpressions; 

namespace TiendaDeSnack.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        // Inyección del Factory para crear contextos aislados
        private readonly IDbContextFactory<AppDbContexto> _contextFactory;
        private readonly AppDbContexto _db;

        // Constructor que recibe el Factory
        public HomeController(ILogger<HomeController> logger, IDbContextFactory<AppDbContexto> contextFactory, AppDbContexto db)
        {
            _logger = logger;
            _contextFactory = contextFactory;
            _db = db;
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
                var productos = await dbContext.Productos.ToListAsync();
                return View(productos);
            }
        }

        public IActionResult Pedidos() => View();
        public IActionResult Resenas() => View();
        [HttpGet]
        public IActionResult Registro() => View();
        [HttpGet]
        public IActionResult RegistroEmpleado() => View();

        // Registro de clientes que se conecta con base de datos
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

            //verificar si todos los campos estan completos
            if (string.IsNullOrWhiteSpace(nombre)) errores.Add("El Nombre es obligatorio.");
            if (string.IsNullOrWhiteSpace(apellido)) errores.Add("El Apellido es obligatorio.");
            if (string.IsNullOrWhiteSpace(usuario)) errores.Add("El Usuario es obligatorio.");
            if (string.IsNullOrWhiteSpace(password)) errores.Add("La contraseña es obligatoria.");

            //Caracteres validos
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


            //Valida si no hay el usuario no existe
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

                //inicia sesion con el cliente recien registrado
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

        //Registro de empleados
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

            //verificar si todos los campos estan completos
            if (string.IsNullOrWhiteSpace(nombre)) errores.Add("El Nombre es obligatorio.");
            if (string.IsNullOrWhiteSpace(apellido)) errores.Add("El Apellido es obligatorio.");
            if (string.IsNullOrWhiteSpace(usuario)) errores.Add("El Usuario es obligatorio.");
            if (string.IsNullOrWhiteSpace(password)) errores.Add("La contraseña es obligatoria.");

            //Caracteres validos
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


            //Valida si no hay el usuario no existe
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

            // Inicializa la sesión si es necesario para asegurar la persistencia del Id
            HttpContext.Session.SetString("CartInit", "1");
            var sessionId = HttpContext.Session.Id;

            try
            {
                using (var dbContext = _contextFactory.CreateDbContext())
                {
                    var producto = await dbContext.Productos
                                                    .AsNoTracking()
                                                    .FirstOrDefaultAsync(p => p.Id == request.productId);

                    if (producto == null)
                    {
                        return Json(new { success = false, message = "Producto no encontrado en la base de datos." });
                    }

                    var cartItem = await dbContext.CarritoItems
                        .FirstOrDefaultAsync(c => c.ProductoId == producto.Id && c.SessionId == sessionId);

                    if (cartItem != null)
                    {
                        // SI EXISTE: INCREMENTAR
                        cartItem.Cantidad++;
                        dbContext.CarritoItems.Update(cartItem);
                    }
                    else
                    {
                        // SI NO EXISTE: INSERTAR NUEVO ÍTEM
                        cartItem = new CarritoItem
                        {
                            SessionId = sessionId,
                            ProductoId = producto.Id,
                            PrecioUnitario = producto.Precio,
                            Cantidad = 1
                        };
                        dbContext.CarritoItems.Add(cartItem);
                    }

                    await dbContext.SaveChangesAsync();
                }

                return Json(new { success = true, message = $"Producto agregado al carrito." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al añadir producto al carrito.");
                return StatusCode(500, Json(new { success = false, message = "Error interno del servidor al procesar el pedido." }));
            }
        }

        // Carga los ítems del carrito para el Offcanvas (Usando lectura sin bloqueo)
        [HttpGet]
        public async Task<IActionResult> GetCartItems()
        {
            HttpContext.Session.SetString("CartInit", "1");
            var sessionId = HttpContext.Session.Id;

            using (var dbContext = _contextFactory.CreateDbContext())
            {
                // Para evitar bloqueos, usamos lectura sin bloqueo
                using (var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadUncommitted))
                {
                    var cartItems = await dbContext.CarritoItems
                        .Include(c => c.Producto)
                        .Where(c => c.SessionId == sessionId)
                        .OrderByDescending(c => c.Id)
                        .ToListAsync();

                    await transaction.CommitAsync();

                    var carritoData = cartItems.Select(item => new
                    {
                        Id = item.Id,
                        Nombre = item.Producto.Nombre,
                        Precio = item.PrecioUnitario,
                        Cantidad = item.Cantidad,
                        Subtotal = item.PrecioUnitario * item.Cantidad
                    }).ToList();

                    decimal subtotal = carritoData.Sum(item => item.Subtotal);

                    return Json(new { items = carritoData, subtotal = subtotal });
                }
            }
        }

        // ---------------------------------------------------------------------
        // FUNCIONES DE INCREMENTO/DECREMENTO PARA EL OFFCANVAS
        // ---------------------------------------------------------------------

        [HttpPost]
        public async Task<IActionResult> IncrementCartItem([FromBody] CartItemChangeRequest request)
        {
            if (request?.itemId == null || request.itemId == Guid.Empty)
            {
                _logger.LogWarning("IncrementCartItem called with invalid itemId: {ItemId}", request?.itemId);
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
                        _logger.LogWarning("IncrementCartItem: item not found. SessionId={SessionId} ItemId={ItemId}", sessionId, request.itemId);
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
                _logger.LogError(ex, "Error al incrementar item del carrito. SessionId={SessionId} ItemId={ItemId}", sessionId, request?.itemId);
                return StatusCode(500, Json(new { success = false, message = "Error interno al incrementar." }));
            }
        }

        [HttpPost]
        public async Task<IActionResult> DecrementCartItem([FromBody] CartItemChangeRequest request)
        {
            if (request?.itemId == null || request.itemId == Guid.Empty)
            {
                _logger.LogWarning("DecrementCartItem called with invalid itemId: {ItemId}", request?.itemId);
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
                        _logger.LogWarning("DecrementCartItem: item not found. SessionId={SessionId} ItemId={ItemId}", sessionId, request.itemId);
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
                _logger.LogError(ex, "Error al decrementar item del carrito. SessionId={SessionId} ItemId={ItemId}", sessionId, request?.itemId);
                return StatusCode(500, Json(new { success = false, message = "Error interno al decrementar." }));
            }
        }

        // ---------------------------------------------------------------------
        // MÉTODOS DE AUTENTICACIÓN Y ADMINISTRACIÓN
        // ---------------------------------------------------------------------

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
    }

    // CLASES AUXILIARES NECESARIAS PARA RECIBIR DATOS DEL AJAX
    public class CartRequest
    {
        public Guid? productId { get; set; }
    }

    public class CartItemChangeRequest
    {
        public Guid? itemId { get; set; }
    }
}