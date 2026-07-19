using Arenas.Common.Rounds;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace Arenas.Common.LoadoutSelector;

[Autoload(Side = ModSide.Client)]
internal sealed class LoadoutSelectorSystem : ModSystem
{
    private static readonly ArenaClass[] Classes = [ArenaClass.Melee, ArenaClass.Ranger, ArenaClass.Mage, ArenaClass.Summoner];

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index >= 0)
            layers.Insert(index, new LegacyGameInterfaceLayer("Arenas: Loadout Selector", Draw, InterfaceScaleType.UI));
    }

    private static bool Draw()
    {
        Player player = Main.LocalPlayer;
        if (!ShouldShow(player)) return true;

        const int cardWidth = 150;
        const int cardHeight = 116;
        const int gap = 12;
        int totalWidth = Classes.Length * cardWidth + (Classes.Length - 1) * gap;
        int left = (Main.screenWidth - totalWidth) / 2;
        int top = Math.Max(120, Main.screenHeight / 2 - cardHeight / 2);

        Utils.DrawBorderStringBig(Main.spriteBatch, player.dead ? "Choose a loadout to respawn" : "Choose your loadout",
            new Vector2(Main.screenWidth / 2, top - 42), Color.White, .65f, .5f, .5f);

        ArenaClass selected = player.GetModPlayer<ArenaRoundPlayer>().SelectedClass;
        Point mouse = Main.MouseScreen.ToPoint();
        foreach ((ArenaClass arenaClass, int i) in Classes.Select((value, i) => (value, i)))
        {
            Rectangle card = new(left + i * (cardWidth + gap), top, cardWidth, cardHeight);
            bool hovered = card.Contains(mouse);
            Color color = arenaClass == selected ? new Color(70, 155, 85) : hovered ? new Color(75, 105, 165) : new Color(35, 45, 70);
            Utils.DrawInvBG(Main.spriteBatch, card, color * .96f);

            int head = PostMechKits.HeadItem(arenaClass);
            Main.instance.LoadItem(head);
            Texture2D texture = TextureAssets.Item[head].Value;
            float scale = Math.Min(1f, 48f / Math.Max(texture.Width, texture.Height));
            Main.spriteBatch.Draw(texture, new Vector2(card.Center.X, card.Y + 38), null, Color.White, 0f,
                texture.Size() * .5f, scale, SpriteEffects.None, 0f);
            Utils.DrawBorderString(Main.spriteBatch, arenaClass.ToString(), new Vector2(card.Center.X, card.Bottom - 29),
                Color.White, 1f, .5f, .5f);

            if (!hovered) continue;
            player.mouseInterface = true;
            if (Main.mouseLeft && Main.mouseLeftRelease)
                ArenaRoundSystem.RequestClass(arenaClass);
        }
        return true;
    }

    private static bool ShouldShow(Player player)
    {
        if (!ArenaWorldSystem.Active || player == null || !ArenaRoundSystem.IsLocalParticipant
            || ArenaRoundSystem.Phase is not (RoundPhase.FreezeCountdown or RoundPhase.Playing)
            || !ArenaRoundSystem.TryGetCurrentPreset(out var preset) || !PostMechKits.Supports(preset))
            return false;

        ArenaRoundPlayer roundPlayer = player.GetModPlayer<ArenaRoundPlayer>();
        return player.dead || player.HasBuff(BuffID.Frozen) || roundPlayer.SelectedClass == ArenaClass.None;
    }
}
