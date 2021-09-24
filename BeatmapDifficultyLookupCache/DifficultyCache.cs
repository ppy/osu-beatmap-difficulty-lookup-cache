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
using Microsoft.Extensions.Primitives;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;

namespace BeatmapDifficultyLookupCache
{
    public class DifficultyCache
    {
        private static readonly List<Ruleset> available_rulesets = getRulesets();

        private readonly ConcurrentDictionary<DifficultyRequest, CancellationTokenSource> cancellationSources = new ConcurrentDictionary<DifficultyRequest, CancellationTokenSource>();
        private readonly IConfiguration config;
        private readonly IMemoryCache cache;

        public DifficultyCache(IConfiguration config, IMemoryCache cache)
        {
            this.config = config;
            this.cache = cache;
        }

        public Task<DifficultyAttributes> GetDifficulty(DifficultyRequest request) => cache.GetOrCreateAsync(request, async entry =>
        {
            var cancellationSource = cancellationSources[request] = new CancellationTokenSource();

            entry.Priority = CacheItemPriority.Normal;
            entry.AddExpirationToken(new CancellationChangeToken(cancellationSource.Token));

            var ruleset = available_rulesets.First(r => r.RulesetInfo.ID == request.RulesetId);
            var mods = request.Mods.Select(m => m.ToMod(ruleset)).ToArray();
            var beatmap = await getBeatmap(request.BeatmapId, cancellationSource.Token);

            var difficultyCalculator = ruleset.CreateDifficultyCalculator(beatmap);
            return difficultyCalculator.Calculate(mods);
        });

        public void Purge(int? beatmapId, int? rulesetId)
        {
            foreach (var (req, cancellationSource) in cancellationSources)
            {
                if (beatmapId != null && req.BeatmapId != beatmapId)
                    continue;

                if (rulesetId != null && req.RulesetId != rulesetId)
                    continue;

                cancellationSource.Cancel();
            }
        }

        private Task<WorkingBeatmap> getBeatmap(int beatmapId, CancellationToken token) => cache.GetOrCreateAsync<WorkingBeatmap>($"{beatmapId}.osu", async entry =>
        {
            entry.Priority = CacheItemPriority.Normal;
            entry.SetAbsoluteExpiration(TimeSpan.FromHours(1));
            entry.AddExpirationToken(new CancellationChangeToken(token));

            var req = new WebRequest(string.Format(config["Beatmaps:DownloadPath"], beatmapId))
            {
                AllowInsecureRequests = true
            };

            await req.PerformAsync(token);

            return new LoaderWorkingBeatmap(req.ResponseStream);
        });

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
