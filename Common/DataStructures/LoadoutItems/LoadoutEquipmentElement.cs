using Arenas.Common.DataStructures;
using Terraria.ModLoader.Config.UI;

namespace Arenas.Common.DataStructures.LoadoutItems;

internal sealed class LoadoutEquipmentElement : ObjectElement
{
    public override void OnBind()
    {
        base.OnBind();

        TextDisplayFunction = () =>
        {
            string baseLabel = Label ?? MemberInfo?.Name ?? "LoadoutEquipment";
            string summary = EquipmentSummary(Value as LoadoutEquipment);
            return string.IsNullOrWhiteSpace(summary) ? baseLabel : $"{baseLabel} {summary}";
        };
    }

    private static string EquipmentSummary(LoadoutEquipment e)
    {
        if (e == null)
            return "";

        return TagUtilities.Join(
            TagUtilities.Tag(e.GrapplingHook), 
            TagUtilities.Tag(e.Mount));
    }
}
