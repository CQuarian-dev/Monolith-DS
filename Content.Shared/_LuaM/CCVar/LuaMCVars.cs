using Robust.Shared.Configuration;

namespace Content.Shared._LuaM.CCVar;

[CVarDefs]
public sealed class LuaMCVars
{
    public const float DefaultScreenAnnouncementX = 0.5f;
    public const float DefaultScreenAnnouncementY = 0.024074074f;
    public const float DefaultLocationAnnouncementX = 0.5f;
    public const float DefaultLocationAnnouncementY = 0.11203704f;
    public static readonly CVarDef<float> ScreenAnnouncementScale =
        CVarDef.Create("luam.screen_announcement_scale", 1f, CVar.ARCHIVE | CVar.CLIENTONLY);
    public static readonly CVarDef<float> LocationAnnouncementScale =
        CVarDef.Create("luam.location_announcement_scale", 1f, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> ScreenAnnouncementsEnabled =
        CVarDef.Create("luam.screen_announcements_enabled", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> ScreenAnnouncementX =
        CVarDef.Create("luam.screen_announcement_x", DefaultScreenAnnouncementX, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> ScreenAnnouncementY =
        CVarDef.Create("luam.screen_announcement_y", DefaultScreenAnnouncementY, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<int> ScreenAnnouncementAlignment =
        CVarDef.Create("luam.screen_announcement_alignment", 1, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<int> LocationAnnouncementAlignment =
        CVarDef.Create("luam.location_announcement_alignment", 1, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> LocationAnnouncementX =
        CVarDef.Create("luam.location_announcement_x", DefaultLocationAnnouncementX, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> LocationAnnouncementY =
        CVarDef.Create("luam.location_announcement_y", DefaultLocationAnnouncementY, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> RpPanelEnabled =
        CVarDef.Create("luam.rp_panel_enabled", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);

    public static readonly CVarDef<bool> RpLungeEnabled =
        CVarDef.Create("luam.rp_lunge_enabled", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<string> RpFavoriteInteractions =
        CVarDef.Create("luam.rp_favorite_interactions", string.Empty, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<string> DeliveryFavorites =
        CVarDef.Create("luam.delivery_favorites", string.Empty, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<string> ConstructionFavorites =
        CVarDef.Create("luam.construction_favorites", string.Empty, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> AnnouncementVolume =
        CVarDef.Create("luam.announcement_volume", 1f, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<int> ActionsActivePreset =
        CVarDef.Create("luam.actions_active_preset", 0, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> AdvancedHotbar =
        CVarDef.Create("luam.advanced_hotbar", false, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> SpaceWindVisuals =
        CVarDef.Create("luam.space_wind_visuals", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> RoundIntroEnabled =
        CVarDef.Create("luam.round_intro", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> LobbyBackgroundParallax =
        CVarDef.Create("luam.lobby_background_parallax", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    // -- Спящие тела вне планет пропускают обработку жизни, когда рядом нет игроков (WGDormancySystem)
    public static readonly CVarDef<bool> OffPlanetDormancy =
        CVarDef.Create("luam.off_planet_dormancy", false, CVar.SERVERONLY);

    // -- Механики Z-уровней из Monolith#4690 (Wolfgate): трение о землю вместо парковки, столкновения со стенами рельефа,
    // -- полёт на джетпаке между слоями, тени слоёв. Выключено — Z-уровни работают как раньше. Планетам (wg.planet_networks) нужно включённым.
    public static readonly CVarDef<bool> ZLevelsGridNetworks =
        CVarDef.Create("luam.zlevels_grid_networks", false, CVar.SERVER | CVar.REPLICATED);

    // -- На сколько тайлов на экране сдвинут соседний Z-уровень: высота «обрыва» между уровнями (в CE 0.7)
    public static readonly CVarDef<float> ZLevelOffset =
        CVarDef.Create("luam.zlevel_offset", 0.4f, CVar.ARCHIVE | CVar.CLIENTONLY);

    // -- Самая густая тень от уровня над головой, 0..1 (в CE 0.98)
    public static readonly CVarDef<float> ZLevelShadow =
        CVarDef.Create("luam.zlevel_shadow", 0.6f, CVar.ARCHIVE | CVar.CLIENTONLY);

    // -- Доля дневного света под крышей на поверхности планет, 0..1 (0 — как в Wolfgate, кромешная тьма без ламп)
    public static readonly CVarDef<float> RoofIndoorLight =
        CVarDef.Create("luam.roof_indoor_light", 0.35f, CVar.ARCHIVE | CVar.CLIENTONLY);
}
