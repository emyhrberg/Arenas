using Arenas.Common.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Terraria.Localization;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace Arenas.Common.DataStructures.ConfigElements;

internal sealed class BossFightPresetListElement : ListElement
{
    private PropertyFieldWrapper _valueMember;
    private Type _wrapperType;

    private sealed class EntryWrapper<T>
    {
        private readonly IList _list;
        private readonly int _index;

        public EntryWrapper(IList list, int index)
        {
            _list = list;
            _index = index;
        }

        [Expand(false)]
        public T Value
        {
            get => _index >= 0 && _index < _list.Count ? (T)_list[_index] : default;
            set
            {
                if (_index >= 0 && _index < _list.Count)
                    _list[_index] = value;
            }
        }
    }

    public override void OnBind()
    {
        base.OnBind();
        EnsureValueMember();
        TextDisplayFunction = () => $"{Label ?? "Boss Fights"} ({(Data as IList)?.Count ?? 0})";
    }

    protected override void SetupList()
    {
        DataList.Clear();
        int top = 0;

        if (Data is not IList list)
            return;

        EnsureEntries(list);
        EnsureValueMember();

        for (int i = 0; i < list.Count; i++)
        {
            int index = i;
            object wrapper = Activator.CreateInstance(_wrapperType, list, index);
            Tuple<UIElement, UIElement> tuple = UIModConfig.WrapIt(DataList, ref top, _valueMember, wrapper, index);

            tuple.Item2.Left.Pixels += 52f;
            tuple.Item2.Width.Pixels -= 58f;

            if (tuple.Item2 is ConfigElement row)
                row.TextDisplayFunction = () => $"{index + 1}: {PresetName(GetPreset(list, index))}";

            tuple.Item1.Append(new BossPresetHead(() => GetPreset(list, index))
            {
                Left = { Pixels = 24f },
                VAlign = .5f
            });

            UIModConfigHoverImage delete = new(DeleteTexture, Language.GetTextValue("tModLoader.ModConfigRemove"))
            {
                VAlign = .5f
            };
            delete.OnLeftClick += (_, _) =>
            {
                list.RemoveAt(index);
                SetupList();
                Interface.modConfig.SetPendingChanges();
            };
            tuple.Item1.Append(delete);
        }
    }

    private void EnsureValueMember()
    {
        if (_valueMember != null)
            return;

        Type elementType = listType ?? MemberInfo.Type.GetGenericArguments()[0];
        _wrapperType = typeof(EntryWrapper<>).MakeGenericType(elementType);
        IList dummyList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
        object dummyWrapper = Activator.CreateInstance(_wrapperType, dummyList, 0);
        _valueMember = ConfigManager.GetFieldsAndProperties(dummyWrapper).First(member => member.Name == "Value");
    }

    private static void EnsureEntries(IList list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is not BossFightPreset preset)
            {
                preset = new BossFightPreset();
                list[i] = preset;
            }

            preset.Boss ??= new NPCDefinition();
            preset.Loadout ??= new Loadout();
        }
    }

    private static BossFightPreset GetPreset(IList list, int index) =>
        index >= 0 && index < list.Count ? list[index] as BossFightPreset : null;

    private static string PresetName(BossFightPreset preset) =>
        preset?.Boss?.Type > 0 ? preset.Boss.DisplayName : "No Boss";

    private sealed class BossPresetHead(Func<BossFightPreset> getPreset) : UIElement
    {
        public override void OnInitialize()
        {
            Width.Set(28f, 0f);
            Height.Set(28f, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            BossVoteDrawer.DrawBossHead(getPreset()?.Boss?.Type ?? 0, GetDimensions().ToRectangle());
        }
    }
}
