#region License NUnit.Specifications 
/* From https://raw.githubusercontent.com/derekgreer/NUnit.Specifications/master/license.txt
Copyright(c) 2015 Derek B.Greer


Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
#endregion
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
#if NET_STANDARD
    using System.Linq;
#endif

namespace NUnit.Specifications
{
  [DebuggerStepThrough]
  [TestFixture]
  public abstract class ContextSpecification
  {
    public delegate void Because();

    public delegate void Cleanup();

    public delegate void Establish();

    public delegate void It();

    protected Exception Exception;

    public IEnumerator GetEnumerator()
    {
      return GetObservations().GetEnumerator();
    }

    [OneTimeSetUp]
    public void TestFixtureSetUp()
    {
      InitializeContext();
      InvokeEstablish();
      InvokeBecause();
    }

    void InitializeContext()
    {
      var t = GetType();
    }

    [OneTimeTearDown]
    public void TestFixtureTearDown()
    {
      InvokeCleanup();
    }

    void InvokeEstablish()
    {
      var types = new Stack<Type>();
      var type = GetType();

      do
      {
        types.Push(type);
#if NET_STANDARD
        type = type.GetTypeInfo().BaseType;
#else
        type = type.BaseType;
#endif

      } while (type != typeof (ContextSpecification));

      foreach (var t in types)
      {
        var fieldInfos = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

        FieldInfo establishFieldInfo = null;
        foreach (var info in fieldInfos)
        {
          if (info.FieldType.Name.Equals("Establish"))
            establishFieldInfo = info;
        }

        Delegate establish = null;

        if (establishFieldInfo != null) establish = establishFieldInfo.GetValue(this) as Delegate;
        if (establish != null) Exception = Catch.Exception(() => establish.DynamicInvoke(null));
      }
    }

    void InvokeBecause()
    {
      var t = GetType();

      var fieldInfos = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

      FieldInfo becauseFieldInfo = null;
      foreach (var info in fieldInfos)
      {
        if (info.FieldType.Name.Equals("Because"))
          becauseFieldInfo = info;
      }

      Delegate because = null;

      if (becauseFieldInfo != null) because = becauseFieldInfo.GetValue(this) as Delegate;
      if (because != null) Exception = Catch.Exception(() => because.DynamicInvoke(null));
    }

    void InvokeCleanup()
    {
      try
      {
        var t = GetType();

        var fieldInfos = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

        FieldInfo cleanupFieldInfo = null;
        foreach (var info in fieldInfos)
        {
          if (info.FieldType.Name.Equals("Cleanup"))
            cleanupFieldInfo = info;
        }

        Delegate cleanup = null;

        if (cleanupFieldInfo != null) cleanup = cleanupFieldInfo.GetValue(this) as Delegate;
        if (cleanup != null) cleanup.DynamicInvoke(null);
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    }

    public IEnumerable GetObservations()
    {
      var t = GetType();

      var categoryName = "Uncategorized";
      var description = string.Empty;

#if NET_STANDARD
      var categoryAttributes = t.GetTypeInfo().CustomAttributes;
      var subjectAttributes = t.GetTypeInfo().CustomAttributes;
#else
      var categoryAttributes = t.GetCustomAttributes(typeof(CategoryAttribute), true);
      var subjectAttributes = t.GetCustomAttributes(typeof(SubjectAttribute), true);
#endif

#if NET_STANDARD
      if (categoryAttributes.Any())
      {
        categoryName = categoryAttributes.First().ConstructorArguments[0].Value.ToString();
#else
      if (categoryAttributes.Length > 0)
      {
        var categoryAttribute = (CategoryAttribute)categoryAttributes[0];
        categoryName = categoryAttribute.Name;
#endif
      }

#if NET_STANDARD
      if (subjectAttributes.Any())
      {
        description = subjectAttributes.First().ConstructorArguments[0].Value.ToString();
#else
            if (subjectAttributes.Length > 0)
      {
        var subjectAttribute = (SubjectAttribute)subjectAttributes[0];
        description = subjectAttribute.Subject;
#endif
      }

      var fieldInfos = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
      var itFieldInfos = new List<FieldInfo>();

      foreach (var info in fieldInfos)
      {
        if (info.FieldType.Name.Equals("It"))
          itFieldInfos.Add(info);
      }

      foreach (var it in itFieldInfos)
      {
        var data = new TestCaseData(it.GetValue(this));
        data.SetDescription(description);
        data.SetName(it.Name.Replace("_", " "));
        data.SetCategory(categoryName);
        yield return data;
      }
    }

    [Test, SpecificationSource("GetObservations")]
    public void Observations(It observation)
    {
      if (Exception != null)
        throw Exception;

      observation();
    }
  }
}