// Type: Raven.Client.IDocumentStore
// Assembly: Raven.Client.Lightweight, Version=1.0.0.0, Culture=neutral, PublicKeyToken=37f41c7f99471593
// Assembly location: C:\users\ian\oss\paramore\ignorance\Lib\Raven.Client.Lightweight.dll

using Raven.Client.Connection;
using Raven.Client.Connection.Async;
using Raven.Client.Document;
using System;
using System.Collections.Specialized;
using System.Net;

namespace Raven.Client
{
  public interface IDocumentStore : IDisposalNotification, IDisposable
  {
    NameValueCollection SharedOperationsHeaders { get; }

    HttpJsonRequestFactory JsonRequestFactory { get; }

    string Identifier { get; set; }

    IAsyncDatabaseCommands AsyncDatabaseCommands { get; }

    IDatabaseCommands DatabaseCommands { get; }

    DocumentConvention Conventions { get; }

    string Url { get; }

    IDisposable AggressivelyCacheFor(TimeSpan cahceDuration);

    IDisposable DisableAggressiveCaching();

    IDocumentStore Initialize();

    IAsyncDocumentSession OpenAsyncSession();

    IAsyncDocumentSession OpenAsyncSession(string database);

    IDocumentSession OpenSession();

    IDocumentSession OpenSession(string database);

    IDocumentSession OpenSession(string database, ICredentials credentialsForSession);

    IDocumentSession OpenSession(ICredentials credentialsForSession);

    Guid? GetLastWrittenEtag();
  }
}
