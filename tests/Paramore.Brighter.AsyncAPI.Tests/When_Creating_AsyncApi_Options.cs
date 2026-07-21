using System.Threading.Tasks;
#region Licence
/* The MIT License (MIT)
Copyright © 2026 Jonny Olliff-Lee <jonny.ollifflee@gmail.com>

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


namespace Paramore.Brighter.AsyncAPI.Tests
{
    public class When_Creating_AsyncApi_Options
    {
        [Test]
        public async Task It_Should_Have_Correct_Defaults()
        {
            var options = new AsyncApiOptions();

            await Assert.That(options.Title).IsEqualTo("Brighter Application");
            await Assert.That(options.Version).IsEqualTo("1.0.0");
            await Assert.That(options.Description).IsNull();
            await Assert.That(options.Servers).IsNull();
            await Assert.That(options.AssembliesToScan).IsNull();
            await Assert.That(options.DisableAssemblyScanning).IsFalse();
            await Assert.That(options.SupplementalPublications).IsNull();
        }
    }
}