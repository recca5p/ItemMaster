using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Pomelo.EntityFrameworkCore.MySql.Storage;

namespace ItemMaster.Infrastructure.Ef;

public sealed class ItemMasterDbContextFactory : IDesignTimeDbContextFactory<ItemMasterDbContext>
{
    public ItemMasterDbContext CreateDbContext(string[] args)
    {
        var connStr = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING")
                     ?? "Server=127.0.0.1;Port=3306;Database=item_master;Uid=im_user;Pwd=im_pass;TreatTinyAsBoolean=false;SslMode=None;";
        var optionsBuilder = new DbContextOptionsBuilder<ItemMasterDbContext>();
        ServerVersion version;
        try
        {
            version = ServerVersion.AutoDetect(connStr);
        }
        catch
        {
            version = ServerVersion.Create(new Version(8,0,34), ServerType.MySql);
        }
        optionsBuilder.UseMySql(connStr, version);
        return new ItemMasterDbContext(optionsBuilder.Options);
    }
}
