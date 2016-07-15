using System;
using NUnit.Framework;

namespace NUnit.Specifications
{
  [AttributeUsage(AttributeTargets.Class)]
  public sealed class AcceptanceAttribute : CategoryAttribute
  {
    public AcceptanceAttribute()
      : base("Acceptance")
    {
    }
  }
}