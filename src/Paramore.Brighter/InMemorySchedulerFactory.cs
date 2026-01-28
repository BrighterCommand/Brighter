#region Licence
/* The MIT License (MIT)
Copyright © 2025 Rafael Andrade

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

namespace Paramore.Brighter;

/// <summary>
/// The <see cref="InMemoryScheduler"/> factory
/// </summary>
public class InMemorySchedulerFactory : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
{
    /// <summary>
    /// The <see cref="System.TimeProvider"/>.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    
    /// <summary>
    /// Get or create a scheduler id for a message
    /// </summary>
    /// <remarks>
    /// The default approach is generate a Guid 
    /// </remarks>
    public Func<Message, string> GetOrCreateMessageSchedulerId { get; set; } = _ => Id.Random().Value;

    /// <summary>
    /// Get or create  a scheduler id to a request
    /// </summary>
    /// <remarks>
    /// The default approach is generate a Guid 
    /// </remarks>
    public Func<IRequest, string> GetOrCreateRequestSchedulerId { get; set; } = _ => Id.Random().Value;

    /// <summary>
    /// The action be executed on conflict during scheduler message
    /// </summary>
    public OnSchedulerConflict OnConflict { get; set; } = OnSchedulerConflict.Throw;
    
    /// <inheritdoc />
    public IAmAMessageScheduler Create(IAmACommandProcessor processor) 
        => new InMemoryScheduler(processor, TimeProvider, GetOrCreateRequestSchedulerId, GetOrCreateMessageSchedulerId, OnConflict);

    /// <inheritdoc />
    public IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor)
        => new InMemoryScheduler(processor, TimeProvider, GetOrCreateRequestSchedulerId, GetOrCreateMessageSchedulerId, OnConflict);

    /// <inheritdoc />
    public IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor)
        => new InMemoryScheduler(processor, TimeProvider, GetOrCreateRequestSchedulerId, GetOrCreateMessageSchedulerId, OnConflict);
}
 
