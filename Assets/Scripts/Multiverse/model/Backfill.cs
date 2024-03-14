using System.Collections.Generic;
using Newtonsoft.Json;

namespace Unity.Cn.Multiverse.Model
{
    [JsonObject]
    public class Backfill
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "appId")]
        public string AppId { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "configId")]
        public string ConfigId { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "ip")]
        public string Ip { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "gamePorts")]
        public string GamePorts { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "roomId")]
        public string RoomId { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "matchProperties")]
        public string MatchProperties { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "regionId")]
        public string RegionId { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "createdAt")]
        public long CreatedAt { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "updatedAt")]
        public long UpdatedAt { get; set; }
    }
    
    [JsonObject]
    public class Team
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "teamDefinitionName")]
        public string TeamDefinitionName { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "teamName")]
        public string TeamName { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "playerIds")]
        public List<string> PlayerIds { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "tickets")]
        public List<Ticket> Tickets { get; set; }
    }

    [JsonObject]
    public class Ticket
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "players")]
        public List<Player> Players { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "createdAt")]
        public long CreatedAt { get; set; }
    }

    [JsonObject]
    public class Player
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "attributes")]
        public Dictionary<string, string> Attributes { get; set; }
    }
}

