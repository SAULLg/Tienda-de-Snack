using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TiendaDeSnack.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContexto>
    {
        public AppDbContexto CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContexto>();
            optionsBuilder.UseSqlServer(
                "Server=localhost;Database=TiendaSnackDB;Trusted_Connection=True;TrustServerCertificate=True;");

            return new AppDbContexto(optionsBuilder.Options);
        }
    }
}
