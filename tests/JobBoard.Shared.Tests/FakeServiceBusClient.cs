using Azure.Messaging.ServiceBus;

namespace JobBoard.Shared.Tests;

/// <summary>
/// A hand-rolled <see cref="ServiceBusClient"/> whose <see cref="CreateSender(string)"/> returns whatever the
/// supplied factory yields for a destination — enough to drive <see cref="Shared.Messaging.OutboxRelay"/> in tests
/// without a real namespace or emulator.
/// </summary>
public sealed class FakeServiceBusClient : ServiceBusClient
{
    private readonly Func<string, ServiceBusSender> _senderFactory;

    public FakeServiceBusClient(Func<string, ServiceBusSender> senderFactory) => _senderFactory = senderFactory;

    public override ServiceBusSender CreateSender(string queueOrTopicName) => _senderFactory(queueOrTopicName);
}
