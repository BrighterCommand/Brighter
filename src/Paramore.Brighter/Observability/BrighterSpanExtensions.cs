#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Provide a helper method to turn span attributes into strings (lowercase)
/// </summary>
public static class BrighterSpanExtensions
{
   ///<summary>
   /// Provide a string representation of the command processor operation
   /// </summary>
   public static string ToSpanName(this CommandProcessorSpanOperation operation) => operation switch
   {
       CommandProcessorSpanOperation.Create => "create",
       CommandProcessorSpanOperation.Deposit => "deposit",
       CommandProcessorSpanOperation.Publish => "publish",
       CommandProcessorSpanOperation.Send => "send",
       CommandProcessorSpanOperation.Clear => "clear",
       CommandProcessorSpanOperation.Archive => "archive",
       CommandProcessorSpanOperation.Scheduler => "scheduler",
       _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
   };

   ///<summary>
   /// Provide a string representation of the inbox/outbox operation
   /// </summary>   
   public static string ToSpanName(this BoxDbOperation span) => span switch
   {
       BoxDbOperation.Add => "add.message",
       BoxDbOperation.Delete => "delete.message",
       BoxDbOperation.DispatchedMessages => "retrieve.dispatched_messages",
       BoxDbOperation.Get => "retrieve.message",
       BoxDbOperation.MarkDispatched => "mark_as_dispatched.outstanding_messages",
       BoxDbOperation.OutStandingMessages => "retrieve.outstanding_messages",
       BoxDbOperation.Exists => "message.exists",
       _ => throw new ArgumentOutOfRangeException(nameof(span), span, null)
   };
   
   public static string ToSpanName(this MessagePumpSpanOperation operation) => operation switch
   {
       MessagePumpSpanOperation.Receive => "receive",
       MessagePumpSpanOperation.Process => "process",
       MessagePumpSpanOperation.Begin => "begin",
       _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
   };


   public static string ToSpanName(this ClaimCheckOperation operation) => operation switch
   {
       ClaimCheckOperation.Delete => "delete.message",
       ClaimCheckOperation.Store => "store.message",
       ClaimCheckOperation.Retrieve => "retrieve.message",
       ClaimCheckOperation.HasClaim => "has_claim.message",
       _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
   };
}
