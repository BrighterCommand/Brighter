#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Microsoft.Practices.ServiceLocation;

namespace paramore.brighter.commandprocessor
{
    public abstract class TrackedServiceLocator : ServiceLocatorImplBase, IManageLifetimes
    {
        private LifetimeScope lifetime;
        private readonly HashSet<Type> doNotTrackList = new HashSet<Type>(); 

        //When we pass the instance out, we only expose the IDisposable, all a consumer can do is choose to release
        public IDisposable CreateLifetime()
        {
            lifetime = new LifetimeScope();
            return lifetime;
        }

        protected void DoNotTrack(Type type)
        {
            doNotTrackList.Add(type);
        }

        public int TrackedItemCount
        {
            get { return lifetime.TrackedItemCount; }
        }

        //used to avoid tracking i.e. a singletone we don't want to kill at the end of the scope
        protected virtual void TrackItem(object instance)
        {
            if(!doNotTrackList.Contains(instance.GetType()))
                lifetime.Add(instance);
        }
        
        protected virtual void TrackItems(IEnumerable<object> instances)
        {
            foreach (var instance in instances)
            {
               TrackItem(instance); 
            }
        }

        internal class LifetimeScope: IDisposable
        {
            private readonly List<object> trackedObjects = new List<object>();

            public int TrackedItemCount
            {
                get { return trackedObjects.Count; }
            }

            public void Add(object instance)
            {
                trackedObjects.Add(instance);
            }

            public void Dispose()
            {
                foreach (var trackedItem in trackedObjects)
                {
                    //free disposableitems
                    var disposableItem = trackedItem as IDisposable;
                    if (disposableItem != null)
                        disposableItem.Dispose();
                }

                //clear our tracking
                trackedObjects.Clear();
            }
        }

    }
}
