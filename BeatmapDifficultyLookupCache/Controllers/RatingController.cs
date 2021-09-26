// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace BeatmapDifficultyLookupCache.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RatingController : Controller
    {
        private readonly DifficultyCache cache;

        public RatingController(DifficultyCache cache)
        {
            this.cache = cache;
        }

        [HttpPost]
        public async Task<double> Post([FromBody] DifficultyRequest request) =>
            (await cache.GetDifficulty(request)).StarRating;
    }
}
