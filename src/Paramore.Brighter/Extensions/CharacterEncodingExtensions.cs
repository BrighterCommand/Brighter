#region Licence

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Extensions;

public static class CharacterEncodingExtensions
{
    public static string? FromCharacterEncoding(this CharacterEncoding characterEncoding) =>
        characterEncoding switch
        {
            CharacterEncoding.ASCII => "us-ascii",
            CharacterEncoding.UTF8 => "utf-8",
            CharacterEncoding.UTF16 => "utf-16",
            CharacterEncoding.Base64 => "base64",
            _ => null
        };

    public static CharacterEncoding ToCharacterEncoding(this string name) =>
        name.ToLowerInvariant() switch
        {
            "us-ascii" => CharacterEncoding.ASCII,
            "utf-8" => CharacterEncoding.UTF8,
            "utf-16" => CharacterEncoding.UTF16,
            "base64" => CharacterEncoding.Base64,
            _ => CharacterEncoding.Raw
        };
}
