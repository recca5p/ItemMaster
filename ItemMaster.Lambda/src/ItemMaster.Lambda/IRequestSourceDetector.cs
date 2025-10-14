using ItemMaster.Shared;

namespace ItemMaster.Lambda;

public interface IRequestSourceDetector
{
    RequestSource DetectSource(object input);
}