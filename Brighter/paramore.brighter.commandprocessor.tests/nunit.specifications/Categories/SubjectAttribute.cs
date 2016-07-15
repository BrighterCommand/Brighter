using System;

namespace NUnit.Specifications
{
  [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class)]
  public sealed class SubjectAttribute : Attribute
  {
    public SubjectAttribute(string subject)
    {
      Subject = subject;
    }

    public string Subject { get; set; }
  }
}