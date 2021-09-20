// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;

namespace BeatmapDifficultyLookupCache.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DifficultyController : Controller
    {
        private static readonly List<Ruleset> available_rulesets = getRulesets();

        private readonly IConfiguration config;

        public DifficultyController(IConfiguration config)
        {
            this.config = config;
        }

        [HttpPost]
        public DifficultyAttributes Post([FromBody] DifficultyRequest request)
        {
            var ruleset = available_rulesets.First(r => r.RulesetInfo.ID == request.RulesetId);
            var mods = request.Mods?.Select(m => m.ToMod(ruleset)).ToArray() ?? Array.Empty<Mod>();
            var beatmap = BeatmapLoader.GetBeatmap(request.BeatmapId, config);

            var difficultyCalculator = ruleset.CreateDifficultyCalculator(beatmap);
            return difficultyCalculator.Calculate(mods);
        }

        private static List<Ruleset> getRulesets()
        {
            const string ruleset_library_prefix = "osu.Game.Rulesets";

            var rulesetsToProcess = new List<Ruleset>();

            foreach (string file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, $"{ruleset_library_prefix}.*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);
                    Type type = assembly.GetTypes().First(t => t.IsPublic && t.IsSubclassOf(typeof(Ruleset)));
                    rulesetsToProcess.Add((Ruleset)Activator.CreateInstance(type)!);
                }
                catch
                {
                    throw new Exception($"Failed to load ruleset ({file})");
                }
            }

            return rulesetsToProcess;
        }
    }
}
