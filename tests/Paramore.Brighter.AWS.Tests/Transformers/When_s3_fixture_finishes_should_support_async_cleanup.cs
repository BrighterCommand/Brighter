#region Licence



/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */



#endregion



using System;





namespace Paramore.Brighter.AWS.Tests.Transformers;



[Property("Category", "AWS")]

public class S3FixtureAsyncCleanupTests

{

    [Test]

    [Arguments(typeof(S3LuggageUploadTests))]

    [Arguments(typeof(LargeMessagePayloadWrapTests))]

    [Arguments(typeof(LargeMessagePaylodUnwrapTests))]

    [Arguments(typeof(S3LuggageStoreExistsTests))]

    public async System.Threading.Tasks.Task When_s3_fixture_finishes_should_support_async_cleanup(Type fixtureType)

    {

        //Arrange

        var cleanup = fixtureType.GetMethod("DisposeAsync");
        await Assert.That(cleanup).IsNotNull();
        await Assert.That(cleanup!.ReturnType).IsEqualTo(typeof(System.Threading.Tasks.Task));
        await Assert.That(cleanup.GetCustomAttributes(typeof(TUnit.Core.AfterAttribute), inherit: true)).HasSingleItem();

    }

}
