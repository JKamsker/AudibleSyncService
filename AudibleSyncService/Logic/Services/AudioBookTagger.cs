using ATL;

using AudibleApi.Common;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AudibleSyncService.Logic.Services
{
    public class Tagger
    {
        private readonly ILogger<Tagger> _logger;

        public Tagger(ILogger<Tagger> logger)
        {
            _logger = logger;
        }

        public bool TryTagFile(FileInfo file, Item metaData)
        {
            try
            {


                var track = new Track(file.FullName);

                Debug.Assert(track.EmbeddedPictures?.Count > 0);

                track.AdditionalFields = new Dictionary<string, string>();

                track.Artist = metaData.Authors?.Select(x => x.Name).JoinString("; ") ?? String.Empty;
                track.Title = metaData.Title;
                track.Album = metaData.Title;

                if (metaData.Subtitle is not null)
                {
                    track.AdditionalFields["TIT3"] = metaData.Subtitle;
                    track.AdditionalFields["----:com.apple.iTunes:subtitle"] = metaData.Subtitle;
                }

                if (metaData.PublisherName is not null)
                {
                    track.Publisher = metaData.PublisherName;
                    track.AdditionalFields["----:com.apple.iTunes:publisher"] = metaData.PublisherName;
                }

                track.Year = metaData.DatePublished?.Year ?? 0;
                track.Composer = metaData.Narrators?.Select(x => x.Name).JoinString("; ") ?? String.Empty;

                if (metaData.Description is not null)
                {
                    var description = metaData.Description;
                    description = Regex.Replace(description, "<.*?>", String.Empty);

                    track.Description = description;
                    track.AdditionalFields["----:com.apple.iTunes:description"] = track.Description;
                }

                track.Genre = metaData.Categories?.Select(x => x.Name).JoinString("; ") ?? string.Empty;

                var series = metaData.Series?.FirstOrDefault();
                if (series != null)
                {
                    track.AdditionalFields["----:com.apple.iTunes:series"] = series.SeriesName;
                    track.AdditionalFields["----:com.apple.iTunes:series-part"] = series.Sequence;
                }

                track.AdditionalFields["CDEK"] = metaData.Asin;
                track.AdditionalFields["----:com.apple.iTunes:ASIN"] = metaData.Asin;

                track.AdditionalFields["rldt"] = metaData.DatePublished?.ToString("dd-MMM-yyyy", new CultureInfo("en-US")) ?? string.Empty;

                if (!string.IsNullOrEmpty(metaData.Language))
                {
                    // Waiting for feedback on https://github.com/advplyr/audiobookshelf/issues/305
                    track.AdditionalFields["----:com.apple.iTunes:LANGUAGE"] = metaData.Language;
                }
                track.AdditionalFields["----:com.apple.iTunes:DateAdded"] = metaData.DateAdded.ToString("dd-MMM-yyyy", new CultureInfo("en-US"));


                track.Save();
                return true;
            }
            catch (Exception)
            {
                _logger.LogWarning("Tagging failed");
                return false;
            }
        }
    }
}
