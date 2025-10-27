using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaDeSnack.Models;
using TiendaDeSnack.Data;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace TiendaDeSnack.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContexto _db;

        public HomeController(ILogger<HomeController> logger, AppDbContexto db)
        {
            _logger = logger;
            _db = db;
        }

        // Página principal (clientes)
        public IActionResult Index()
        {
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario");
            ViewBag.Rol = HttpContext.Session.GetString("Rol");
            return View();
        }

        public IActionResult Menu() => View();
        public IActionResult Pedidos() => View();
        public IActionResult Resenas() => View();

        [HttpGet]
        public IActionResult Registro() => View();

        [HttpGet]
        public IActionResult Login() => View();

        // Panel único corregido (Admin)
        [HttpGet]
        public IActionResult Panel(string? tab = null)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index");

            ViewBag.Tab = string.IsNullOrWhiteSpace(tab) ? "Productos" : tab;
            return View("Panel"); // Views/Home/Panel.cshtml
        }

        // Registro que se conecta con base de datos
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

        // ✅ Login que conecta con base de datos
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string usuario, string password)
        {
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Por favor, completa todos los campos.";
                return View();
            }

            // 1️⃣ Intento como Cliente
            var cliente = await _db.Clientes
                                   .AsNoTracking()
                                   .SingleOrDefaultAsync(c => c.Usuario == usuario);

            if (cliente != null && cliente.Contraseña == password)
            {
                HttpContext.Session.SetString("Usuario", cliente.Usuario ?? cliente.Nombre);
                HttpContext.Session.SetString("Rol", "Cliente");
                return RedirectToAction("Index");
            }

            // 2️⃣ Intento como Empleado (incluye Admin / Repartidor)
            var empleado = await _db.Empleados
                                    .AsNoTracking()
                                    .SingleOrDefaultAsync(e => e.Usuario == usuario);

            if (empleado != null && empleado.Contraseña == password)
            {
                HttpContext.Session.SetString("Usuario", empleado.Usuario ?? empleado.Nombre);
                HttpContext.Session.SetString("Rol", empleado.TipoUsuario ?? "Empleado");

                // Si es Admin -> Panel
                if (string.Equals(empleado.TipoUsuario, "Admin", StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Panel", "Home");

                return RedirectToAction("Index");
            }
            ViewBag.Error = "Usuario o contraseña incorrectos.";
            return View();
        }

        //  Cerrar sesión
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
}
