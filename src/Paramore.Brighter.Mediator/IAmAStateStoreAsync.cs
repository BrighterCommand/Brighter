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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Mediator;

/// <summary>
/// Used to store the state of a workflow
/// </summary>
public interface IAmAStateStoreAsync
{
    /// <summary>
    /// Saves the job 
    /// </summary>
    /// <param name="job">The job</param>
    /// <param name="cancellationToken"></param>
    Task SaveJobAsync<TData>(Job<TData>? job, CancellationToken cancellationToken = default(CancellationToken));

    /// <summary>
    /// Retrieves a job via its Id
    /// </summary>
    /// <param name="id">The id of the job</param>
    /// <returns>if found, the job, otherwise null</returns>
    Task<Job?> GetJobAsync(string? id) ;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="jobAge">The time before now at which becomes scheduled</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<Job>> GetDueJobsAsync(TimeSpan jobAge, CancellationToken cancellationToken = default(CancellationToken));
}
