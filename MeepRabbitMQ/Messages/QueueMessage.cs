using System;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using RabbitMQ.Client;

using MeepLib.Messages;

namespace MeepRabbitMQ.Messages
{
    /// <summary>
    /// A special type of Batch message encapsulating messages sent over a queue
    /// </summary>
    /// <remarks>Maintains the metadata concerning the queue delivery and one or more messages discovered in the queue
    /// delivery.</remarks>
    [DataContract]
    public class QueueMessage : Batch, IAcknowledgableMessage
    {
        [XmlIgnore]
        public IModel Channel { get; set; }

        [DataMember]
        public ulong DeliveryTag { get; set; }

        public async ValueTask Acknowledge()
        {
            if (Channel != null)
                await Task.Run(() =>
                {
                    Channel.BasicAck(DeliveryTag, true);
                });            
        }

        public async ValueTask Decline()
        {
            if (Channel != null)
                await Task.Run(() =>
                {
                    Channel.BasicNack(DeliveryTag, true, true);
                });
        }
    }
}
