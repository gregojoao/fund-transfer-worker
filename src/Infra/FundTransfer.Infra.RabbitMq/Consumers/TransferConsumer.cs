using FundTransfer.Domain.Bus.Consumers;
using FundTransfer.Domain.Commands;
using FundTransfer.Domain.Entities;
using FundTransfer.Domain.Handlers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System.Text;

namespace FundTransfer.Infra.RabbitMq.Consumers
{
    public class TransferConsumer : IBusConsumer
    {
        private readonly FundTransferHandler _handler;
        private readonly IConfiguration _configuration;

        public TransferConsumer(FundTransferHandler handler, IConfiguration configuration)
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
                await channel.QueueDeclareAsync(queue: GetTransferQueueName(), durable: true, exclusive: false, autoDelete: false, arguments: null);
                await RunWorkerAsync(channel);
                await RunWorkerAsync(channel);
                Log.Information($"[Transfer Consumer] - Initiated");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "");
            }
        }

        private async Task RunWorkerAsync(IChannel channel)
        {
            await channel.BasicQosAsync(0, 10, false);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Guid transactionId = default;
                try
                {
                    var transferCommand = JsonConvert.DeserializeObject<TransferCommand>(message);
                    transactionId = transferCommand.TransactionId;
                    var commandResult = await _handler.Handle(transferCommand, default);
                    var transfer = (Transfer)commandResult.Data;
                    if (!commandResult.Sucess)
                    {
                        await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                        Log.Debug($"[{transactionId}] - {JsonConvert.SerializeObject(transfer)}");
                        Log.Information($"[{transactionId}] - Nack: {message}");
                    }
                    else
                    {
                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                        Log.Debug($"[{transactionId}] - {JsonConvert.SerializeObject(transfer)}");
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
            await channel.BasicConsumeAsync(queue: GetTransferQueueName(), autoAck: false, consumer: consumer);
        }

        private string GetTransferQueueName() =>
            _configuration["RabbitMq:TransferQueue"];

        private string GetHostName() =>
            _configuration["RabbitMq:Url"];
    }
}
