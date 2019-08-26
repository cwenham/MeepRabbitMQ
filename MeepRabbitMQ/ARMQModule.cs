using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using RabbitMQ.Client;

using MeepLib;
using MeepLib.MeepLang;
using MeepLib.Messages;

namespace MeepRabbitMQ
{
    public abstract class ARMQModule : AMessageModule
    {
        public const string PluginNamespace = "http://meep.example.com/MeepRabbitMQ/V1";

        /// <summary>
        /// Specify the full queue connection in one URI string, obviates the need to separately specify the User,
        /// Password, Host, Port and VirtualHost since they'd all be encoded here
        /// </summary>
        /// <remarks>E.G.: amqp://user:pass@hostName:port/vhost
        ///
        /// <para>If this has a valid URI, then User, Password, VirtualHost and Host will be ignored even if they're
        /// specified anyway.</para>
        ///
        /// <para>This can be the fastest method, since specifying each part separately will require a seperate
        /// operation to extract their values from each message.</para>
        /// </remarks>
        public DataSelector URI { get; set; }

        /// <summary>
        /// Name of the queue. This also works as the routing key when dequeueing. Defaults to "Meep"
        /// </summary>
        public DataSelector Queue { get; set; } = "Meep";

        /// <summary>
        /// Exchange name
        /// </summary>
        public DataSelector Exchange { get; set; } = "";

		/// <summary>
		/// RabbitMQ Username. Defaults to RabbitMQ's "guest"
		/// </summary>
		public DataSelector User { get; set; } = "guest";

		/// <summary>
		/// RabbitMQ Password. Defaults to RabbitMQ's "guest"
		/// </summary>
		public DataSelector Password { get; set; } = "guest";

        /// <summary>
        /// Specify the Virtual Host. RabbitMQ's default is '/'
        /// </summary>
        public DataSelector VirtualHost { get; set; }

        /// <summary>
        /// Specify the HostName. Defaults to "localhost"
        /// </summary>
        public DataSelector Host { get; set; } = "localhost";

        /// <summary>
        /// Port number. RabbitMQ's default is 5672
        /// </summary>
        public DataSelector Port { get; set; }

        /// <summary>
        /// Collection of long-lived queue connections and models, keyed by URI
        /// </summary>
        internal static ConcurrentDictionary<string, QueueConnection> Connections = new ConcurrentDictionary<string, QueueConnection>();

        internal async Task<QueueConnection> GetConnection(MessageContext context)
        {
            string user = null;
            string pass = null;
            string host = null;
            string virt = null;
            int port = 0;

            string dsUri = null;
            if (URI != null)
                dsUri = await URI.SelectStringAsync(context);
            else
            {
                var userTask = User?.SelectStringAsync(context);
                var passTask = Password?.SelectStringAsync(context);
                var hostTask = Host?.SelectStringAsync(context);
                var portTask = Port?.SelectStringAsync(context);
                var virtTask = VirtualHost?.SelectStringAsync(context);
                var paramTasks = new Task<string>[] { userTask, passTask, hostTask, portTask, virtTask };
                Task.WaitAll(paramTasks.Where(x => x != null).ToArray());

                user = userTask?.Result;
                pass = passTask?.Result;
                host = hostTask?.Result;
                virt = virtTask?.Result;
                if (portTask != null)
                    int.TryParse(portTask.Result, out port);

                dsUri = String.Format("amqp://{0}:{1}@{2}:{3}/{4}",
                    user,
                    pass,
                    host,
                    port,
                    virt);
            }

            if (Connections.ContainsKey(dsUri))
                return Connections[dsUri];
            else
            {
                ConnectionFactory factory = new ConnectionFactory();

                if (URI != null)
                    factory.Uri = new Uri(dsUri);
                else
                {
                    // We've already made a Uri for the sake of having a dictionary key, but we'll pass these into
                    // the factory separatey in case Rabbit's code does any optimisations, so revisit this after
                    // some benchmarking.
                    if (user != null)
                        factory.UserName = user;
                    if (pass != null)
                        factory.Password = pass;
                    if (host != null)
                        factory.HostName = host;
                    if (virt != null)
                        factory.VirtualHost = virt;
                    if (port > 0)
                        factory.Port = port;
                }

                QueueConnection qc = null;
                try
                {
                    qc = new QueueConnection(factory.CreateConnection());
                }
                catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException buex)
                {
                    logger.Error(buex, "Queue Broker (RabbitMQ service) is unreachable. Is there one running at {0}? ({1})", factory.Endpoint.ToString(), buex.Message);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "{0} thrown when attempting to get RabbitMQ connection: {1}", ex.GetType().Name, ex.Message);
                    throw;
                }
                 
                Connections.TryAdd(dsUri, qc);
                return qc;
            }
        }        

        public override void Dispose()
        {
            foreach (var qc in Connections.Values)
                qc.Dispose();
        }
    }

    internal class QueueConnection : IDisposable
    {
        public QueueConnection(IConnection conn)
        {
            this.Connection = conn;
            this.Channel = conn.CreateModel();
        }

        public IConnection Connection { get; set; }

        public IModel Channel { get; set; }

        public void Dispose()
        {
            Channel.Close();
            Connection.Close();
        }
    }
}
