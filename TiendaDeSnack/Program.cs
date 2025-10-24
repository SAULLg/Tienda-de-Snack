using Microsoft.EntityFrameworkCore;                  
using TiendaDeSnack.Data;                               

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// ➜ agregar sesión
builder.Services.AddSession();

// ➕ registrar DbContext usando la cadena "ConexionSQL" de appsettings.json
builder.Services.AddDbContext<AppDbContexto>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConexionSQL")));

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

// ➜ usar sesión
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
