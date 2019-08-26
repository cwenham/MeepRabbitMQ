using System;
using System.Linq;
using System.IO;

using RabbitMQ.Client;

using MeepLib;
using MeepLib.MeepLang;
using MeepLib.Messages;
using System.Threading.Tasks;

namespace MeepRabbitMQ.Outputs
{
    [MeepNamespace(ARMQModule.PluginNamespace)]
    public class Enqueue : ARMQModule
    {
        /// <summary>
        /// How to get the data to be enqueued from each message, leave null to serialise the whole message
        /// </summary>
        public DataSelector From { get; set; }

        public async override Task<Message> HandleMessage(Message msg)
        {
            MessageContext context = new MessageContext(msg, this);
            var connection = await GetConnection(context);

            if (connection is null)
            {
                logger.Warn("Could not create RabbitMQ connection");
                return null;
            }

            string exchange = null;
            if (Exchange != null)
                exchange = await Exchange?.SelectStringAsync(context);
            string queue = null;
            if (Queue != null)
                queue = await Queue?.SelectStringAsync(context);

            connection.Channel.QueueDeclare(queue: queue,                                 durable: false,                                 exclusive: false,                                 autoDelete: false,                                 arguments: null);

            byte[] payload = null;
            if (From != null)
                payload = (await From.SelectROMByteAsync(context)).ToArray();
            else
            {
                MemoryStream utf8Stream = new MemoryStream();
                await msg.SerialiseWithTypePrefixAsync(utf8Stream);
                utf8Stream.Position = 0;
                payload = new byte[utf8Stream.Length];
                utf8Stream.Read(payload, 0, payload.Length);
            }

            if (payload != null)
                connection.Channel.BasicPublish(exchange, queue, false, null, payload);

            return msg;
        }
    }
}
