using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;        
using TiendaDeSnack.Models;
using TiendaDeSnack.Data;                 
using Microsoft.AspNetCore.Http;            

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

        public IActionResult Index()
        {
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario");
            ViewBag.Rol = HttpContext.Session.GetString("Rol");
            return View();
        }

        public IActionResult Menu() => View(); // Views/Home/Menu.cshtml
        public IActionResult Pedidos() => View(); // Views/Home/Pedidos.cshtml
        public IActionResult Resenas() => View(); // Views/Home/Resenas.cshtml

        [HttpGet]
        public IActionResult Login() => View();    // Views/Home/Login.cshtml

        [HttpGet]
        public IActionResult Panel()
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index");

            return View("Panel"); // ← Views/Home/Panel.cshtml
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string usuario, string password)
        {
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Por favor, completa todos los campos.";
                return View();
            }

            // 1) Intento como Cliente
            var cliente = await _db.Clientes
                                   .AsNoTracking()
                                   .SingleOrDefaultAsync(c => c.Usuario == usuario);

            if (cliente != null && cliente.Contraseña == password)
            {
                HttpContext.Session.SetString("Usuario", cliente.Usuario ?? cliente.Nombre);
                HttpContext.Session.SetString("Rol", "Cliente");
                return RedirectToAction("Index");
            }

            // 2) Intento como Empleado (incluye Repartidor/Admin por TipoUsuario)
            var empleado = await _db.Empleados
                                    .AsNoTracking()
                                    .SingleOrDefaultAsync(e => e.Usuario == usuario);

            if (empleado != null && empleado.Contraseña == password)
            {
                HttpContext.Session.SetString("Usuario", empleado.Usuario ?? empleado.Nombre);
                HttpContext.Session.SetString("Rol", empleado.TipoUsuario ?? "Empleado"); // "Empleado" | "Repartidor" | "Admin"

                // 🔁 CAMBIO: redirigir a Panel en Home (no en Admin)
                if (string.Equals(empleado.TipoUsuario, "Admin", StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Panel", "Home");

                return RedirectToAction("Index");
            }

            // Credenciales inválidas
            ViewBag.Error = "Usuario o contraseña incorrectos.";
            return View();
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
}
