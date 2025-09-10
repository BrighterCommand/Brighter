#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// How do we validate the topic - when we opt to validate or create (already exists) infrastructure
/// Relates to how we interpret the RoutingKey. Is it an Arn (0 or 1) or a name (2)
/// 0 - The topic is supplied as an Arn, and should be checked with a GetTopicAttributes call. May be any account.
/// 1 - The topic is supplied as a name, and should be turned into an Arn and checked via a GetTopicAttributes call. Must be in caller's account.
/// 2 - The topic is supplies as a name, and should be checked by a ListTopics call. Must be in caller's account. Rate limited at 30 calls per second
/// </summary>
public enum TopicFindBy
{
    Arn,
    Convention,
    Name,
}