using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Threading;


namespace RabbitMpFullDynamic
{
    public class RabbitMqConsumer
    {
        public const string Hostname = "localhost";
        private const string Login = "tao";
        private const string Password = "tao";
        private const int Port = 5673;

        public bool KeepConsumer { get; set; }

        public void Subscribe(string serviceName, string virtualHost, string queueName)
        {
            var factory = new ConnectionFactory()
                                        {
                                            HostName = Hostname,
                                            UserName = Login,
                                            Password = Password,
                                            VirtualHost = virtualHost,
                                            Port = Port
                                        };
            using (var consumerConnection = factory.CreateConnection())
            {
                using (var consumerChannel = consumerConnection.CreateModel())
                {
                    var consumer = new EventingBasicConsumer(consumerChannel);
                    consumer.Received += (model, ea) =>
                    {
                        var body = ea.Body;
                        var message = Encoding.UTF8.GetString(body);
                        Console.WriteLine(" Sercvice {0} Received {1} from queue {2}", serviceName, message, queueName);
                    };

                    while (KeepConsumer)
                    {
                        consumerChannel.BasicConsume(queue: queueName,
                                                 noAck: true,
                                                 consumer: consumer);
                        Thread.Sleep(1000);
                    }
                }
            }
        }
    }
}
