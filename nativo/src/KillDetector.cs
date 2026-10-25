using System;
using System.Collections.Generic;

namespace CrosshairRoulette
{
    // Turns the CS2 Game State Integration feed into kills and shots.
    // The feed describes whoever you are *watching*, so after you die it starts
    // describing a teammate: everything keys off provider.steamid (you) and
    // ignores payloads about anyone else, keeping the kill baseline untouched.
    sealed class KillDetector
    {
        public event Action<int> Kill;          // kills in this update
        public event Action<int> Shot;          // bullets fired in this update
        public event Action<string, string> Status; // observing (self/other/menu/none), map

        string map;
        bool mapSeen;
        bool primed;
        double? kills;
        double roundKills, roundHs;
        string weaponName;
        double? clip;

        void Reset()
        {
            primed = false;
            kills = null;
            roundKills = roundHs = 0;
            weaponName = null;
            clip = null;
        }

        static object ActiveWeapon(object player)
        {
            if (!(Json.Get(player, "weapons") is Dictionary<string, object> weapons)) return null;
            foreach (var w in weapons.Values)
            {
                var state = Json.Str(w, "state", "");
                if (state == "active" || state == "reloading") return w;
            }
            return null;
        }

        public void Update(Dictionary<string, object> payload)
        {
            var provider = Json.Get(payload, "provider");
            if (provider == null) return;
            string me = Json.Str(provider, "steamid", "");
            string newMap = Json.Str(Json.Get(payload, "map"), "name", null);

            if (!mapSeen || newMap != map)
            {
                Reset();
                map = newMap;
                mapSeen = true;
            }

            var player = Json.Get(payload, "player");
            bool isMe = player != null && me != "" && Json.Str(player, "steamid", "") == me;
            string observing = player == null ? "none" : !isMe ? "other" : (Json.Str(player, "activity", "") == "menu" || newMap == null) ? "menu" : "self";
            Status?.Invoke(observing, newMap);
            if (observing != "self") return;

            var st = Json.Get(player, "state");
            var ms = Json.Get(player, "match_stats");
            double? rk = Json.Num(Json.Get(st, "round_kills"));
            double? rhs = Json.Num(Json.Get(st, "round_killhs"));
            double? total = Json.Num(Json.Get(ms, "kills"));

            int newKills = 0;
            if (primed)
            {
                // round_kills drops back to 0 at the start of every round
                double prevRk = rk.HasValue && rk.Value < roundKills ? 0 : roundKills;
                double dRk = rk.HasValue ? Math.Max(0, rk.Value - prevRk) : 0;
                // match_stats is authoritative: it also goes *down* for team kills
                newKills = (int)(total.HasValue && kills.HasValue ? Math.Max(0, total.Value - kills.Value) : dRk);
            }
            if (total.HasValue) kills = total;
            if (rk.HasValue) roundKills = rk.Value;
            if (rhs.HasValue) roundHs = rhs.Value;

            // shots: ammo in the same active weapon going down
            int shots = 0;
            var weapon = ActiveWeapon(player);
            if (weapon != null)
            {
                string name = Json.Str(weapon, "name", "");
                double? c = Json.Num(Json.Get(weapon, "ammo_clip"));
                if (primed && name == weaponName && c.HasValue && clip.HasValue && c.Value < clip.Value && Json.Str(weapon, "state", "") != "reloading")
                    shots = (int)(clip.Value - c.Value);
                weaponName = name;
                clip = c;
            }
            else
            {
                weaponName = null;
                clip = null;
            }

            if (!primed)
            {
                primed = true; // the first payload about you only sets baselines
                return;
            }
            if (shots > 0) Shot?.Invoke(shots);
            if (newKills > 0) Kill?.Invoke(newKills);
        }
    }
}
