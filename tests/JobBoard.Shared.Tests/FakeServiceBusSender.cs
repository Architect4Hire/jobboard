using Azure.Messaging.ServiceBus;

namespace JobBoard.Shared.Tests;

/// <summary>
/// A hand-rolled <see cref="ServiceBusSender"/> (the SDK type has a protected ctor and virtual send) that records
/// what the relay sent — or, when <c>throwOnSend</c> is set, fails every send so the relay's retry path can be
/// exercised without a broker.
/// </summary>
public sealed class FakeServiceBusSender : ServiceBusSender
{
    private readonly bool _throwOnSend;

    public FakeServiceBusSender(bool throwOnSend = false) => _throwOnSend = throwOnSend;

    public List<ServiceBusMessage> Sent { get; } = [];

    public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
    {
        if (_throwOnSend)
        {
            throw new ServiceBusException("Simulated send failure.", ServiceBusFailureReason.ServiceCommunicationProblem);
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}
