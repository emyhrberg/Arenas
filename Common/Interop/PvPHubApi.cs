using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Arenas.Common.Interop;

/// <summary>PvPHub integration through Mod.Call without a weak or assembly reference.</summary>
internal static class PvPHubApi
{
    private const int RequiredApiVersion = 1;

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool IsAvailable => TryGetHub(out _);

    public static bool TryGetSteamId(Player player, out ulong steamId)
    {
        steamId = 0;
        if (player?.active != true || Main.netMode != NetmodeID.Server || !TryGetHub(out Mod hub))
            return false;

        try
        {
            object result = hub.Call("Auth.GetSteamId", player.whoAmI);
            if (result is not ulong id || id == 0 || id > long.MaxValue)
                return false;

            steamId = id;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"PvPHub Steam ID lookup failed for {player.name}: {ex.Message}");
            return false;
        }
    }

    public static async Task<PvPHubApiResult> PostMatchAsync(
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetHub(out Mod hub))
            return PvPHubApiResult.Failure("PvPHub Mod.Call API v1 is not available.");

        try
        {
            object call = cancellationToken.CanBeCanceled
                ? hub.Call("Match.Post", payloadJson, cancellationToken)
                : hub.Call("Match.Post", payloadJson);
            if (call is not Task<string> responseTask)
                return PvPHubApiResult.Failure("PvPHub returned an unexpected Match.Post result.");

            string responseJson = await responseTask.ConfigureAwait(false);
            return JsonSerializer.Deserialize<PvPHubApiResult>(responseJson, JsonOptions)
                ?? PvPHubApiResult.Failure("PvPHub returned an empty Match.Post result.");
        }
        catch (Exception ex)
        {
            return PvPHubApiResult.Failure(ex.Message);
        }
    }

    private static bool TryGetHub(out Mod hub)
    {
        hub = null;
        if (!ModLoader.TryGetMod("PvPHub", out Mod candidate))
            return false;

        try
        {
            if (candidate.Call("Api.Version") is not int version || version < RequiredApiVersion)
                return false;
        }
        catch
        {
            return false;
        }

        hub = candidate;
        return true;
    }
}

internal sealed record PvPHubApiResult(
    bool Success,
    int StatusCode,
    JsonElement Data,
    string Error,
    string RequestSummary)
{
    public static PvPHubApiResult Failure(string error) => new(false, 0, default, error, "");

    public bool TryGetMatchId(out long matchId)
    {
        matchId = 0;
        return Data.ValueKind == JsonValueKind.Object
            && Data.TryGetProperty("id", out JsonElement id)
            && id.TryGetInt64(out matchId);
    }
}
