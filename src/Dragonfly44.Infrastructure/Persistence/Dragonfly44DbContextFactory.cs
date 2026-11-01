using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dragonfly44.Infrastructure.Persistence;

public class Dragonfly44DbContextFactory : IDesignTimeDbContextFactory<Dragonfly44DbContext>
{
    public Dragonfly44DbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<Dragonfly44DbContext>();
        optionsBuilder.UseSqlite("Data Source=dragonfly44.db", sqliteOptions =>
        {
            sqliteOptions.MigrationsAssembly(typeof(Dragonfly44DbContext).Assembly.FullName);
        });

        return new Dragonfly44DbContext(optionsBuilder.Options);
    }
}
