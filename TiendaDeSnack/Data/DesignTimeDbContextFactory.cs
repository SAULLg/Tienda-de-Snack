using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TiendaDeSnack.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDBContexto>
    {
        public AppDBContexto CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDBContexto>();
            optionsBuilder.UseSqlServer(
                "Server=localhost;Database=TiendaSnackDB;Trusted_Connection=True;TrustServerCertificate=True;");

            return new AppDBContexto(optionsBuilder.Options);
        }
    }
}
