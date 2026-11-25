/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Diagnostics.CodeAnalysis;
using Content.Shared._WG.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared._LuaM.Planets;

public sealed partial class PlanetNetworksLoadoutEffect : LoadoutEffect
{
    public override bool Validate(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession? session,
        IDependencyCollection collection, [NotNullWhen(false)] out FormattedMessage? reason)
    {
        if (PlanetCVars.NetworksEnabled(collection.Resolve<IConfigurationManager>()))
        {
            reason = null;
            return true;
        }

        reason = new FormattedMessage();
        return false;
    }
}
