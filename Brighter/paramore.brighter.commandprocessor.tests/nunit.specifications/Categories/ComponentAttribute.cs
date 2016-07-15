using System;
using NUnit.Framework;

namespace NUnit.Specifications
{
  [AttributeUsage(AttributeTargets.Class)]
  public sealed class ComponentAttribute : CategoryAttribute
  {
    public ComponentAttribute() : base("Component")
    {
    }
  }
}