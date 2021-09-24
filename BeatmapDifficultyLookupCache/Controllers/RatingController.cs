// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace BeatmapDifficultyLookupCache.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RatingController : Controller
    {
        private readonly IConfiguration config;
        private readonly DifficultyCache cache;

        public RatingController(IConfiguration config, DifficultyCache cache)
        {
            this.config = config;
            this.cache = cache;
        }

        [HttpPost]
        public async Task<double> Post([FromBody] DifficultyRequest request)
            => (await cache.GetDifficulty(request).ConfigureAwait(false)).StarRating;
    }
}
