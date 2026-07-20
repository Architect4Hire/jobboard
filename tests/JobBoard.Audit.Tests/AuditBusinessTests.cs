using JobBoard.Audit.Core.Business;
using JobBoard.Audit.Tests.Fakes;
using Xunit;

namespace JobBoard.Audit.Tests;

/// <summary>Business maps the event to one entry and keys the append on the event's id (the Service Bus
/// MessageId), so the inbox dedupes a redelivery.</summary>
public sealed class AuditBusinessTests
{
    [Fact]
    public async Task RecordAsync_AppendsMappedEntry_KeyedByEventId()
    {
        var dataLayer = new FakeAuditDataLayer();
        var business = new AuditBusiness(dataLayer);
        var @event = TestData.ApplicationSubmitted();

        await business.RecordAsync(@event);

        Assert.NotNull(dataLayer.Appended);
        Assert.Equal(@event.Id, dataLayer.Appended!.Id);
        Assert.Equal("ApplicationSubmitted", dataLayer.Appended.EventType);
        Assert.Equal(@event.ApplicationId, dataLayer.Appended.SubjectId);
        Assert.Equal(@event.Id, dataLayer.MessageId); // dedupe key = the event id
    }
}
