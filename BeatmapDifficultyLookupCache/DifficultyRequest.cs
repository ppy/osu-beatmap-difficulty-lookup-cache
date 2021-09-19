// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Online.API;

namespace BeatmapDifficultyLookupCache
{
    public class DifficultyRequest
    {
        public int BeatmapId { get; set; }
        public int RulesetId { get; set; }
        public List<APIMod>? Mods { get; set; }
    }
}
