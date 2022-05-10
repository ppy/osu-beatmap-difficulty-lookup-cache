// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BeatmapDifficultyLookupCache.Models;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Taiko.Difficulty;

namespace BeatmapDifficultyLookupCache
{
    public class DifficultyCache
    {
        private static readonly List<Ruleset> available_rulesets = getRulesets();
        private static readonly DifficultyAttributes empty_attributes = new DifficultyAttributes(Array.Empty<Mod>(), -1);

        private readonly Dictionary<DifficultyRequest, Task<DifficultyAttributes>> attributesCache = new Dictionary<DifficultyRequest, Task<DifficultyAttributes>>();
        private readonly IConfiguration config;
        private readonly ILogger logger;

        private readonly bool useDatabase;

        public DifficultyCache(IConfiguration config, ILogger<DifficultyCache> logger)
        {
            this.config = config;
            this.logger = logger;

            useDatabase = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("USE_DATABASE_LOOKUPS"));
        }

        private static long totalLookups;

        public async Task<double> GetDifficultyRating(DifficultyRequest request)
        {
            if (request.BeatmapId == 0)
                return 0;

            if (useDatabase)
                return await getDatabasedDifficulty(request);

            return (await computeAttributes(request)).StarRating;
        }

        public async Task<DifficultyAttributes> GetAttributes(DifficultyRequest request)
        {
            if (request.BeatmapId == 0)
                return empty_attributes;

            if (useDatabase)
            {
                try
                {
                    return await getDatabasedAttributes(request);
                }
                catch
                {
                    // Databased attribute retrieval can fail if the database doesn't contain all attributes for a given beatmap.
                    // If such a case occurs, fall back to providing just the star rating rather than outputting exceptions.
                    return new DifficultyAttributes { StarRating = await GetDifficultyRating(request) };
                }
            }

            return await computeAttributes(request);
        }

        private async Task<DifficultyAttributes> getDatabasedAttributes(DifficultyRequest request)
        {
            int mods = getModBitwise(request.RulesetId, request.GetMods());

            beatmap_difficulty_attribute[] rawDifficultyAttributes;

            using (var conn = await Database.GetDatabaseConnection())
            {
                rawDifficultyAttributes = (await conn.QueryAsync<beatmap_difficulty_attribute>(
                    "SELECT * FROM osu_beatmap_difficulty_attribs WHERE beatmap_id = @BeatmapId AND mode = @RulesetId AND mods = @ModValue", new
                    {
                        BeatmapId = request.BeatmapId,
                        RulesetId = request.RulesetId,
                        ModValue = mods
                    })).ToArray();
            }

            DifficultyAttributes attributes;

            switch (request.RulesetId)
            {
                case 0:
                    attributes = new OsuDifficultyAttributes();
                    break;

                case 1:
                    attributes = new TaikoDifficultyAttributes();
                    break;

                case 2:
                    attributes = new CatchDifficultyAttributes();
                    break;

                case 3:
                    attributes = new ManiaDifficultyAttributes();
                    break;

                default:
                    throw new InvalidOperationException($"Invalid ruleset: {request.RulesetId}");
            }

            attributes.FromDatabaseAttributes(rawDifficultyAttributes.ToDictionary(a => (int)a.attrib_id, e => (double)e.value));

            return attributes;
        }

        private async Task<float> getDatabasedDifficulty(DifficultyRequest request)
        {
            int mods = getModBitwise(request.RulesetId, request.GetMods());

            if (Interlocked.Increment(ref totalLookups) % 1000 == 0)
            {
                logger.LogInformation("difficulty lookup for (beatmap: {BeatmapId}, ruleset: {RulesetId}, mods: {Mods})",
                    request.BeatmapId,
                    request.RulesetId,
                    mods);
            }

            using (var conn = await Database.GetDatabaseConnection())
            {
                return await conn.QueryFirstOrDefaultAsync<float>("SELECT diff_unified from osu.osu_beatmap_difficulty WHERE beatmap_id = @BeatmapId AND mode = @RulesetId and mods = @ModValue", new
                {
                    BeatmapId = request.BeatmapId,
                    RulesetId = request.RulesetId,
                    ModValue = mods
                });
            }
        }

        private async Task<DifficultyAttributes> computeAttributes(DifficultyRequest request)
        {
            Task<DifficultyAttributes>? task;

            lock (attributesCache)
            {
                if (!attributesCache.TryGetValue(request, out task))
                {
                    attributesCache[request] = task = Task.Run(async () =>
                    {
                        var apiMods = request.GetMods();

                        logger.LogInformation("Computing difficulty (beatmap: {BeatmapId}, ruleset: {RulesetId}, mods: {Mods})",
                            request.BeatmapId,
                            request.RulesetId,
                            apiMods.Select(m => m.ToString()));

                        try
                        {
                            var ruleset = available_rulesets.First(r => r.RulesetInfo.OnlineID == request.RulesetId);
                            var mods = apiMods.Select(m => m.ToMod(ruleset)).ToArray();
                            var beatmap = await getBeatmap(request.BeatmapId);

                            var difficultyCalculator = ruleset.CreateDifficultyCalculator(beatmap);
                            var attributes = difficultyCalculator.Calculate(mods);

                            // Trim a few members which we don't consume and only take up RAM.
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

            return await task;
        }

        public void Purge(int beatmapId)
        {
            logger.LogInformation("Purging (beatmap: {BeatmapId})", beatmapId);

            lock (attributesCache)
            {
                foreach (var req in attributesCache.Keys.ToArray())
                {
                    if (req.BeatmapId == beatmapId)
                        attributesCache.Remove(req);
                }
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

        private static int getModBitwise(int rulesetId, List<APIMod> mods)
        {
            LegacyMods val = 0;

            foreach (var mod in mods)
                val |= getLegacyMod(mod);

            // HD is only considered for the osu! ruleset when it is alongside FL.
            if (rulesetId != 0 || !val.HasFlag(LegacyMods.Flashlight))
                val &= ~LegacyMods.Hidden;

            return (int)val;

            LegacyMods getLegacyMod(APIMod mod)
            {
                switch (mod.Acronym)
                {
                    case "EZ": return LegacyMods.Easy;

                    case "HR": return LegacyMods.HardRock;

                    case "NC": return LegacyMods.DoubleTime;

                    case "DT": return LegacyMods.DoubleTime;

                    case "HT": return LegacyMods.HalfTime;

                    case "4K": return LegacyMods.Key4;

                    case "5K": return LegacyMods.Key5;

                    case "6K": return LegacyMods.Key6;

                    case "7K": return LegacyMods.Key7;

                    case "8K": return LegacyMods.Key8;

                    case "9K": return LegacyMods.Key9;

                    case "FL" when rulesetId == 0: return LegacyMods.Flashlight;

                    case "HD" when rulesetId == 0: return LegacyMods.Hidden;
                }

                return 0;
            }
        }
    }
}
