// Type: Simple.Data.IAdapterTransaction
// Assembly: Simple.Data, Version=0.12.2.2, Culture=neutral, PublicKeyToken=null
// Assembly location: C:\users\sundance\work\paramore\brighter\Examples\tasklist.web\Bin\Simple.Data.dll

using System;

namespace Simple.Data
{
  public interface IAdapterTransaction : IDisposable
  {
    string Name { get; }

    void Commit();

    void Rollback();
  }
}
