using System;
using System.Collections.Generic;
using Microsoft.Practices.ServiceLocation;

namespace paramore.commandprocessor
{
    public abstract class TrackedServiceLocator : ServiceLocatorImplBase, IManageLifetimes
    {
        private LifetimeScope lifetime;

        //When we pass the instance out, we only expose the IDisposable, all a consumer can do is choose to release
        public IDisposable CreateLifetime()
        {
            lifetime = new LifetimeScope();
            return lifetime;
        }

        public int TrackedItemCount
        {
            get { return lifetime.TrackedItemCount; }
        }

        protected virtual void TrackItem(object instance)
        {
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
