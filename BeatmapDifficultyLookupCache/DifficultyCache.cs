// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
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

        private readonly ConcurrentDictionary<DifficultyRequest, CancellationTokenSource> requestExpirationSources = new ConcurrentDictionary<DifficultyRequest, CancellationTokenSource>();
        private readonly ConcurrentDictionary<int, CancellationTokenSource> beatmapExpirationSources = new ConcurrentDictionary<int, CancellationTokenSource>();

        private readonly IConfiguration config;
        private readonly IMemoryCache cache;
        private readonly ILogger logger;

        public DifficultyCache(IConfiguration config, IMemoryCache cache, ILogger<DifficultyCache> logger)
        {
            this.config = config;
            this.cache = cache;
            this.logger = logger;
        }

        public async Task<DifficultyAttributes> GetDifficulty(DifficultyRequest request)
        {
            if (request.BeatmapId == 0)
                return new DifficultyAttributes(Array.Empty<Mod>(), Array.Empty<Skill>(), -1);

            return await cache.GetOrCreateAsync(request, async entry =>
            {
                logger.LogInformation("Computing difficulty (beatmap: {BeatmapId}, ruleset: {RulesetId}, mods: {Mods})",
                    request.BeatmapId,
                    request.RulesetId,
                    request.Mods.Select(m => m.ToString()));

                var requestExpirationSource = requestExpirationSources[request] = new CancellationTokenSource();

                entry.SetPriority(CacheItemPriority.Normal);
                entry.AddExpirationToken(new CancellationChangeToken(requestExpirationSource.Token));

                var ruleset = available_rulesets.First(r => r.RulesetInfo.ID == request.RulesetId);
                var mods = request.Mods.Select(m => m.ToMod(ruleset)).ToArray();
                var beatmap = await getBeatmap(request.BeatmapId);

                var difficultyCalculator = ruleset.CreateDifficultyCalculator(beatmap);
                return difficultyCalculator.Calculate(mods);
            });
        }

        public void Purge(int? beatmapId, int? rulesetId)
        {
            logger.LogInformation("Purging (beatmap: {BeatmapId}, ruleset: {RulesetId})", beatmapId, rulesetId);

            foreach (var (req, source) in requestExpirationSources)
            {
                if (beatmapId != null && req.BeatmapId != beatmapId)
                    continue;

                if (rulesetId != null && req.RulesetId != rulesetId)
                    continue;

                source.Cancel();
            }

            if (beatmapId is int b)
            {
                if (beatmapExpirationSources.TryGetValue(b, out var source))
                    source.Cancel();
            }
            else
            {
                foreach (var (_, source) in beatmapExpirationSources)
                    source.Cancel();
            }
        }

        private Task<WorkingBeatmap> getBeatmap(int beatmapId)
        {
            return cache.GetOrCreateAsync<WorkingBeatmap>($"{beatmapId}.osu", async entry =>
            {
                logger.LogInformation("Downloading beatmap ({BeatmapId})", beatmapId);

                var beatmapExpirationSource = beatmapExpirationSources[beatmapId] = new CancellationTokenSource();

                entry.SetPriority(CacheItemPriority.Low);
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(1));
                entry.AddExpirationToken(new CancellationChangeToken(beatmapExpirationSource.Token));

                var req = new WebRequest(string.Format(config["Beatmaps:DownloadPath"], beatmapId))
                {
                    AllowInsecureRequests = true
                };

                await req.PerformAsync(beatmapExpirationSource.Token);

                if (req.ResponseStream.Length == 0)
                    throw new Exception($"Retrieved zero-length beatmap ({beatmapId})!");

                return new LoaderWorkingBeatmap(req.ResponseStream);
            });
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
