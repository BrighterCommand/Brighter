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

using FakeItEasy;
using Machine.Specifications;
using Raven.Client;
using Raven.Client.Document;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messagestore.ravendb;

namespace paramore.commandprocessor.tests.MessageStore.RavenDb
{
    class When_installing_the_message_store_into_the_container 
    {
        private static IAdaptAnInversionOfControlContainer container;

        Establish context = () =>
            {
                container = A.Fake<IAdaptAnInversionOfControlContainer>();

            };

        Because of = () => MessageStoreFactory.InstallRavenDbMessageStore(container);

        It should_add_the_document_store_to_the_container = () => A.CallTo(() => container.Register<IDocumentStore, DocumentStore>(new DocumentStore())).WithAnyArguments().MustHaveHappened();
        It should_set_the_global_message_store_document_store = () => MessageStoreFactory.DocumentStore.ShouldNotBeNull();
        It should_use_a_ravendb_document_store = () => MessageStoreFactory.DocumentStore.ShouldNotBeOfExactType<IDocumentStore>();
    }
}
