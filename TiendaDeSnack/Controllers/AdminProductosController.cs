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

        // ---------- Helpers ----------
        private static DiasSemana BuildDias(string[]? dias)
        {
            if (dias == null || dias.Length == 0) return DiasSemana.Ninguno;

            DiasSemana m = DiasSemana.Ninguno;
            foreach (var d in dias)
            {
                switch ((d ?? "").Trim().ToLowerInvariant())
                {
                    case "lunes": m |= DiasSemana.Lunes; break;
                    case "martes": m |= DiasSemana.Martes; break;
                    case "miercoles": m |= DiasSemana.Miercoles; break;
                    case "jueves": m |= DiasSemana.Jueves; break;
                    case "viernes": m |= DiasSemana.Viernes; break;
                    case "sabado": m |= DiasSemana.Sabado; break;
                    case "domingo": m |= DiasSemana.Domingo; break;
                }
            }
            return m;
        }

        private string? SaveImage(IFormFile? imagen, string folder)
        {
            if (imagen == null || imagen.Length == 0) return null;

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imagen.FileName)}";
            var saveDir = Path.Combine(_env.WebRootPath, folder);
            if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);
            using var fs = System.IO.File.Create(Path.Combine(saveDir, fileName));
            imagen.CopyTo(fs);
            return "/" + folder + "/" + fileName;
        }

        // ---------- LISTAR ----------
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

            var promos = await _db.Promociones
                                  .AsNoTracking()
                                  .OrderByDescending(p => p.Activo)
                                  .ThenBy(p => p.Nombre)
                                  .ToListAsync();
            ViewBag.Promos = promos;

            return View("~/Views/Home/AdminProductos.cshtml", productos);
        }

        // ---------- PRODUCTOS ----------
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

            var nuevo = new Producto
            {
                Nombre = nombre.Trim(),
                Precio = precio,
                Descripcion = descripcion,
                ImagenUrl = SaveImage(imagen, "images/productos"),
                Activo = true
            };

            _db.Productos.Add(nuevo);
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Producto agregado correctamente.";
            return RedirectToAction("Index");
        }

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

        [HttpGet]
        public async Task<IActionResult> Editar(Guid id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            var productos = await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync();
            var item = await _db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (item == null)
            {
                TempData["Err"] = "Producto no encontrado.";
                return RedirectToAction("Index");
            }

            ViewBag.Promos = await _db.Promociones.AsNoTracking().OrderByDescending(x => x.Activo).ThenBy(x => x.Nombre).ToListAsync();
            ViewBag.IsEdit = true;
            ViewBag.EditItem = item;
            return View("~/Views/Home/AdminProductos.cshtml", productos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Actualizar(Guid id, IFormFile? imagen, string nombre, decimal precio, string? descripcion, bool activo = true)
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

            prod.Nombre = nombre.Trim();
            prod.Precio = precio;
            prod.Descripcion = descripcion;
            prod.Activo = activo;

            var img = SaveImage(imagen, "images/productos");
            if (img != null) prod.ImagenUrl = img;

            await _db.SaveChangesAsync();
            TempData["Ok"] = "Producto actualizado.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActivo(Guid id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            var prod = await _db.Productos.FindAsync(id);
            if (prod != null)
            {
                prod.Activo = !prod.Activo;
                await _db.SaveChangesAsync();
                TempData["Ok"] = $"Producto {(prod.Activo ? "activado" : "desactivado")}.";
            }
            return RedirectToAction("Index");
        }

        // ---------- PROMOCIONES ----------
        [HttpGet]
        public IActionResult CrearPromocion() => RedirectToAction("Index");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPromocion(
            IFormFile? imagen,
            string nombre,
            decimal precio,
            string? descripcion,
            string[]? dias,               // <— cambio clave: arreglo de días
            TimeSpan? horaInicio,
            TimeSpan? horaFin)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            if (string.IsNullOrWhiteSpace(nombre) || precio <= 0)
            {
                TempData["Err"] = "Completa el nombre y un precio válido para la promoción.";
                return RedirectToAction("Index");
            }

            var promo = new Promocion
            {
                Nombre = nombre.Trim(),
                Precio = precio,
                Descripcion = descripcion,
                ImagenUrl = SaveImage(imagen, "images/promos"),
                DiasPermitidos = BuildDias(dias),   // <— mapeo correcto
                HoraInicio = horaInicio,
                HoraFin = horaFin,
                Activo = true
            };

            _db.Promociones.Add(promo);
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Promoción agregada.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> EditarPromocion(Guid id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            var productos = await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync();
            var promo = await _db.Promociones.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (promo == null)
            {
                TempData["Err"] = "Promoción no encontrada.";
                return RedirectToAction("Index");
            }

            ViewBag.Promos = await _db.Promociones.AsNoTracking().OrderByDescending(x => x.Activo).ThenBy(x => x.Nombre).ToListAsync();
            ViewBag.PromoIsEdit = true;
            ViewBag.PromoEditItem = promo;
            return View("~/Views/Home/AdminProductos.cshtml", productos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPromocion(
            Guid id,
            IFormFile? imagen,
            string nombre,
            decimal precio,
            string? descripcion,
            string[]? dias,               // <— cambio clave
            TimeSpan? horaInicio,
            TimeSpan? horaFin,
            bool activo = true)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            var promo = await _db.Promociones.FindAsync(id);
            if (promo == null)
            {
                TempData["Err"] = "Promoción no encontrada.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(nombre) || precio <= 0)
            {
                TempData["Err"] = "Completa el nombre y un precio válido.";
                return RedirectToAction("EditarPromocion", new { id });
            }

            promo.Nombre = nombre.Trim();
            promo.Precio = precio;
            promo.Descripcion = descripcion;
            promo.HoraInicio = horaInicio;
            promo.HoraFin = horaFin;
            promo.Activo = activo;
            promo.DiasPermitidos = BuildDias(dias);   // <— mapeo correcto

            var img = SaveImage(imagen, "images/promos");
            if (img != null) promo.ImagenUrl = img;

            await _db.SaveChangesAsync();
            TempData["Ok"] = "Promoción actualizada.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPromocion(Guid id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            var promo = await _db.Promociones.FindAsync(id);
            if (promo != null)
            {
                _db.Promociones.Remove(promo);
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Promoción eliminada.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActivoPromocion(Guid id)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (!string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Home");

            var promo = await _db.Promociones.FindAsync(id);
            if (promo != null)
            {
                promo.Activo = !promo.Activo;
                await _db.SaveChangesAsync();
                TempData["Ok"] = $"Promoción {(promo.Activo ? "activada" : "desactivada")}.";
            }
            return RedirectToAction("Index");
        }
    }
}
