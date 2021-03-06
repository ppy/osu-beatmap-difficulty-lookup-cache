// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Mvc;

namespace BeatmapDifficultyLookupCache.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CacheController : Controller
    {
        private readonly DifficultyCache cache;

        public CacheController(DifficultyCache cache)
        {
            this.cache = cache;
        }

        [HttpDelete]
        public void Delete([FromQuery(Name = "beatmap_id")] int beatmapId)
            => cache.Purge(beatmapId);
    }
}
