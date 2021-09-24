// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Online.API;
using osu.Game.Utils;

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

        private IEnumerable<APIMod> getOrderedMods => Mods.OrderBy(m => m.Acronym);

        public bool Equals(DifficultyRequest? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return BeatmapId == other.BeatmapId && RulesetId == other.RulesetId && getOrderedMods.SequenceEqual(other.getOrderedMods);
        }

        public override bool Equals(object? obj)
            => obj is DifficultyRequest other && Equals(other);

        public override int GetHashCode()
        {
            var hashCode = new HashCode();

            hashCode.Add(BeatmapId);
            hashCode.Add(RulesetId);

            // Todo: Temporary (APIMod doesn't implement GetHashCode()).
            foreach (var m in getOrderedMods)
            {
                hashCode.Add(m.Acronym);

                foreach (var (key, value) in m.Settings)
                {
                    hashCode.Add(key);
                    hashCode.Add(ModUtils.GetSettingUnderlyingValue(value));
                }
            }

            return hashCode.ToHashCode();
        }
    }
}
