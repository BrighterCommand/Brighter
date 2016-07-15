using System;
using System.Diagnostics;

namespace NUnit.Specifications
{
  [DebuggerStepThrough]
  public static class Catch
  {
    public static Exception Exception(Action action)
    {
      Exception exception = null;

      try
      {
        action();
      }
      catch (Exception e)
      {
        exception = e;
      }

      return exception;
    }
  }
}