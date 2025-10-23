using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TiendaDeSnack.Models;
using Microsoft.AspNetCore.Http;

namespace TiendaDeSnack.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Si hay usuario guardado en sesi�n, se lo pasamos a la vista
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario");
            return View();
        }

        public IActionResult Menu()
        {
            return View();// buscar� Views/Home/Menu.cshtml
        }

        public IActionResult Pedidos()
        {
            return View();// buscar� Views/Home/Pedidos.cshtml
        }

        public IActionResult Resenas()
        {
            return View();// buscar� Views/Home/Resenas.cshtml
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View(); // buscar� Views/Home/Login.cshtml
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string usuario, string password)
        {
            // Validaci�n simple
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Por favor, completa todos los campos.";
                return View();
            }

            // Guardar el usuario en la sesi�n
            HttpContext.Session.SetString("Usuario", usuario);

            // Redirigir al inicio
            return RedirectToAction("Index");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
