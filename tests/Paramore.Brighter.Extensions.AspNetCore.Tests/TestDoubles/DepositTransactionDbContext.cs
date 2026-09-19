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

using Microsoft.EntityFrameworkCore;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// A real, Sqlite-backed <c>DbContext</c> (AC-52) - a stand-in for an application's own <c>DbContext</c>,
/// registered <c>AddDbContext</c> in a test host, so a test can prove a handler's outbox write, made
/// through a transaction provider over this context, joins whatever transaction the controller already
/// opened on the same, shared instance.
/// </summary>
public sealed class DepositTransactionDbContext : DbContext
{
    public DepositTransactionDbContext(DbContextOptions<DepositTransactionDbContext> options) : base(options)
    {
    }

    public DbSet<DepositedEntity> DepositedEntities => Set<DepositedEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DepositedEntity>(entity =>
        {
            entity.ToTable("DepositedEntities");
            entity.HasKey(e => e.Id);
        });
    }
}
