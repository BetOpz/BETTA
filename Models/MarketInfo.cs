using System;
using Newtonsoft.Json;

namespace BETTA.Models
{
    public class MarketInfo
    {
        [JsonProperty("market_id")]
        public string MarketId { get; set; }

        [JsonProperty("market_name")]
        public string MarketName { get; set; }

        [JsonProperty("start_time")]
        public DateTime StartTime { get; set; }

        [JsonProperty("total_matched")]
        public decimal TotalMatched { get; set; }
    }
}
