using Microsoft.EntityFrameworkCore;
using TiendaDeSnack.Data;
using Microsoft.AspNetCore.Session;
// Asegúrate de incluir estos using si no están

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURACIÓN DE SERVICIOS (Dependency Injection) ---

builder.Services.AddControllersWithViews();

// 1. Añadir el servicio de Sesión
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// 2. 🚨 CAMBIO CLAVE: USAR AddDbContextFactory
builder.Services.AddDbContextFactory<AppDbContexto>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConexionSQL")));

var app = builder.Build();

// --- 2. MIDDLEWARE ---
// ... (El resto del código de app.UseHttpsRedirection, app.UseStaticFiles, etc., permanece igual)

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();