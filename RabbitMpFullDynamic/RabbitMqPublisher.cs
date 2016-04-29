using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Collections.Concurrent;

namespace RabbitMpFullDynamic
{
    public class RabbitMqPublisher
    {

        public const string Hostname = "localhost";
        private const string Login = "tao";
        private const string Password = "tao";
        private const int Port = 5673;

        private static object lockConnection = new object();
        private static object lockExchage = new object();
        private static object lockQueue = new object();

        ////list of <serviceName, virtual hostname>
        //private static ConcurrentDictionary<string, string> ServiceVhostMap = new ConcurrentDictionary<string,string>();
        
        //list of <virtual hostname, connection objects>
        private static ConcurrentDictionary<string, ConnectionFactory> VhostConnectionMap = new ConcurrentDictionary<string,ConnectionFactory>();

        //list of <serviceName, exchange name>
        private static ConcurrentDictionary<string, string> ServiceExchangeMap = new ConcurrentDictionary<string, string>();
        
        //list of <serviceName, queue name>
        private static ConcurrentDictionary<string, string> ServiceQueueMap = new ConcurrentDictionary<string, string>();
        
        /// <summary>
        /// Load mappingsfrom somewhere for :
        /// mapping between service name, virtual hostname, exchange name, queue name.
        /// </summary>
        private static string GetVirtualHostFromService(string serviceName)
        {
            return "myvirtualhost";
        }

        private TValue UpdateValueFactory<TKey, TValue>(TKey key, TValue value)
        {
            return value;
        }
        
        public static ConnectionFactory GetConnectionFactory(string serviceName)
        {
            var factory = new ConnectionFactory()
            {
                HostName = Hostname,
                UserName = Login,
                Password = Password,
                VirtualHost = GetVirtualHostFromService(serviceName),
                Port = Port
            };

            return factory;
        }

        public string GetExchangeName(IModel model, string serviceName, bool isPublisher)
        {
            if (ServiceExchangeMap.ContainsKey(serviceName)) return ServiceExchangeMap[serviceName];

            lock (lockExchage)
            {
                //generate exchang Name
                var found = false;
                var random = new Random();
                while (!found)
                {
                    var exchangeName = serviceName + (isPublisher ? "PublisherExchange" : "ConsumerExchange") + random.Next(999999999).ToString().PadLeft(9, '0');
                    if (ServiceExchangeMap.Values.Contains(exchangeName)) continue;

                    if (model == null)
                    {
                        throw new ArgumentNullException("model");
                    }

                    try
                    {
                        model.ExchangeDeclare(
                            exchange: exchangeName, 
                            type: "fanout");
                        ServiceExchangeMap.AddOrUpdate(serviceName, exchangeName, UpdateValueFactory);
                        found = true;
                    }
                    catch
                    {
                        throw;
                    }
                }
            }

            return ServiceExchangeMap[serviceName];
        }
            

        public string GetQueueName(IModel model, string serviceName, bool isPublisher)
        {
            if (ServiceQueueMap.ContainsKey(serviceName)) return ServiceQueueMap[serviceName];

            lock (lockQueue)
            {
                //generate exchang Name
                var found = false;
                var random = new Random();
                while (!found)
                {
                    var queueName = serviceName + (isPublisher ? "PublisherQueue" : "ConsumerQueue") + random.Next(999999999).ToString().PadLeft(9, '0');
                    if (ServiceQueueMap.Values.Contains(queueName)) continue;

                    if (model == null)
                    {
                        throw new ArgumentNullException("model");
                    }

                    try
                    {
                        model.QueueDeclare(
                            queue: queueName,
                            durable: false,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);
                        ServiceQueueMap.AddOrUpdate(serviceName, queueName, UpdateValueFactory);
                        found = true;
                    }
                    catch
                    {
                        throw;
                    }
                }
            }

            return ServiceQueueMap[serviceName];
        }

        public MessagePublishEventArgs SentMessage(string serviceName, string message)
        {
            var eventArgs = new MessagePublishEventArgs() { ServiceName = serviceName };
            var factory = GetConnectionFactory(serviceName);
            using(var pubisherConnection = factory.CreateConnection())
            {
                using (var pubisherModel = pubisherConnection.CreateModel())
                {
                    //set source exchange
                    var exchangePublisher = GetExchangeName(pubisherModel, serviceName, true);
                    var queuePublisher = GetQueueName(pubisherModel, serviceName, true);
                    pubisherModel.QueueBind(
                        queue: queuePublisher,
                        exchange: exchangePublisher,
                        routingKey:"");

                    //set destination exchange
                    var exchangeConsumer = GetExchangeName(pubisherModel, serviceName, false);
                    var queueConsumer = GetQueueName(pubisherModel, serviceName, false);
                    pubisherModel.QueueBind(
                        queue: queueConsumer, 
                        exchange: exchangeConsumer,
                        routingKey: "");

                    pubisherModel.ExchangeBind(exchangeConsumer, exchangePublisher, "");

                    var body = Encoding.UTF8.GetBytes(message);
                    pubisherModel.BasicPublish(exchange: exchangePublisher,
                                         routingKey: "",
                                         basicProperties: null,
                                         body: body);

                    eventArgs.ConsumerExchangeName = exchangeConsumer;
                    eventArgs.ConsumerQueueName = queueConsumer;
                    eventArgs.ConsumerVirtualHostName = GetVirtualHostFromService(serviceName);
                }
            }


            return eventArgs;
        }
    }
}
