using System.Collections.Generic;
using Newtonsoft.Json;

namespace Unity.Cn.Multiverse.Model
{
    [JsonObject]
    public class ServerError
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "reservedHttpStatusCode")]
        public int HttpStatusCode { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "message")]
        public string Message { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "code")]
        public int Code { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "details")]
        private List<ErrorDetail> Details { get; set; }

        public override string ToString()
        {
            return $"HttpCode:{HttpStatusCode}, Message:{Message}";
        }
    }

    [JsonObject]
    public class ErrorDetail
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "field")]
        public string Field { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "reason")]
        public string Reason { get; set; }
    }

    public class BackfillAPIError
    {
        public readonly string Message;

        public BackfillAPIError(string msg)
        {
            Message = msg;
        }

        public override string ToString()
        {
            return Message;
        }
    }
}