// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace BeatmapDifficultyLookupCache.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PurgeController : Controller
    {
        private readonly IConfiguration config;
        private readonly DifficultyCache cache;

        public PurgeController(IConfiguration config, DifficultyCache cache)
        {
            this.config = config;
            this.cache = cache;
        }

        [HttpDelete]
        public void Delete([FromQuery(Name = "beatmap_id")] int? beatmapId, [FromQuery(Name = "ruleset_id")] int? rulesetId)
            => cache.Purge(beatmapId, rulesetId);
    }
}
