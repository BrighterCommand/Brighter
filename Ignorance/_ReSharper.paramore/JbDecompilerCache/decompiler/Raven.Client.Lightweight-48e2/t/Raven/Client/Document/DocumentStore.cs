// Type: Raven.Client.Document.DocumentStore
// Assembly: Raven.Client.Lightweight, Version=1.0.0.0, Culture=neutral, PublicKeyToken=37f41c7f99471593
// Assembly location: C:\users\sundance\work\paramore\ignorance\Lib\Raven.Client.Lightweight.dll

using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Client;
using Raven.Client.Connection;
using Raven.Client.Connection.Async;
using Raven.Client.Connection.Profiling;
using Raven.Client.Document.Async;
using Raven.Client.Extensions;
using Raven.Client.Listeners;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading;

namespace Raven.Client.Document
{
  public class DocumentStore : IDocumentStore, IDisposalNotification, IDisposable
  {
    private readonly HttpJsonRequestFactory jsonRequestFactory = new HttpJsonRequestFactory();
    private DocumentSessionListeners listeners = new DocumentSessionListeners();
    private ICredentials credentials = (ICredentials) CredentialCache.DefaultNetworkCredentials;
    private readonly ProfilingContext profilingContext = new ProfilingContext();
    private readonly object lastEtagLocker = new object();
    [ThreadStatic]
    protected static Guid? currentSessionId;
    protected Func<IDatabaseCommands> databaseCommandsGenerator;
    private Func<IAsyncDatabaseCommands> asyncDatabaseCommandsGenerator;
    private string identifier;
    private string connectionStringName;
    private Action<InMemoryDocumentSessionOperations> SessionCreatedInternal;
    private volatile DocumentStore.EtagHolder lastEtag;
    private EventHandler AfterDispose;

    public NameValueCollection SharedOperationsHeaders { get; private set; }

    public HttpJsonRequestFactory JsonRequestFactory
    {
      get
      {
        return this.jsonRequestFactory;
      }
    }

    public IDatabaseCommands DatabaseCommands
    {
      get
      {
        if (this.databaseCommandsGenerator == null)
          throw new InvalidOperationException("You cannot open a session or access the database commands before initializing the document store. Did you forget calling Initialize()?");
        IDatabaseCommands databaseCommands = this.databaseCommandsGenerator();
        foreach (string name in (NameObjectCollectionBase) this.SharedOperationsHeaders)
        {
          string[] values = this.SharedOperationsHeaders.GetValues(name);
          if (values != null)
          {
            foreach (string str in values)
              databaseCommands.OperationsHeaders[name] = str;
          }
        }
        return databaseCommands;
      }
    }

    public IAsyncDatabaseCommands AsyncDatabaseCommands
    {
      get
      {
        if (this.asyncDatabaseCommandsGenerator == null)
          return (IAsyncDatabaseCommands) null;
        else
          return this.asyncDatabaseCommandsGenerator();
      }
    }

    public ICredentials Credentials
    {
      get
      {
        return this.credentials;
      }
      set
      {
        this.credentials = value;
      }
    }

    public virtual string Identifier
    {
      get
      {
        if (this.identifier != null)
          return this.identifier;
        if (this.Url == null)
          return (string) null;
        if (this.DefaultDatabase != null)
          return this.Url + " (DB: " + this.DefaultDatabase + ")";
        else
          return this.Url;
      }
      set
      {
        this.identifier = value;
      }
    }

    public string ConnectionStringName
    {
      get
      {
        return this.connectionStringName;
      }
      set
      {
        this.connectionStringName = value;
        this.SetConnectionStringSettings(this.GetConnectionStringOptions());
      }
    }

    public bool EnlistInDistributedTransactions { get; set; }

    public string Url { get; set; }

    public string DefaultDatabase { get; set; }

    public DocumentConvention Conventions { get; set; }

    public Guid ResourceManagerId { get; set; }

    public bool WasDisposed { get; private set; }

    public event Action<InMemoryDocumentSessionOperations> SessionCreatedInternal
    {
      add
      {
        Action<InMemoryDocumentSessionOperations> action = this.SessionCreatedInternal;
        Action<InMemoryDocumentSessionOperations> comparand;
        do
        {
          comparand = action;
          action = Interlocked.CompareExchange<Action<InMemoryDocumentSessionOperations>>(ref this.SessionCreatedInternal, comparand + value, comparand);
        }
        while (action != comparand);
      }
      remove
      {
        Action<InMemoryDocumentSessionOperations> action = this.SessionCreatedInternal;
        Action<InMemoryDocumentSessionOperations> comparand;
        do
        {
          comparand = action;
          action = Interlocked.CompareExchange<Action<InMemoryDocumentSessionOperations>>(ref this.SessionCreatedInternal, comparand - value, comparand);
        }
        while (action != comparand);
      }
    }

    public event EventHandler AfterDispose
    {
      add
      {
        EventHandler eventHandler = this.AfterDispose;
        EventHandler comparand;
        do
        {
          comparand = eventHandler;
          eventHandler = Interlocked.CompareExchange<EventHandler>(ref this.AfterDispose, comparand + value, comparand);
        }
        while (eventHandler != comparand);
      }
      remove
      {
        EventHandler eventHandler = this.AfterDispose;
        EventHandler comparand;
        do
        {
          comparand = eventHandler;
          eventHandler = Interlocked.CompareExchange<EventHandler>(ref this.AfterDispose, comparand - value, comparand);
        }
        while (eventHandler != comparand);
      }
    }

    public DocumentStore()
    {
      this.ResourceManagerId = new Guid("E749BAA6-6F76-4EEF-A069-40A4378954F8");
      this.EnlistInDistributedTransactions = true;
      this.SharedOperationsHeaders = new NameValueCollection();
      this.Conventions = new DocumentConvention();
    }

    public void ParseConnectionString(string connString)
    {
      ConnectionStringParser<RavenConnectionStringOptions> connectionStringParser = ConnectionStringParser<RavenConnectionStringOptions>.FromConnectionString(connString);
      connectionStringParser.Parse();
      this.SetConnectionStringSettings(connectionStringParser.ConnectionStringOptions);
    }

    protected virtual void SetConnectionStringSettings(RavenConnectionStringOptions options)
    {
      if (options.ResourceManagerId != Guid.Empty)
        this.ResourceManagerId = options.ResourceManagerId;
      if (options.Credentials != null)
        this.Credentials = (ICredentials) options.Credentials;
      if (!string.IsNullOrEmpty(options.Url))
        this.Url = options.Url;
      if (!string.IsNullOrEmpty(options.DefaultDatabase))
        this.DefaultDatabase = options.DefaultDatabase;
      this.EnlistInDistributedTransactions = options.EnlistInDistributedTransactions;
    }

    protected virtual RavenConnectionStringOptions GetConnectionStringOptions()
    {
      ConnectionStringParser<RavenConnectionStringOptions> connectionStringParser = ConnectionStringParser<RavenConnectionStringOptions>.FromConnectionStringName(this.connectionStringName);
      connectionStringParser.Parse();
      return connectionStringParser.ConnectionStringOptions;
    }

    public virtual void Dispose()
    {
      this.jsonRequestFactory.Dispose();
      this.WasDisposed = true;
      EventHandler eventHandler = this.AfterDispose;
      if (eventHandler == null)
        return;
      eventHandler((object) this, EventArgs.Empty);
    }

    public IDocumentSession OpenSession(ICredentials credentialsForSession)
    {
      this.EnsureNotClosed();
      Guid id = Guid.NewGuid();
      DocumentStore.currentSessionId = new Guid?(id);
      try
      {
        DocumentSession documentSession = new DocumentSession(this, this.listeners, id, this.DatabaseCommands.With(credentialsForSession), this.AsyncDatabaseCommands.With(credentialsForSession));
        this.AfterSessionCreated((InMemoryDocumentSessionOperations) documentSession);
        return (IDocumentSession) documentSession;
      }
      finally
      {
        DocumentStore.currentSessionId = new Guid?();
      }
    }

    public IDocumentSession OpenSession()
    {
      this.EnsureNotClosed();
      Guid id = Guid.NewGuid();
      DocumentStore.currentSessionId = new Guid?(id);
      try
      {
        DocumentSession documentSession = new DocumentSession(this, this.listeners, id, this.DatabaseCommands, this.AsyncDatabaseCommands);
        this.AfterSessionCreated((InMemoryDocumentSessionOperations) documentSession);
        return (IDocumentSession) documentSession;
      }
      finally
      {
        DocumentStore.currentSessionId = new Guid?();
      }
    }

    public IDocumentSession OpenSession(string database)
    {
      this.EnsureNotClosed();
      Guid id = Guid.NewGuid();
      DocumentStore.currentSessionId = new Guid?(id);
      try
      {
        DocumentSession documentSession = new DocumentSession(this, this.listeners, id, this.DatabaseCommands.ForDatabase(database), this.AsyncDatabaseCommands.ForDatabase(database));
        this.AfterSessionCreated((InMemoryDocumentSessionOperations) documentSession);
        return (IDocumentSession) documentSession;
      }
      finally
      {
        DocumentStore.currentSessionId = new Guid?();
      }
    }

    public IDocumentSession OpenSession(string database, ICredentials credentialsForSession)
    {
      this.EnsureNotClosed();
      Guid id = Guid.NewGuid();
      DocumentStore.currentSessionId = new Guid?(id);
      try
      {
        DocumentSession documentSession = new DocumentSession(this, this.listeners, id, this.DatabaseCommands.ForDatabase(database).With(credentialsForSession), this.AsyncDatabaseCommands.ForDatabase(database).With(credentialsForSession));
        this.AfterSessionCreated((InMemoryDocumentSessionOperations) documentSession);
        return (IDocumentSession) documentSession;
      }
      finally
      {
        DocumentStore.currentSessionId = new Guid?();
      }
    }

    public DocumentStore RegisterListener(IDocumentStoreListener documentStoreListener)
    {
      this.listeners.StoreListeners = Enumerable.ToArray<IDocumentStoreListener>(Enumerable.Concat<IDocumentStoreListener>((IEnumerable<IDocumentStoreListener>) this.listeners.StoreListeners, (IEnumerable<IDocumentStoreListener>) new IDocumentStoreListener[1]
      {
        documentStoreListener
      }));
      return this;
    }

    private void AfterSessionCreated(InMemoryDocumentSessionOperations session)
    {
      Action<InMemoryDocumentSessionOperations> action = this.SessionCreatedInternal;
      if (action == null)
        return;
      action(session);
    }

    public ProfilingInformation GetProfilingInformationFor(Guid id)
    {
      return this.profilingContext.TryGet(id);
    }

    public IDocumentStore Initialize()
    {
      try
      {
        if (!this.Conventions.DisableProfiling)
          this.jsonRequestFactory.LogRequest += new EventHandler<RequestResultArgs>(this.profilingContext.RecordAction);
        this.InitializeInternal();
        if (this.Conventions.DocumentKeyGenerator == null)
        {
          MultiTypeHiLoKeyGenerator generator = new MultiTypeHiLoKeyGenerator((IDocumentStore) this, 1024);
          this.Conventions.DocumentKeyGenerator = (Func<object, string>) (entity => generator.GenerateDocumentKey(this.Conventions, entity));
        }
      }
      catch (Exception ex)
      {
        this.Dispose();
        throw;
      }
      if (!string.IsNullOrEmpty(this.DefaultDatabase))
        MultiTenancyExtensions.EnsureDatabaseExists(this.DatabaseCommands.GetRootDatabase(), this.DefaultDatabase);
      return (IDocumentStore) this;
    }

    protected virtual void InitializeInternal()
    {
      ReplicationInformer replicationInformer = new ReplicationInformer(this.Conventions);
      this.databaseCommandsGenerator = (Func<IDatabaseCommands>) (() =>
      {
        ServerClient local_0 = new ServerClient(this.Url, this.Conventions, this.credentials, replicationInformer, this.jsonRequestFactory, DocumentStore.currentSessionId);
        if (string.IsNullOrEmpty(this.DefaultDatabase))
          return (IDatabaseCommands) local_0;
        else
          return local_0.ForDatabase(this.DefaultDatabase);
      });
      this.asyncDatabaseCommandsGenerator = (Func<IAsyncDatabaseCommands>) (() =>
      {
        AsyncServerClient local_0 = new AsyncServerClient(this.Url, this.Conventions, this.credentials, this.jsonRequestFactory, DocumentStore.currentSessionId);
        if (string.IsNullOrEmpty(this.DefaultDatabase))
          return (IAsyncDatabaseCommands) local_0;
        else
          return local_0.ForDatabase(this.DefaultDatabase);
      });
    }

    public DocumentStore RegisterListener(IDocumentDeleteListener deleteListener)
    {
      this.listeners.DeleteListeners = Enumerable.ToArray<IDocumentDeleteListener>(Enumerable.Concat<IDocumentDeleteListener>((IEnumerable<IDocumentDeleteListener>) this.listeners.DeleteListeners, (IEnumerable<IDocumentDeleteListener>) new IDocumentDeleteListener[1]
      {
        deleteListener
      }));
      return this;
    }

    public DocumentStore RegisterListener(IDocumentQueryListener queryListener)
    {
      this.listeners.QueryListeners = Enumerable.ToArray<IDocumentQueryListener>(Enumerable.Concat<IDocumentQueryListener>((IEnumerable<IDocumentQueryListener>) this.listeners.QueryListeners, (IEnumerable<IDocumentQueryListener>) new IDocumentQueryListener[1]
      {
        queryListener
      }));
      return this;
    }

    public DocumentStore RegisterListener(IDocumentConversionListener conversionListener)
    {
      this.listeners.ConversionListeners = Enumerable.ToArray<IDocumentConversionListener>(Enumerable.Concat<IDocumentConversionListener>((IEnumerable<IDocumentConversionListener>) this.listeners.ConversionListeners, (IEnumerable<IDocumentConversionListener>) new IDocumentConversionListener[1]
      {
        conversionListener
      }));
      return this;
    }

    public IDisposable DisableAggressiveCaching()
    {
      TimeSpan? old = this.jsonRequestFactory.AggressiveCacheDuration;
      this.jsonRequestFactory.AggressiveCacheDuration = new TimeSpan?();
      return (IDisposable) new DisposableAction((Action) (() => this.jsonRequestFactory.AggressiveCacheDuration = old));
    }

    public IDisposable AggressivelyCacheFor(TimeSpan cacheDuration)
    {
      if (cacheDuration.TotalSeconds < 1.0)
        throw new ArgumentException("cacheDuration must be longer than a single second");
      this.jsonRequestFactory.AggressiveCacheDuration = new TimeSpan?(cacheDuration);
      return (IDisposable) new DisposableAction((Action) (() => this.jsonRequestFactory.AggressiveCacheDuration = new TimeSpan?()));
    }

    public IAsyncDocumentSession OpenAsyncSession()
    {
      this.EnsureNotClosed();
      Guid id = Guid.NewGuid();
      DocumentStore.currentSessionId = new Guid?(id);
      try
      {
        if (this.AsyncDatabaseCommands == null)
          throw new InvalidOperationException("You cannot open an async session because it is not supported on embedded mode");
        AsyncDocumentSession asyncDocumentSession = new AsyncDocumentSession(this, this.AsyncDatabaseCommands, this.listeners, id);
        this.AfterSessionCreated((InMemoryDocumentSessionOperations) asyncDocumentSession);
        return (IAsyncDocumentSession) asyncDocumentSession;
      }
      finally
      {
        DocumentStore.currentSessionId = new Guid?();
      }
    }

    public IAsyncDocumentSession OpenAsyncSession(string databaseName)
    {
      this.EnsureNotClosed();
      Guid id = Guid.NewGuid();
      DocumentStore.currentSessionId = new Guid?(id);
      try
      {
        if (this.AsyncDatabaseCommands == null)
          throw new InvalidOperationException("You cannot open an async session because it is not supported on embedded mode");
        AsyncDocumentSession asyncDocumentSession = new AsyncDocumentSession(this, this.AsyncDatabaseCommands.ForDatabase(databaseName), this.listeners, id);
        this.AfterSessionCreated((InMemoryDocumentSessionOperations) asyncDocumentSession);
        return (IAsyncDocumentSession) asyncDocumentSession;
      }
      finally
      {
        DocumentStore.currentSessionId = new Guid?();
      }
    }

    internal void UpdateLastWrittenEtag(Guid? etag)
    {
      if (!etag.HasValue)
        return;
      byte[] y = etag.Value.ToByteArray();
      if (this.lastEtag == null)
      {
        lock (this.lastEtagLocker)
        {
          if (this.lastEtag == null)
          {
            this.lastEtag = new DocumentStore.EtagHolder()
            {
              Bytes = y,
              Etag = etag.Value
            };
            return;
          }
        }
      }
      if (Buffers.Compare(this.lastEtag.Bytes, y) >= 0)
        return;
      lock (this.lastEtagLocker)
      {
        if (Buffers.Compare(this.lastEtag.Bytes, y) >= 0)
          return;
        this.lastEtag = new DocumentStore.EtagHolder()
        {
          Etag = etag.Value,
          Bytes = y
        };
      }
    }

    public Guid? GetLastWrittenEtag()
    {
      DocumentStore.EtagHolder etagHolder = this.lastEtag;
      if (etagHolder == null)
        return new Guid?();
      else
        return new Guid?(etagHolder.Etag);
    }

    private void EnsureNotClosed()
    {
      if (this.WasDisposed)
        throw new ObjectDisposedException("DocumentStore", "The document store has already been disposed and cannot be used");
    }

    private class EtagHolder
    {
      public Guid Etag;
      public byte[] Bytes;
    }
  }
}
