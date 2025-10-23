var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// ➜ agregar sesión
builder.Services.AddSession();

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
