namespace ItemMaster.Infrastructure;

public interface ISnowflakeItemQueryBuilder
{
    string BuildSelectAll();
    (string sql, object parameters) BuildSelectBySkus(IEnumerable<string> skus);
    string BuildSelectLatest(int count);
}