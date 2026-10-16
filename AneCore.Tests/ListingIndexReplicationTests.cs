using System.Reflection;
using System.Text.Json;
using Coflnet.Ane.Opensearch;
using Microsoft.Extensions.Logging.Abstractions;
using OpenSearch.Client;
using OpenSearch.Net;

namespace AneCore.Tests;

[TestFixture]
public class ListingIndexReplicationTests
{
    [TestCase(false, "template")]
    [TestCase(false, "bootstrap")]
    [TestCase(false, "rollover")]
    [TestCase(true, "template")]
    [TestCase(true, "bootstrap")]
    [TestCase(true, "rollover")]
    public void IndexCreationPaths_KeepAnOffNodeReplica(bool samples, string operation)
    {
        OpenSearchIndexBase index = samples
            ? new ListingSampleIndex(NullLogger<ListingSampleIndex>.Instance, null!)
            : new ListingIndex(NullLogger<ListingIndex>.Instance, null!);
        object Invoke(string name, params object[] args) => index.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(index, args)!;

        object request = operation switch
        {
            "template" => ((Func<PutIndexTemplateDescriptor, IPutIndexTemplateRequest>)Invoke("IndexTemplateFunc"))(
                new PutIndexTemplateDescriptor("test-template")),
            "bootstrap" => Invoke("ConfigureNewIndex", new CreateIndexDescriptor("test-index")),
            "rollover" => Invoke("ConfigureRollover", new RolloverIndexDescriptor("test-alias")),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        var client = new OpenSearchClient(new ConnectionSettings(new InMemoryConnection()));
        using var body = JsonDocument.Parse(client.RequestResponseSerializer.SerializeToString(request));
        var settings = body.RootElement.GetProperty("settings");
        Assert.That(settings.GetProperty("index.number_of_replicas").GetInt32(), Is.EqualTo(1));
        Assert.That(settings.GetProperty("index.number_of_shards").GetInt32(), Is.EqualTo(1));
        Assert.That(settings.GetProperty("plugins.index_state_management.rollover_alias").GetString(),
            Is.EqualTo(index.IndexName()));
    }
}
