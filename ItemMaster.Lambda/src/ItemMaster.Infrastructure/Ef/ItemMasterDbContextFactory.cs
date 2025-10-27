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
                      "Server=database-1.c7appipf23g8.ap-southeast-1.rds.amazonaws.com;Port=3306;Database=itemmaster;Uid=admin;Pwd=!A!wR[:g8cg$437F9MEE1z:?(pW-;TreatTinyAsBoolean=false;SslMode=None;";
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