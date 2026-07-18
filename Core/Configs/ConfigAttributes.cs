using System;

namespace Arenas.Core.Configs;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
internal sealed class ConfigIconAttribute : Attribute
{
    internal int ItemId { get; } = -1;
    internal string AssetName { get; }
    internal string OffAssetName { get; }

    public ConfigIconAttribute(int itemId) => ItemId = itemId;
    public ConfigIconAttribute(string assetName) => AssetName = assetName;
    public ConfigIconAttribute(string assetName, string offAssetName)
    {
        AssetName = assetName;
        OffAssetName = offAssetName;
    }
}
