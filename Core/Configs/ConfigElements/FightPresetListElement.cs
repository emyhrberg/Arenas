using Arenas.Common.Rounds;
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

namespace Arenas.Core.Configs.ConfigElements;

internal sealed class FightPresetListElement : ListElement
{
    private PropertyFieldWrapper valueMember;
    private Type wrapperType;

    private sealed class EntryWrapper<T>
    {
        private readonly IList list;
        private readonly int index;

        public EntryWrapper(IList list, int index)
        {
            this.list = list;
            this.index = index;
        }

        [Expand(false)]
        public T Value
        {
            get => index >= 0 && index < list.Count ? (T)list[index] : default;
            set
            {
                if (index >= 0 && index < list.Count)
                    list[index] = value;
            }
        }
    }

    public override void OnBind()
    {
        base.OnBind();
        expanded = true;
        pendingChanges = true;
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
            object wrapper = Activator.CreateInstance(wrapperType, list, index);
            Tuple<UIElement, UIElement> tuple = UIModConfig.WrapIt(DataList, ref top, valueMember, wrapper, index);

            tuple.Item2.Left.Pixels += 52f;
            tuple.Item2.Width.Pixels -= 58f;

            if (tuple.Item2 is ConfigElement row)
            {
                row.TextDisplayFunction = () =>
                {
                    BossFightPreset preset = GetPreset(list, index);
                    if (preset == null)
                        return $"{index + 1}: Missing";

                    return $"{index + 1}: {ArenaRoundSystem.PresetName(preset)}";
                };
            }

            tuple.Item1.Append(new BossPresetIcon(() => GetPreset(list, index))
            {
                Left = { Pixels = 24f },
                VAlign = 0.5f
            });

            var delete = new UIModConfigHoverImage(DeleteTexture, Language.GetTextValue("tModLoader.ModConfigRemove"))
            {
                VAlign = 0.5f
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
        if (valueMember != null)
            return;

        Type elementType = listType ?? MemberInfo.Type.GetGenericArguments()[0];
        wrapperType = typeof(EntryWrapper<>).MakeGenericType(elementType);
        IList dummyList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
        object dummyWrapper = Activator.CreateInstance(wrapperType, dummyList, 0);
        valueMember = ConfigManager.GetFieldsAndProperties(dummyWrapper).First(member => member.Name == "Value");
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

    private sealed class BossPresetIcon : UIElement
    {
        private readonly Func<BossFightPreset> getPreset;

        public BossPresetIcon(Func<BossFightPreset> getPreset)
        {
            this.getPreset = getPreset;
            Width.Set(26f, 0f);
            Height.Set(26f, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            BossFightPreset preset = getPreset();
            if (ArenaRoundSystem.IsSandboxPreset(preset))
            {
                Texture2D icon = Ass.IconArenas.Value;
                Rectangle box = GetDimensions().ToRectangle();
                float scale = Math.Min(box.Width / (float)icon.Width, box.Height / (float)icon.Height);
                spriteBatch.Draw(icon, box.Center.ToVector2(), null, Color.White, 0f, icon.Size() * .5f, scale, SpriteEffects.None, 0f);
                return;
            }
            ArenaBossVoteDrawer.DrawBossHead(preset?.Boss?.Type ?? 0, GetDimensions().ToRectangle());
        }
    }
}
