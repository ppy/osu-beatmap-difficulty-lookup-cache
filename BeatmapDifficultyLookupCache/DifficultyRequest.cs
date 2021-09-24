// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;
using osu.Game.Online.API;

namespace BeatmapDifficultyLookupCache
{
    public class DifficultyRequest : IEquatable<DifficultyRequest>
    {
        [JsonProperty("beatmap_id")]
        public int BeatmapId { get; init; }

        [JsonProperty("ruleset_id")]
        public int RulesetId { get; init; }

        [JsonProperty("mods")]
        public APIMod[] Mods { get; init; } = Array.Empty<APIMod>();

        public bool Equals(DifficultyRequest? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return BeatmapId == other.BeatmapId && RulesetId == other.RulesetId && Equals(Mods, other.Mods);
        }

        public override bool Equals(object? obj)
            => obj is DifficultyRequest other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(BeatmapId, RulesetId, Mods);
    }
}
