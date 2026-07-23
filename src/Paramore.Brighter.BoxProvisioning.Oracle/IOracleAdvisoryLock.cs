// The MIT License (MIT)
// Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace Paramore.Brighter.BoxProvisioning.Oracle;

/// <summary>
/// Encapsulates the Oracle session-level advisory-lock primitive used to serialise
/// box-table migrations. Backed by <c>DBMS_LOCK.REQUEST</c> / <c>DBMS_LOCK.RELEASE</c>.
/// Requires the executing user to hold <c>EXECUTE ON DBMS_LOCK</c> (or
/// <c>EXECUTE ANY PROCEDURE</c>).
/// </summary>
/// <remarks>
/// The default implementation is <see cref="OracleAdvisoryLock"/>. <see cref="OracleBoxMigrationRunner"/>
/// accepts an injected instance through its constructor for testability and for advanced
/// integrators. Lock-key derivation is performed by the runner via
/// <see cref="OracleMigrationLockName.For"/>; the abstraction owns only the
/// <c>DBMS_LOCK.REQUEST</c> / <c>DBMS_LOCK.RELEASE</c> PL/SQL calls.
/// </remarks>
public interface IOracleAdvisoryLock
{
    /// <summary>
    /// Acquires a session-level exclusive advisory lock via <c>DBMS_LOCK.REQUEST</c>.
    /// </summary>
    /// <param name="connection">The open <see cref="OracleConnection"/> on which to run the PL/SQL block.</param>
    /// <param name="lockKey">The lock name; must be within the 128-character <c>DBMS_LOCK.ALLOCATE_UNIQUE</c> limit.
    /// Callers should derive via <see cref="OracleMigrationLockName.For"/>.</param>
    /// <param name="timeout">Maximum wait. Values are truncated to whole seconds; zero means
    /// try-once (NOWAIT).</param>
    /// <param name="cancellationToken">Cancellation token observed by the underlying command.</param>
    /// <exception cref="TimeoutException">Thrown when <c>DBMS_LOCK.REQUEST</c> returns <c>1</c>
    /// (could not acquire within <paramref name="timeout"/>).</exception>
    /// <exception cref="OracleAdvisoryLockException">Thrown when <c>DBMS_LOCK.REQUEST</c> returns
    /// <c>2</c> (deadlock), <c>3</c> (parameter error), or <c>5</c> (illegal handle).</exception>
    Task AcquireAsync(
        OracleConnection connection, string lockKey,
        TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Releases the advisory lock via <c>DBMS_LOCK.RELEASE</c>.
    /// </summary>
    /// <param name="connection">The same <see cref="OracleConnection"/> on which the lock was acquired.</param>
    /// <param name="lockKey">The lock name used at acquisition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> — released successfully (code 0).
    /// <c>false</c> — not the owner or parameter error (codes 3–5).
    /// <c>null</c> — lock was never acquired (no handle allocated for this session).
    /// </returns>
    Task<bool?> ReleaseAsync(
        OracleConnection connection, string lockKey,
        CancellationToken cancellationToken);
}
