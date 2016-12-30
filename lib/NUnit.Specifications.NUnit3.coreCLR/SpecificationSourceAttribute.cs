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
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;

namespace NUnit.Specifications
{
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
  public class SpecificationSourceAttribute : NUnitAttribute, ITestBuilder, IImplyFixture
  {
    readonly NUnitTestCaseBuilder _builder = new NUnitTestCaseBuilder();

    public SpecificationSourceAttribute(string sourceName)
    {
      SourceName = sourceName;
    }

    public string SourceName { get; }

    public IEnumerable<TestMethod> BuildFrom(IMethodInfo method, Test suite)
    {
      var classNameTokens = suite.FullName.Split('.', '+');
      suite.FullName = classNameTokens[classNameTokens.Length - 1].Replace("_", " ");
      suite.Name = suite.FullName;

      foreach (TestCaseParameters testCaseParameters in GetTestCasesFor(method))
      {
        yield return _builder.BuildTestMethod(method, suite, testCaseParameters);
      }
    }

    IEnumerable<TestCaseParameters> GetTestCasesFor(IMethodInfo method)
    {
      var list = new List<TestCaseParameters>();
      try
      {
        var testCaseSource = GetTestCaseSource(method);
        if (testCaseSource != null)
        {
          foreach (var obj in testCaseSource)
          {
            var testCaseData = obj as TestCaseParameters;
            list.Add(testCaseData);
          }
        }
      }
      catch (Exception ex)
      {
        list.Clear();
        list.Add(new TestCaseParameters(ex));
      }
      return list;
    }

    IEnumerable GetTestCaseSource(IMethodInfo method)
    {
      var type = method.TypeInfo.Type;
      var specification = Reflect.Construct(type);

      var member = type.GetMember(SourceName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
      if (member.Length == 1)
      {
        var memberInfo = member[0];
        var methodInfo = memberInfo as MethodInfo;
        if (methodInfo != null)
        {
          return (IEnumerable) methodInfo.Invoke(specification, null);
        }
      }
      return null;
    }
  }
}