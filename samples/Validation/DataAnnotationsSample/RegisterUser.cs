#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

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

using System.ComponentModel.DataAnnotations;
using Paramore.Brighter;

namespace DataAnnotationsSample;

/// <summary>
/// A request that declares its own validation constraints with DataAnnotations attributes: Name is required
/// and Email must be present and a well-formed address. The provider validates the request against these
/// attributes, so no separate validator type is needed.
/// </summary>
public sealed class RegisterUser() : Command(Id.Random())
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Name must not be empty")]
    public string Name { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Email must not be empty")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address")]
    public string Email { get; set; } = string.Empty;
}
