using System;
using NUnit.Framework;

namespace NUnit.Specifications
{
  [AttributeUsage(AttributeTargets.Class)]
  public sealed class SubcutaneousAttribute : CategoryAttribute
  {
    public SubcutaneousAttribute() : base("Subcutaneous")
    {
    }
  }
}