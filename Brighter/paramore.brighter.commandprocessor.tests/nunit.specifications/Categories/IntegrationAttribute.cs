using System;
using NUnit.Framework;

namespace NUnit.Specifications
{
  [AttributeUsage(AttributeTargets.Class)]
  public sealed class IntegrationAttribute : CategoryAttribute
  {
    public IntegrationAttribute() : base("Integration")
    {
    }
  }
}