using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Recam.DataAccess.Factory;

public class RecamDbContextFactory : IDesignTimeDbContextFactory<RecamDbContext>
{
    public RecamDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RecamDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=RecamDb;User Id=sa;Password=Str0ng!Passw0rd;TrustServerCertificate=True")
            .Options;

        return new RecamDbContext(options);
    }
}