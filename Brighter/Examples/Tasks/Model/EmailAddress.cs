// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text.RegularExpressions;

namespace Tasks.Model
{
    public class EmailAddress : IEquatable<EmailAddress>
    {
        private readonly string _emailAddress;

        private const string VALID_EMAIL_REGEX = @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z";

        public string Value { get { return _emailAddress; } }

        public EmailAddress(string emailAddress)
        {
            bool isEmail = Regex.IsMatch(emailAddress, VALID_EMAIL_REGEX);
            if (!isEmail)
                throw new ArgumentException("Email address was not valid", "emailAddress");

            _emailAddress = emailAddress;
        }

        public override string ToString()
        {
            return _emailAddress;
        }

        public static implicit operator string (EmailAddress rhs)
        {
            return rhs.Value;
        }

        public bool Equals(EmailAddress other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_emailAddress, other._emailAddress);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EmailAddress)obj);
        }

        public override int GetHashCode()
        {
            return _emailAddress.GetHashCode();
        }

        public static bool operator ==(EmailAddress left, EmailAddress right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(EmailAddress left, EmailAddress right)
        {
            return !Equals(left, right);
        }
    }
}