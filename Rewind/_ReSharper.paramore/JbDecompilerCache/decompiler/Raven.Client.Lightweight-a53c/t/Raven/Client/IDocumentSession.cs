// Type: Raven.Client.IDocumentSession
// Assembly: Raven.Client.Lightweight, Version=1.0.0.0, Culture=neutral, PublicKeyToken=37f41c7f99471593
// Assembly location: C:\users\sundance\work\paramore\ignorance\paramore.features\bin\Debug\Raven.Client.Lightweight.dll

using Raven.Client.Document;
using Raven.Client.Indexes;
using Raven.Client.Linq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Raven.Client
{
  public interface IDocumentSession : IDisposable
  {
    ISyncAdvancedSessionOperation Advanced { get; }

    void Delete<T>(T entity);

    T Load<T>(string id);

    T[] Load<T>(params string[] ids);

    T[] Load<T>(IEnumerable<string> ids);

    T Load<T>(ValueType id);

    IRavenQueryable<T> Query<T>(string indexName);

    IRavenQueryable<T> Query<T>();

    IRavenQueryable<T> Query<T, TIndexCreator>() where TIndexCreator : new(), AbstractIndexCreationTask;

    ILoaderWithInclude<object> Include(string path);

    ILoaderWithInclude<T> Include<T>(Expression<Func<T, object>> path);

    void SaveChanges();

    void Store(object entity, Guid etag);

    void Store(object entity);
  }
}
