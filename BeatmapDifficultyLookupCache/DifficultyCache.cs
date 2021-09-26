// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace BeatmapDifficultyLookupCache
{
    public class DifficultyCache
    {
        private static readonly List<Ruleset> available_rulesets = getRulesets();
        private static readonly DifficultyAttributes empty_attributes = new DifficultyAttributes(Array.Empty<Mod>(), Array.Empty<Skill>(), -1);

        private readonly Dictionary<DifficultyRequest, Task<DifficultyAttributes>> attributesCache = new Dictionary<DifficultyRequest, Task<DifficultyAttributes>>();
        private readonly IConfiguration config;
        private readonly ILogger logger;

        public DifficultyCache(IConfiguration config, ILogger<DifficultyCache> logger)
        {
            this.config = config;
            this.logger = logger;
        }

        public Task<DifficultyAttributes> GetDifficulty(DifficultyRequest request)
        {
            if (request.BeatmapId == 0)
                return Task.FromResult(empty_attributes);

            Task<DifficultyAttributes>? task;

            lock (attributesCache)
            {
                if (!attributesCache.TryGetValue(request, out task))
                {
                    attributesCache[request] = task = Task.Run(async () =>
                    {
                        // Trim a few mods which are invalid.
                        var apiMods = request.GetMods()
                                             .Where(m => !string.IsNullOrWhiteSpace(m.Acronym) && m.Acronym.ToUpperInvariant() != "SCOREV2")
                                             .ToArray();

                        logger.LogInformation("Computing difficulty (beatmap: {BeatmapId}, ruleset: {RulesetId}, mods: {Mods})",
                            request.BeatmapId,
                            request.RulesetId,
                            apiMods.Select(m => m.ToString()));

                        try
                        {
                            var ruleset = available_rulesets.First(r => r.RulesetInfo.ID == request.RulesetId);
                            var mods = apiMods.Select(m => m.ToMod(ruleset)).ToArray();
                            var beatmap = await getBeatmap(request.BeatmapId);

                            var difficultyCalculator = ruleset.CreateDifficultyCalculator(beatmap);
                            var attributes = difficultyCalculator.Calculate(mods);

                            // Trim a few members which we don't consume and only take up RAM.
                            attributes.Skills = Array.Empty<Skill>();
                            attributes.Mods = Array.Empty<Mod>();

                            return attributes;
                        }
                        catch (Exception e)
                        {
                            logger.LogWarning("Request failed with \"{Message}\"", e.Message);
                            return empty_attributes;
                        }
                    });
                }
            }

            return task;
        }

        public void Purge(int? beatmapId, int? rulesetId)
        {
            logger.LogInformation("Purging (beatmap: {BeatmapId}, ruleset: {RulesetId})", beatmapId, rulesetId);

            lock (attributesCache)
            {
                foreach (var req in attributesCache.Keys.ToArray())
                    attributesCache.Remove(req);
            }
        }

        private async Task<WorkingBeatmap> getBeatmap(int beatmapId)
        {
            logger.LogInformation("Downloading beatmap ({BeatmapId})", beatmapId);

            var req = new WebRequest(string.Format(config["Beatmaps:DownloadPath"], beatmapId))
            {
                AllowInsecureRequests = true
            };

            await req.PerformAsync();

            if (req.ResponseStream.Length == 0)
                throw new Exception($"Retrieved zero-length beatmap ({beatmapId})!");

            return new LoaderWorkingBeatmap(req.ResponseStream);
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
