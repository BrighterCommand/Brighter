#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Records every <see cref="PostedOrderMapper"/> instance the container constructs, and the dispose
/// count <see cref="PostTwiceController"/> observed on the shared instance immediately after its
/// second <c>Post</c> returned, so a test can compare that against the count once the whole HTTP
/// request has completed. Register as a singleton in the container under test.
/// </summary>
public sealed class PostedOrderMapperRecorder
{
    private readonly List<PostedOrderMapper> _constructed = new();

    /// <summary>
    /// Every <see cref="PostedOrderMapper"/> instance the container has constructed, in construction order.
    /// </summary>
    public IReadOnlyList<PostedOrderMapper> Constructed => _constructed;

    /// <summary>
    /// The shared mapper's own dispose count, as observed by the controller immediately after its
    /// second <c>Post</c> call returned — before the HTTP response, and therefore before ASP.NET could
    /// have disposed the request scope.
    /// </summary>
    public int DisposeCountAfterSecondPost { get; private set; }

    /// <summary>
    /// Records a newly constructed <see cref="PostedOrderMapper"/>.
    /// </summary>
    public void RecordConstruction(PostedOrderMapper mapper) => _constructed.Add(mapper);

    /// <summary>
    /// Records the shared mapper's dispose count as observed immediately after the second <c>Post</c> returned.
    /// </summary>
    public void RecordDisposeCountAfterSecondPost(int disposeCount) => DisposeCountAfterSecondPost = disposeCount;
}
