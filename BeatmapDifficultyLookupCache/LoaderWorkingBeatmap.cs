// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Skinning;

namespace BeatmapDifficultyLookupCache
{
    public class LoaderWorkingBeatmap : WorkingBeatmap
    {
        private readonly Beatmap beatmap;

        public LoaderWorkingBeatmap(Stream stream)
            : this(new LineBufferedReader(stream))
        {
            stream.Dispose();
        }

        private LoaderWorkingBeatmap(LineBufferedReader reader)
            : this(Decoder.GetDecoder<Beatmap>(reader).Decode(reader))
        {
        }

        private LoaderWorkingBeatmap(Beatmap beatmap)
            : base(beatmap.BeatmapInfo, null)
        {
            this.beatmap = beatmap;
        }

        protected override IBeatmap GetBeatmap() => beatmap;
        protected override Texture GetBackground() => null!;
        protected override Track GetBeatmapTrack() => null!;
        protected override ISkin GetSkin() => null!;
        public override Stream GetStream(string storagePath) => null!;
    }
}
