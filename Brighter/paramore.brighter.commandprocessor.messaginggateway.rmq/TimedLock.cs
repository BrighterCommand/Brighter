// Code licensed under the MIT License
using System;
#if DEBUG
using System.Collections;
using System.Diagnostics;
#endif
using System.Runtime.Serialization;
using System.Threading;

/// <summary>
/// Class provides a nice way of obtaining a lock that will time out 
/// with a cleaner syntax than using the whole Monitor.TryEnter() method.
/// </summary>
/// <remarks>
/// Adapted from Ian Griffiths article http://www.interact-sw.co.uk/iangblog/2004/03/23/locking 
/// and incorporating suggestions by Marek Malowidzki as outlined in this blog post 
/// http://www.interact-sw.co.uk/iangblog/2004/05/12/timedlockstacktrace
/// </remarks>
/// <example>
/// Instead of:
/// <code>
/// lock(obj)
/// {
///     //Thread safe operation
/// }
/// 
/// do this:
/// 
/// using(TimedLock.Lock(obj))
/// {
///     //Thread safe operations
/// }
/// 
/// or this:
/// 
/// try
/// {
///     TimedLock timeLock = TimedLock.Lock(obj);
///     //Thread safe operations
///     timeLock.Dispose();
/// }
/// catch(LockTimeoutException e)
/// {
///     Console.WriteLine("Couldn't get a lock!");
///     StackTrace otherStack = e.GetBlockingThreadStackTrace(5000);
///     if(otherStack == null)
///     {
///         Console.WriteLine("Couldn't get other stack!");
///     }
///     else
///     {
///         Console.WriteLine("Stack trace of thread that owns lock!");
///     }
/// }
/// </code>
/// </example>
public struct TimedLock : IDisposable
{
    /// <summary>
    /// Attempts to obtain a lock on the specified object for up 
    /// to 10 seconds.
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public static TimedLock Lock(object o)
    {
        return Lock(o, TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Attempts to obtain a lock on the specified object for up to 
    /// the specified timeout.
    /// </summary>
    /// <param name="o"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public static TimedLock Lock(object o, TimeSpan timeout)
    {
        Console.WriteLine("Acquiring lock");
        Thread.BeginCriticalRegion();
        var timedLock = new TimedLock(o);
        if (!Monitor.TryEnter(o, timeout))
        {
            // Failed to acquire lock.
#if DEBUG
            GC.SuppressFinalize(timedLock.leakDetector);
            throw new LockTimeoutException(o);
#else
            throw new LockTimeoutException();
#endif
        }
        return timedLock;
    }

    TimedLock(object o)
    {
        target = o;
#if DEBUG
        leakDetector = new Sentinel();
#endif
    }

    readonly object target;

    /// <summary>
    /// Disposes of this lock.
    /// </summary>
    public void Dispose()
    {
        Console.WriteLine("Disposing lock");
        // Owning thread is done.
#if DEBUG
        try
        {
            //This shouldn't throw an exception.
            LockTimeoutException.ReportStackTraceIfError(target);
        }
        finally
        {
            //But just in case...
            Monitor.Exit(target);
        }
#else
        Monitor.Exit(target);
#endif
#if DEBUG
        // It's a bad error if someone forgets to call Dispose,
        // so in Debug builds, we put a finalizer in to detect
        // the error. If Dispose is called, we suppress the
        // finalizer.
        GC.SuppressFinalize(leakDetector);
#endif
        Thread.EndCriticalRegion();
    }

#if DEBUG
    // (In Debug mode, we make it a class so that we can add a finalizer
    // in order to detect when the object is not freed.)
    class Sentinel
    {
        ~Sentinel()
        {
            // If this finalizer runs, someone somewhere failed to
            // call Dispose, which means we've failed to leave
            // a monitor!
            //System.Diagnostics.Debug.Fail("Undisposed lock");
            throw new UndisposedLockException("Undisposed Lock");
        }
    }

    readonly Sentinel leakDetector;
#endif
}

/// <summary>
/// Thrown when a lock times out.
/// </summary>
[Serializable]
public class LockTimeoutException : Exception
{
#if DEBUG
    readonly object lockTarget;
    StackTrace blockingStackTrace;
    static readonly Hashtable failedLockTargets = new Hashtable();

    /// <summary>
    /// Sets the stack trace for the given lock target 
    /// if an error occurred.
    /// </summary>
    /// <param name="lockTarget">Lock target.</param>
    public static void ReportStackTraceIfError(object lockTarget)
    {
        lock (failedLockTargets)
        {
            if (failedLockTargets.ContainsKey(lockTarget))
            {
                var waitHandle = failedLockTargets[lockTarget] as ManualResetEvent;
                if (waitHandle != null)
                {
                    waitHandle.Set();
                }
                failedLockTargets[lockTarget] = new StackTrace();
                //Also. if you don't call GetBlockingStackTrace()
                //the lockTarget doesn't get removed from the hash 
                //table and so we'll always think there's an error
                //here (though no locktimeout exception is thrown).
            }
        }
    }

    /// <summary>
    /// Creates a new <see cref="LockTimeoutException"/> instance.
    /// </summary>
    /// <remarks>Use this exception.</remarks>
    /// <param name="lockTarget">Object we tried to lock.</param>
    public LockTimeoutException(object lockTarget)
        : base("Timeout waiting for lock")
    {
        lock (failedLockTargets)
        {
            // This is safer in case somebody forgot to remove 
            // the lock target.
            var waitHandle = new ManualResetEvent(false);
            failedLockTargets[lockTarget] = waitHandle;
        }
        this.lockTarget = lockTarget;
    }

    /// <summary>
    /// Stack trace of the thread that holds a lock on the object 
    /// this lock is attempting to acquire when it fails.
    /// </summary>
    /// <param name="timeout">Number of milliseconds to wait for the blocking stack trace.</param>
    public StackTrace GetBlockingStackTrace(int timeout)
    {
        if (timeout < 0)
            throw new InvalidOperationException("We'd all like to be able to go back in time, but this is not allowed. Please choose a positive wait time.");

        ManualResetEvent waitHandle;
        lock (failedLockTargets)
        {
            waitHandle = failedLockTargets[lockTarget] as ManualResetEvent;
        }
        if (timeout > 0 && waitHandle != null)
        {
            waitHandle.WaitOne(timeout, false);
        }
        lock (failedLockTargets)
        {
            //Hopefully by now we have a stack trace.
            blockingStackTrace = failedLockTargets[lockTarget] as StackTrace;
        }

        return blockingStackTrace;
    }
#endif

    /// <summary>
    /// Creates a new <see cref="LockTimeoutException"/> instance.
    /// </summary>
    public LockTimeoutException()
        : base("Timeout waiting for lock")
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="message"></param>
    public LockTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public LockTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    protected LockTimeoutException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }

    /// <summary>
    /// Returns a string representation of the exception.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        string toString = base.ToString();
#if DEBUG
        if (blockingStackTrace != null)
        {
            toString += "\n-------Blocking Stack Trace--------\n" + blockingStackTrace;
        }
#endif
        return toString;
    }
}

#if DEBUG
/// <summary>
/// This exception indicates that a user of the TimedLock struct 
/// failed to leave a Monitor.  This could be the result of a 
/// deadlock or forgetting to use the using statement or a try 
/// finally block.
/// </summary>
[Serializable]
public class UndisposedLockException : Exception
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="message"></param>
    public UndisposedLockException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Special constructor used for deserialization.
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    protected UndisposedLockException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
#endif