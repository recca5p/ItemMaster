using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace ItemMaster.Infrastructure.Ef;

public sealed class ItemMasterDbContextFactory : IDesignTimeDbContextFactory<MySqlDbContext>
{
    public MySqlDbContext CreateDbContext(string[] args)
    {
        var connStr = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING")
                      ??
                      "Server=xxxx;Port=3306;Database=xxxx;Uid=admin;Pwd=xxx;TreatTinyAsBoolean=false;SslMode=None;";
        var optionsBuilder = new DbContextOptionsBuilder<MySqlDbContext>();
        ServerVersion version;
        try
        {
            version = ServerVersion.AutoDetect(connStr);
        }
        catch
        {
            version = ServerVersion.Create(new Version(8, 0, 34), ServerType.MySql);
        }

        optionsBuilder.UseMySql(connStr, version);
        return new MySqlDbContext(optionsBuilder.Options);
    }
}