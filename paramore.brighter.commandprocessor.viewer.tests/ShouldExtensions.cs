using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace paramore.brighter.commandprocessor.viewer.tests
{
    public static class ShouldExtensions
    {
        public static void ShouldBeNull(this object objectToCheck)
        {
            Assert.Null(objectToCheck);
        }
        public static void ShouldNotBeNull<T>(this T objectToCheck) where T : class
        {
            Assert.NotNull(objectToCheck);
        }

        public static void ShouldBeFalse(this bool condition)
        {
            Assert.False(condition);
        }
        public static void ShouldBeTrue(this bool condition)
        {
            Assert.True(condition);
        }

        public static void ShouldEqual<T>(this T actual, T expected)
        {
            Assert.AreEqual(expected, actual);
        }

        public static void ShouldNotEqual<T>(this T actual, T notExpected)
        {
            Assert.AreNotEqual(actual, notExpected);   
        }

        public static void ShouldBeOfExactType<TExpectedType>(this object objectToCheck)
        {
            Assert.IsInstanceOf<TExpectedType>(objectToCheck);
        }
        public static void ShouldBeOfExactType(this object objectToCheck, Type tExpectedType)
        {
            Assert.IsInstanceOf(tExpectedType, objectToCheck);
        }
        public static void ShouldBeAssignableTo(this object objectToCheck, Type tExpectedType)
        {
            Assert.IsAssignableFrom(tExpectedType, objectToCheck);
        }

        public static void ShouldContain(this string actualString, string expectedSubString)
        {
            StringAssert.Contains(expectedSubString, actualString);
        }
        public static void ShouldContain<T>(this IEnumerable actualEnumerable, object expectedObject)
        {
            CollectionAssert.Contains(actualEnumerable, expectedObject);
        }
        public static void ShouldContain<T>(this ICollection<T> actualEnumerable, object expectedObject)
        {
            CollectionAssert.Contains(actualEnumerable, expectedObject);
        }

        public static void ShouldNotContain<T>(this ICollection<T> actualEnumerable, object expectedObject)
        {
            CollectionAssert.DoesNotContain(actualEnumerable, expectedObject);
        }

        public static void ShouldContain<T>(this IEnumerable<T> list, Func<T, bool> condition)
        {
            Assert.True(list.Any(condition));
        }
        public static void ShouldContain<T>(this IList<T> list, Func<T, bool> condition)
        {
            Assert.True(list.Any(condition));
        }

        public static void ShouldBeTheSameAs<T>(this T actual, T expected)
        {
            Assert.AreSame(expected, actual);
        }
        public static void ShouldContainErrorMessage(this Exception exception, string message)
        {
            Assert.NotNull(exception);
            ShouldContain(exception.Message, message);
        }

        public static void ShouldMatch<T>(this T actual, Func<T, bool> condition)
        {
            Assert.True(condition.Invoke(actual));
        }

        public static void ShouldBeEmpty<T>(this IEnumerable<T> list)
        {
            Assert.False(list.Any());
        }
        public static void ShouldBeGreaterThan(this IComparable actual, IComparable greaterThan)
        {
            Assert.Greater(actual, greaterThan);
        }

        public static void ShouldBeGreaterThanOrEqualTo(this IComparable actual, IComparable greaterThanOrEqualTo)
        {
            Assert.GreaterOrEqual(actual, greaterThanOrEqualTo);
        }
    }
}