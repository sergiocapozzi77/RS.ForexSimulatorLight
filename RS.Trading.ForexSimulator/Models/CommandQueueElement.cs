using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace RS.Trading.ForexSimulator.Models
{
    internal class CommandQueueElement
    {
        public CommandQueueElement(CommandType command, string arguments): this(command)
        {
            Arguments = arguments;
        }

        public CommandQueueElement(CommandType command)
        {
            Command = command;
            ReqId = Guid.NewGuid().ToString();
            ReqTime = DateTime.Now;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public CommandType Command { get; set; }

        public string Arguments { get; set; }

        public string ReqId { get; }

        public DateTime ReqTime { get; }
    }
}