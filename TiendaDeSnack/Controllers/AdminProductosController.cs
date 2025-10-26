using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaDeSnack.Data;
using TiendaDeSnack.Models;

namespace TiendaDeSnack.Controllers
{
    public class AdminProductosController : Controller
    {
        private readonly AppDbContexto _db;
        private readonly IWebHostEnvironment _env;

        public AdminProductosController(AppDbContexto db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // LISTAR
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            var productos = await _db.Productos
                                     .AsNoTracking()
                                     .OrderBy(p => p.Nombre)
                                     .ToListAsync();

            return View("~/Views/Home/AdminProductos.cshtml", productos);
        }

        // CREAR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(IFormFile? imagen, string nombre, decimal precio, string? descripcion)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            if (string.IsNullOrWhiteSpace(nombre))
            {
                TempData["Err"] = "El nombre es obligatorio.";
                return RedirectToAction("Index");
            }

            string? relativePath = null;

            if (imagen != null && imagen.Length > 0)
            {
                var folder = "images/productos";
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imagen.FileName)}";
                var saveDir = Path.Combine(_env.WebRootPath, folder);

                if (!Directory.Exists(saveDir))
                    Directory.CreateDirectory(saveDir);

                var fullPath = Path.Combine(saveDir, fileName);
                using (var fs = System.IO.File.Create(fullPath))
                    await imagen.CopyToAsync(fs);

                relativePath = "/" + folder + "/" + fileName;
            }

            var nuevo = new Producto
            {
                Nombre = nombre,
                Precio = precio,
                Descripcion = descripcion,
                ImagenUrl = relativePath
            };

            _db.Productos.Add(nuevo);
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Producto agregado correctamente.";
            return RedirectToAction("Index");
        }

        // ELIMINAR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(Guid id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            var prod = await _db.Productos.FindAsync(id);
            if (prod != null)
            {
                _db.Productos.Remove(prod);
                await _db.SaveChangesAsync();
                TempData["Ok"] = $"Se eliminó el producto: {prod.Nombre}";
            }

            return RedirectToAction("Index");
        }

        // EDITAR (cargar el mismo form con datos)
        [HttpGet]
        public async Task<IActionResult> Editar(Guid id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            var productos = await _db.Productos
                                     .AsNoTracking()
                                     .OrderBy(p => p.Nombre)
                                     .ToListAsync();

            var item = await _db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (item == null)
            {
                TempData["Err"] = "Producto no encontrado.";
                return RedirectToAction("Index");
            }

            ViewBag.IsEdit = true;
            ViewBag.EditItem = item;
            return View("~/Views/Home/AdminProductos.cshtml", productos);
        }

        // ACTUALIZAR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Actualizar(Guid id, IFormFile? imagen, string nombre, decimal precio, string? descripcion)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            var prod = await _db.Productos.FindAsync(id);
            if (prod == null)
            {
                TempData["Err"] = "Producto no encontrado.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(nombre))
            {
                TempData["Err"] = "El nombre es obligatorio.";
                return RedirectToAction("Editar", new { id });
            }

            // Actualizar campos
            prod.Nombre = nombre;
            prod.Precio = precio;
            prod.Descripcion = descripcion;

            // Si viene nueva imagen, guardar y reemplazar
            if (imagen != null && imagen.Length > 0)
            {
                var folder = "images/productos";
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imagen.FileName)}";
                var saveDir = Path.Combine(_env.WebRootPath, folder);

                if (!Directory.Exists(saveDir))
                    Directory.CreateDirectory(saveDir);

                var fullPath = Path.Combine(saveDir, fileName);
                using (var fs = System.IO.File.Create(fullPath))
                    await imagen.CopyToAsync(fs);

                prod.ImagenUrl = "/" + folder + "/" + fileName;
            }

            await _db.SaveChangesAsync();
            TempData["Ok"] = "Producto actualizado.";
            return RedirectToAction("Index");
        }
    }
}
