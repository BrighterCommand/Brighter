using System;
using Paramore.Domain.Common;

namespace Paramore.Domain.Venues
{
    public class PostCode : IEquatable<PostCode>, IAmAValueType<string>
    {
        private readonly string code = string.Empty;

        public PostCode(string code)
        {
            this.code = code;
        }

        public PostCode() {}

        public string Value
        {
            get { return code; }
        }

        public static implicit operator string(PostCode rhs)
        {
            return rhs.code;
        }

        public override string ToString()
        {
            return string.Format("{0}", code);
        }

        public bool Equals(PostCode rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return Equals(rhs.code, code);
        }

        public override bool Equals(object rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            if (rhs.GetType() != typeof (PostCode)) return false;
            return Equals((PostCode) rhs);
        }

        public override int GetHashCode()
        {
            return (code != null ? code.GetHashCode() : 0);
        }

        public static bool operator ==(PostCode left, PostCode right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PostCode left, PostCode right)
        {
            return !Equals(left, right);
        }
    }
}