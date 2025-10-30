using ItemMaster.Domain;

namespace ItemMaster.Application;

public interface IUnifiedItemMapper
{
    MappingResult MapToUnifiedModel(Item item);
}