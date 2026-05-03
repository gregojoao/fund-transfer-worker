using FundTransfer.Domain.Bus.Consumers;
using FundTransfer.Domain.Commands;
using FundTransfer.Domain.Handlers;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System.Text;
using System.Text.Json;

namespace FundTransfer.Infra.RabbitMq.Consumers
{
    public class PendingTransferConsumer : IBusConsumer
    {
        private readonly FundTransferHandler _handler;
        private readonly IConfiguration _configuration;

        public PendingTransferConsumer(FundTransferHandler handler, IConfiguration configuration)
        {
            _handler = handler;
            _configuration = configuration;
        }

        public async Task ReceiveAsync()
        {
            try
            {
                var factory = new ConnectionFactory() { HostName = GetHostName() };
                var connection = await factory.CreateConnectionAsync();
                var channel = await connection.CreateChannelAsync();
                await channel.QueueDeclareAsync(queue: PendingTransferQueue(), durable: true, exclusive: false, autoDelete: false, arguments: null);
                await RunWorkerAsync(channel);
                Log.Information($"[Pending Transfer Consumer] - Initiated");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "");
            }
        }

        private async Task RunWorkerAsync(IChannel channel)
        {
            await channel.BasicQosAsync(0, 5, false);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Guid transactionId = default;
                try
                {
                    var transferCommand = JsonSerializer.Deserialize<PendingTransferCommand>(message);
                    transactionId = transferCommand.TransactionId;
                    var commandResult = await _handler.Handle(transferCommand, default);
                    if (!commandResult.Sucess)
                    {
                        await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                        Log.Information($"[{transactionId}] - Nack: {message}");
                    }
                    else
                    {
                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                        Log.Information($"[{transactionId}] - Ack: {message}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "");
                    Log.Warning($"[{transactionId}] - Nack: {message}");
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };
            await channel.BasicConsumeAsync(queue: PendingTransferQueue(), autoAck: false, consumer: consumer);
        }

        private string PendingTransferQueue() =>
            _configuration["RabbitMq:PendingTransferQueue"];

        private string GetHostName() =>
            _configuration["RabbitMq:Url"];
    }
}