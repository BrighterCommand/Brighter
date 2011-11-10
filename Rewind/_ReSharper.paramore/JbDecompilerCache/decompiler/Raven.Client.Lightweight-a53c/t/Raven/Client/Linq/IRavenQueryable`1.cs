// Type: Raven.Client.Linq.IRavenQueryable`1
// Assembly: Raven.Client.Lightweight, Version=1.0.0.0, Culture=neutral, PublicKeyToken=37f41c7f99471593
// Assembly location: C:\users\sundance\work\paramore\ignorance\paramore.features\bin\Debug\Raven.Client.Lightweight.dll

using Raven.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Raven.Client.Linq
{
  public interface IRavenQueryable<T> : IOrderedQueryable<T>, IQueryable<T>, IEnumerable<T>, IOrderedQueryable, IQueryable, IEnumerable
  {
    IRavenQueryable<T> Statistics(out RavenQueryStatistics stats);

    IRavenQueryable<T> Customize(Action<IDocumentQueryCustomization> action);
  }
}
