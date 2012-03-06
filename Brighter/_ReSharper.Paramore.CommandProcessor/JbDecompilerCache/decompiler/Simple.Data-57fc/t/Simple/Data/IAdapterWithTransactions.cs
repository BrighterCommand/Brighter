// Type: Simple.Data.IAdapterWithTransactions
// Assembly: Simple.Data, Version=0.12.2.2, Culture=neutral, PublicKeyToken=null
// Assembly location: C:\users\sundance\work\paramore\brighter\Examples\tasklist.web\Bin\Simple.Data.dll

using System;
using System.Collections.Generic;

namespace Simple.Data
{
  public interface IAdapterWithTransactions
  {
    IAdapterTransaction BeginTransaction();

    IAdapterTransaction BeginTransaction(string name);

    IEnumerable<IDictionary<string, object>> Find(string tableName, SimpleExpression criteria, IAdapterTransaction transaction);

    IDictionary<string, object> Insert(string tableName, IDictionary<string, object> data, IAdapterTransaction transaction, bool resultRequired);

    IEnumerable<IDictionary<string, object>> InsertMany(string tableName, IEnumerable<IDictionary<string, object>> data, IAdapterTransaction transaction, Func<IDictionary<string, object>, Exception, bool> onError, bool resultRequired);

    int Update(string tableName, IDictionary<string, object> data, SimpleExpression criteria, IAdapterTransaction transaction);

    int Delete(string tableName, SimpleExpression criteria, IAdapterTransaction transaction);

    int UpdateMany(string tableName, IEnumerable<IDictionary<string, object>> dataList, IAdapterTransaction adapterTransaction);

    int UpdateMany(string tableName, IEnumerable<IDictionary<string, object>> dataList, IAdapterTransaction adapterTransaction, IList<string> keyFields);

    int Update(string tableName, IDictionary<string, object> data, IAdapterTransaction adapterTransaction);

    int UpdateMany(string tableName, IList<IDictionary<string, object>> dataList, IEnumerable<string> criteriaFieldNames, IAdapterTransaction adapterTransaction);
  }
}
