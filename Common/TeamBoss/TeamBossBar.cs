using System.Linq;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;

namespace Arenas.Common.TeamBoss;

internal sealed class TeamBossBar : GlobalBossBar
{
    public override void PostDraw(SpriteBatch spriteBatch, NPC npc, BossBarDrawParams drawParams)
    {
        Point bossBarPosition = new(456, 22);
        Rectangle rectangle =
            Utils.CenteredRectangle(Main.ScreenSize.ToVector2() * new Vector2(0.5f, 1f) + new Vector2(0f, -50f),
                bossBarPosition.ToVector2());

        TeamBossGlobalNPC boss = npc.GetGlobalNPC<TeamBossGlobalNPC>();

        var teamLife = boss.TeamLife
            .Where(entry => boss.HasBeenHurtByTeam.Contains(entry.Key))
            .OrderByDescending(kv => kv.Value);
        foreach (var (team, life) in teamLife)
        {
            if (Main.GameUpdateCount % (60 * 20) == 0)
                Log.Debug($"Boss ({npc.TypeName}) {team} damage: {npc.lifeMax - life}");

            if (team == Team.None)
                continue;

            float lifeRemaining = (float)life / npc.lifeMax;

            Rectangle frame = TextureAssets.Pvp[1].Value.Frame(6);
            frame.X = frame.Width * (int)team;

            Color color = Color.White * MathHelper.Clamp((1f - lifeRemaining) / .15f, 0f, 1f);

            spriteBatch.Draw(
                TextureAssets.Pvp[1].Value,
                rectangle.TopLeft() + new Vector2(
                    (bossBarPosition.X * lifeRemaining) - (frame.Height / 2.0f),
                    -30.0f
                ),
                frame,
                color
            );
        }
    }
}
