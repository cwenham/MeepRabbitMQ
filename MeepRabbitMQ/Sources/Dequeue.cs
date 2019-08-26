using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive.Linq;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using MeepLib;
using MeepLib.MeepLang;
using MeepLib.Messages;

using MeepRabbitMQ.Messages;

namespace MeepRabbitMQ.Sources
{
    [MeepNamespace(ARMQModule.PluginNamespace)]
    public class Dequeue : ARMQModule
    {
        public override IObservable<Message> Pipeline
        {
            get
            {
                if (_pipeline is null)
                {
                    MessageContext context = new MessageContext(null, this);
                    var connection = GetConnection(context).GetAwaiter().GetResult();

                    string queue = null;
                    if (Queue != null)
                        queue = Queue?.SelectString(context);

                    connection.Channel.QueueDeclare(queue: queue,
                                                    durable: false,
                                                    exclusive: false,
                                                    autoDelete: false,
                                                    arguments: null);

                    var consumer = new EventingBasicConsumer(connection.Channel);
                    _pipeline = from qm in Observable.FromEventPattern<BasicDeliverEventArgs>(
                                                x => consumer.Received += x,
                                                x => consumer.Received -= x)
                                where qm.EventArgs.Body != null && qm.EventArgs.Body.Length > 0
                                let deliveredMsg = qm.EventArgs.Body.DeserialiseOrBust()
                                where deliveredMsg != null
                                select new QueueMessage
                                {
                                    Messages = new List<Message> { deliveredMsg },
                                    Channel = connection.Channel,
                                    DeliveryTag = qm.EventArgs.DeliveryTag
                                };
                    connection.Channel.BasicConsume(queue: queue,
                                                    autoAck: true,
                                                    consumer: consumer);
                }

                return _pipeline;
            }
        }
        private IObservable<Message> _pipeline;
    }
}
