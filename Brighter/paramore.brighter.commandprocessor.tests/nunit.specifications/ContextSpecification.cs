using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;

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

    [TestFixtureSetUp]
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

    [TestFixtureTearDown]
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
        type = type.BaseType;
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

      var categoryAttributes = t.GetCustomAttributes(typeof (CategoryAttribute), true);
      var subjectAttributes = t.GetCustomAttributes(typeof (SubjectAttribute), true);

      if (categoryAttributes.Length > 0)
      {
        var categoryAttribute = (CategoryAttribute) categoryAttributes[0];
        categoryName = categoryAttribute.Name;
      }

      if (subjectAttributes.Length > 0)
      {
        var subjectAttribute = (SubjectAttribute) subjectAttributes[0];
        description = subjectAttribute.Subject;
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

    [Test, TestCaseSource("GetObservations")]
    public void Observation(It observation)
    {
      if (Exception != null)
        throw Exception;

      observation();
    }
  }
}