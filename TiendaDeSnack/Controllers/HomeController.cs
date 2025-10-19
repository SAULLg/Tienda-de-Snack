using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TiendaDeSnack.Models;

namespace TiendaDeSnack.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    // --- ¡NUEVOS MÉTODOS AÑADIDOS! ---

    public IActionResult Menu()
    {
        // Busca y muestra el archivo Views/Home/Menu.cshtml
        return View();
    }

    public IActionResult Pedidos()
    {
        // Busca y muestra el archivo Views/Home/Pedidos.cshtml
        return View();
    }

    public IActionResult Resenas()
    {
        // Busca y muestra el archivo Views/Home/Resenas.cshtml
        return View();
    }

    // ------------------------------------

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