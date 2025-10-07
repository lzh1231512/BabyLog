using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BabyLog.Models
{
    public class Event
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Media Media { get; set; }
        public string Date { get; set; }
        public string Location { get; set; }
    }

    public class Media
    {
        public List<MediaItem> Images { get; set; }
        public List<MediaItem> Videos { get; set; }
        public List<MediaItem> Audios { get; set; }
    }

    public class MediaItem
    {
        public string FileName { get; set; }
        public string Desc { get; set; }
    }

    public class TimelineGroup
    {
        public string Age { get; set; }
        public string Date { get; set; }
        public List<Event> Events { get; set; }
    }
}