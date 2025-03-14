﻿using System.Linq;
using VideoDL_m3u8.DashParser;

namespace VideoDL_m3u8.Extensions
{
    public static class MpdExtension
    {
        public static Representation? GetWithHighestQualityVideo(
            this Period source, int? maxHeight = null)
        {
            var _maxHeight = maxHeight ?? int.MaxValue;
            return source.AdaptationSets
                .SelectMany(it =>
                {
                    return it.Representations
                        .Select(itt => new
                        {
                            height = itt.Height,
                            width = itt.Width,
                            bandwidth = itt.Bandwidth ?? 0,
                            mimeType = itt.MimeType,
                            contentType = itt.ContentType,
                            ada = it,
                            rep = itt
                        });
                })
                .Where(it =>
                    it.mimeType.StartsWith("video") ||
                    it.contentType.StartsWith("video") ||
                    it.ada.MimeType.StartsWith("video") ||
                    it.ada.ContentType.StartsWith("video"))
                .Select(it => new
                {
                    quality = it.height ?? 0,
                    bandwidth = it.bandwidth,
                    rep = it.rep
                })
                .Where(it => it.quality <= _maxHeight)
                .OrderByDescending(it => it.quality)
                .ThenByDescending(it => it.bandwidth)
                .FirstOrDefault()?
                .rep;
        }

        public static Representation? GetWithHighestQualityAudio(
            this Period source, string? lang = null)
        {
            return source.AdaptationSets
                .SelectMany(it =>
                {
                    return it.Representations
                        .Select(itt => new
                        {
                            lang = it.Lang,
                            bandwidth = itt.Bandwidth ?? 0,
                            mimeType = itt.MimeType,
                            contentType = itt.ContentType,
                            ada = it,
                            rep = itt
                        });
                })
                .Where(it =>
                    it.mimeType.StartsWith("audio") ||
                    it.contentType.StartsWith("audio") ||
                    it.ada.MimeType.StartsWith("audio") ||
                    it.ada.ContentType.StartsWith("audio"))
                .Where(it => lang != null ? it.lang == lang : true)
                .OrderByDescending(it => it.bandwidth)
                .FirstOrDefault()?
                .rep;
        }
    }
}
