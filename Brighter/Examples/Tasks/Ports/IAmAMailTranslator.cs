// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using SendGrid;
using Tasks.Model;

namespace Tasks.Ports
{
    public interface IAmAMailTranslator
    {
        Mail Translate(TaskReminder taskReminder);
    }
}