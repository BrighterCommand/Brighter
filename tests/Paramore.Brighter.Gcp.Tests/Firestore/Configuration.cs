using System;
using Grpc.Core;
using Paramore.Brighter.Firestore;
using Paramore.Brighter.Gcp.Tests.Helper;

namespace Paramore.Brighter.Gcp.Tests.Firestore;

public static class Configuration
{
    public static FirestoreConfiguration CreateInbox()
    {
        return new FirestoreConfiguration(GatewayFactory.GetProjectId(), DatabaseName)
        {
            Credential = GatewayFactory.GetCredential(),
            Inbox = new FirestoreCollection
            {
                Name = InboxCollection,
                Ttl = TimeSpan.FromMinutes(5)
            }
        };
    }
    
    public static FirestoreConfiguration CreateOutbox(string? collection = null)
    {
        return new FirestoreConfiguration(GatewayFactory.GetProjectId(), DatabaseName)
        {
            Credential = GatewayFactory.GetCredential(),
            Configure = ConfigureEmulator,
            Outbox = new FirestoreCollection
            {
                Name = collection ?? OutboxCollection,
                Ttl = TimeSpan.FromMinutes(5)
            } 
        };
    }
    
    public static FirestoreConfiguration CreateLocking(string collection)
    {
        return new FirestoreConfiguration(GatewayFactory.GetProjectId(), DatabaseName)
        {
            Credential = GatewayFactory.GetCredential(),
            Locking = new FirestoreCollection
            {
                Name = LockingCollection,
                Ttl = TimeSpan.FromMinutes(5)
            } 
        };
    }

    private static void ConfigureEmulator(Google.Cloud.Firestore.V1.FirestoreClientBuilder builder)
    {
        var emulatorHost = Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST");
        if (string.IsNullOrEmpty(emulatorHost))
        {
            return;
        }

        builder.Credential = null;
        builder.Endpoint = emulatorHost;
        builder.ChannelCredentials = ChannelCredentials.Insecure;
    }

    private const string DatabaseName = "brighter-firestore-database";
    private const string InboxCollection = "inbox";
    private const string OutboxCollection = "outbox";
    private const string LockingCollection = "locking";
}
