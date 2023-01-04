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

using System;

namespace Paramore.Brighter.Transforms.Storage
{
    /// <summary>
    /// A schema stored in a registry
    /// </summary>
    public class BrighterMessageSchema
    {
        /// <summary>
        /// The schema that we are storing
        /// </summary>
        public string Schema { get; }
        
        /// <summary>
        /// The subject the schema is registered against.
        /// </summary>
        public string Subject { get; }

        /// <summary>
        /// The schema version.
        /// </summary>
        public long Version { get; }

        /// <summary>
        /// Unique identifier of the schema.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// A schema stored in the registry
        /// </summary>
        /// <param name="subject">The subject fo the schema, usually just the topic</param>
        /// <param name="id">The id of the schema in the registry</param>
        /// <param name="version">The version number of the schema for this is and subject</param>
        /// <param name="schema">The schema we want to register</param>
        public BrighterMessageSchema(string subject, int id, long version, string schema)
        {
            Subject = subject;
            Id = id;
            Version = version;
            Schema = schema;
        }
        
        protected bool Equals(BrighterMessageSchema other)
        {
            return Subject == other.Subject && Version == other.Version && Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BrighterMessageSchema)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Subject, Version, Id);
        }

        public static bool operator ==(BrighterMessageSchema left, BrighterMessageSchema right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(BrighterMessageSchema left, BrighterMessageSchema right)
        {
            return !Equals(left, right);
        }
    }
}
