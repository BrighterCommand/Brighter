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

using System.Collections.Generic;
using paramore.brighter.restms.server.Ports.Common;

namespace paramore.brighter.restms.server.Model
{
    public class Domain : Resource, IAmAnAggregate
    {
        private readonly HashSet<Identity> feeds = new HashSet<Identity>();
        public Title Title { get; private set; }
        public Profile Profile { get; private set; }
        
        public Identity Id { get; private set; }
        public AggregateVersion Version { get; private set; }

        public Domain(Name name, Title title, Profile profile, AggregateVersion version)
        {
            Name = name;
            Title = title;
            Profile = profile;
            Version = version;
        }
        
        protected bool Equals(Domain other)
        {
            return Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Domain) obj);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }

        public static bool operator ==(Domain left, Domain right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Domain left, Domain right)
        {
            return !Equals(left, right);
        }

        public void AddFeed(Identity id)
        {
            feeds.Add(id);
        }
    }
}
