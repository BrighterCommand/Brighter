using NUnit.Framework;

namespace nUnitShouldAdapter
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

        public static void ShouldBeOfExactType<TExpectedType>(this object objectToCheck)
        {
            Assert.IsInstanceOf<TExpectedType>(objectToCheck);
        }

        public static void ShouldContain(this string actualString, string expectedSubString)
        {
            StringAssert.Contains(expectedSubString, actualString);
        }
    }
}