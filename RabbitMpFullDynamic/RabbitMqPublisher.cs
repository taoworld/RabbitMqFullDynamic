using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Collections.Concurrent;
using EasyNetQ;
using EasyNetQ.Management.Client;
using EasyNetQ.Management.Client.Model;

namespace RabbitMpFullDynamic
{
    public class RabbitMqPublisher
    {

        public const string Hostname = "localhost";
        private const string User = "tao";
        private const string Password = "tao";
        private const int Port = 5673;

        private static object _lockExchage = new object();
        private static object _lockQueue = new object();

        ////list of <serviceName, virtual hostname>
        //private static ConcurrentDictionary<string, string> ServiceVhostMap = new ConcurrentDictionary<string,string>();
        
        //list of <virtual hostname, connection objects>

        private static readonly Dictionary<string, ConnectionFactory> VhostConnectionMap = new Dictionary<string, ConnectionFactory>();

        private static readonly Dictionary<string, string> ServiceVirtualHostPublisherMap = new Dictionary<string, string>();
        //private static readonly Dictionary<string, string> ServiceVirtualHostConsumerMap = new Dictionary<string, string>();

        //list of <serviceName, exchange name>
        private static readonly Dictionary<string, string> ServiceExchangePublisherMap = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> ServiceExchangeConsumerMap = new Dictionary<string, string>();
        
        //list of <serviceName, queue name>
        private static readonly Dictionary<string, string> ServiceQueuePublisherMap = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> ServiceQueueConsumerMap = new Dictionary<string, string>();
        
        /// <summary>
        /// Load mappingsfrom somewhere for :
        /// mapping between service name, virtual hostname, exchange name, queue name.
        /// </summary>
        private string GetVirtualHostFromService(string serviceName, bool isForPublisher)
        {
            //var targetMap = isForPublisher ? ServiceVirtualHostPublisherMap : ServiceVirtualHostConsumerMap;
            var targetMap = ServiceVirtualHostPublisherMap;
            if (!targetMap.ContainsKey(serviceName))
            {
                CreateVirtualHost(serviceName);
            }

            return targetMap[serviceName];
        }

        private static ManagementClient _managementClient;
        private readonly object _managementClientLock= new object();

        private ManagementClient GetManagementClient()
        {
            if (_managementClient == null)
            {
                lock (_managementClientLock)
                {
                    if (_managementClient == null)
                    {
                        _managementClient = new ManagementClient(Hostname, User, Password);
                    }
                }
            }
            
            return _managementClient;
        }

        private void CreateVirtualHost(string serviceName)
        {
            bool isExists;
            var managementClient = GetManagementClient();

            var vhPublisherName = GenerateComponentName(serviceName, ServiceVirtualHostPublisherMap, "VirtualHostPublisher", out isExists);
            if (!isExists)
            {
                var vhHost = managementClient.CreateVirtualHost(vhPublisherName);
                var user = managementClient.GetUser("tao");
                managementClient.CreatePermission(new PermissionInfo(user, vhHost));
            }

            //var vhConsumerName = GenerateComponentName(serviceName, ServiceVirtualHostConsumerMap, "VirtualHostConsumer", out isExists);
            //if (!isExists)
            //{
            //    var vhHost = managementClient.CreateVirtualHost(vhConsumerName);
            //    var user = managementClient.GetUser("tao");
            //    managementClient.CreatePermission(new PermissionInfo(user, vhHost));
                
            //}
        }

        private string GenerateComponentName(string serviceName, Dictionary<string, string> existingNames, string componentKey, out bool isExists)
        {
            if (existingNames.ContainsKey(serviceName))
            {
                isExists = true;
                return existingNames[serviceName];
            }

            isExists = false;
            var componentName = string.Empty;
            var random = new Random();
            var found = false;
            while (!found)
            {
                componentName = serviceName + componentKey + random.Next(999999999).ToString().PadLeft(9, '0');
                if (!existingNames.ContainsKey(componentName))
                {
                    existingNames.Add(serviceName, componentName);
                    found = true;
                }
            }

            return componentName;
        }
        
        public ConnectionFactory GetConnectionFactory(string serviceName, bool isPublisher)
        {
            var factory = new ConnectionFactory()
            {
                HostName = Hostname,
                UserName = User,
                Password = Password,
                VirtualHost = GetVirtualHostFromService(serviceName, isPublisher),
                Port = Port
            };

            return factory;
        }

        public string GetExchangeName(IModel model, string serviceName, bool isPublisher)
        {
            bool isExists;
            var exchangeName = GenerateComponentName(serviceName,
                isPublisher ? ServiceExchangePublisherMap : ServiceExchangeConsumerMap,
                isPublisher ? "PublisherExchage" : "ConsumerExchange", out isExists);
            if (!isExists)
            {
                var managementClient = GetManagementClient();

                var vhost = managementClient.GetVhost(GetVirtualHostFromService(serviceName, isPublisher));
                var exchangeInfo = new ExchangeInfo(exchangeName, "fanout");
                managementClient.CreateExchange(exchangeInfo, vhost);
            }

            return exchangeName;
        }


        public string GetQueueName(IModel model, string serviceName, bool isPublisher)
        {
            bool isExists;
            var queueName = GenerateComponentName(serviceName, 
                isPublisher ? ServiceQueuePublisherMap : ServiceQueueConsumerMap,
                isPublisher ? "PublisherQueue" : "ConsumerQueue", out isExists);
            if (!isExists)
            {
                var managementClient = GetManagementClient();

                var vhost = managementClient.GetVhost(GetVirtualHostFromService(serviceName, isPublisher));
                var queueInfo = new QueueInfo(queueName);
                managementClient.CreateQueue(queueInfo, vhost);
            }

            return queueName;
        }

        public MessagePublishEventArgs SentMessage(string serviceName, string message)
        {
            var eventArgs = new MessagePublishEventArgs() { ServiceName = serviceName };
            var factoryPublisher = GetConnectionFactory(serviceName, true);
            var factoryConsumer = GetConnectionFactory(serviceName, false);
            using (var pubisherConnection = factoryPublisher.CreateConnection())
            using (var consumerConnection = factoryConsumer.CreateConnection())
            {
                using (var pubisherModel = pubisherConnection.CreateModel())
                using (var consumerModel = consumerConnection.CreateModel())
                {
                    //set source exchange
                    var exchangePublisher = GetExchangeName(pubisherModel, serviceName, true);
                    var queuePublisher = GetQueueName(pubisherModel, serviceName, true);
                    pubisherModel.QueueBind(
                        queue: queuePublisher,
                        exchange: exchangePublisher,
                        routingKey:"");

                    //set destination exchange
                    var exchangeConsumer = GetExchangeName(consumerModel, serviceName, false);
                    var queueConsumer = GetQueueName(consumerModel, serviceName, false);
                    consumerModel.QueueBind(
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
                    eventArgs.ConsumerVirtualHostName = GetVirtualHostFromService(serviceName, false);
                }
            }
            
            return eventArgs;
        }
    }
}
