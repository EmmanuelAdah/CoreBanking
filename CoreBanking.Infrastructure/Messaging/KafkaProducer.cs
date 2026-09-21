using Confluent.Kafka;
using CoreBanking.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoreBanking.Infrastructure.Messaging;

public class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IConfiguration config, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        var bootstrap = config["KAFKA_BOOTSTRAP_SERVERS"] ?? "localhost:9092";

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrap,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task PublishAsync(string topic, string key, string message, CancellationToken ct = default)
    {
        try
        {
            var result = await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = message
            }, ct);

            _logger.LogInformation("Published to {Topic} partition {Partition} offset {Offset}",
                result.Topic, result.Partition, result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish to Kafka topic {Topic}", topic);
            throw;
        }
    }

    public void Dispose() => _producer.Dispose();
}