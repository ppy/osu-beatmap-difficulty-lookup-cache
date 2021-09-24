// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Online.API;

namespace BeatmapDifficultyLookupCache
{
    public class DifficultyRequest
    {
        [JsonProperty("beatmap_id")]
        public int BeatmapId { get; set; }

        [JsonProperty("ruleset_id")]
        public int RulesetId { get; set; }

        [JsonProperty("mods")]
        public List<APIMod>? Mods { get; set; }
    }
}
