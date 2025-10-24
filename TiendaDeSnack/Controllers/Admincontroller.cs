using Microsoft.AspNetCore.Mvc;

namespace TiendaDeSnack.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Panel()
        {
            // Seguridad mínima: solo admin
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            ViewBag.Usuario = HttpContext.Session.GetString("Usuario");
            return View();
        }
    }
}

