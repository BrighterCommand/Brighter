// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 25-03-2014
//
// Last Modified By : ian
// Last Modified On : 25-03-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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
using System.Linq;
using Machine.Specifications;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports
{
    [Subject(typeof(MessageStoreActivationStateListViewModelRetriever))]
    public class MessageStoreActivationStateListViewModelRetrieverTests
    {
        public class When_retrieving_json_for_valid_list
        {
            private Establish _context = () =>
            {
                _sqlMessageStores = new List<MessageStoreActivationState>
                {
                    MessageStoreActivationStateFactory.Create("sqlce", typeof(MsSqlMessageStore).FullName, "DataSource='test.sdf';", "table2"),
                    MessageStoreActivationStateFactory.Create("sql2008", typeof(MsSqlMessageStore).FullName, "Server=.;Database=aMessageStore;Trusted_Connection=True", "table1"),
                };
                var fakeStoreListProvider = new FakeMessageStoreActivationStateProvider(_ravenMessageStores.Union(_sqlMessageStores));

                _messageStoreActivationStateListViewModelRetriever = new MessageStoreActivationStateListViewModelRetriever(fakeStoreListProvider);
            };
                
            private Because _of_GET = () => _result = _messageStoreActivationStateListViewModelRetriever.Get();

            private It should_return_StoresListModel = () =>
            {
                var model = _result.Result;
                model.ShouldNotBeNull();
            };
            private It should_return_SqlStores_in_ListModel = () =>
            {
                var model = _result.Result;
                AssertStoreItems(model, _sqlMessageStores);
            };

            private It should_return_RavenStores_in_ListModel = () =>
            {
                var model = _result.Result;
                model.ShouldNotBeNull();
                AssertStoreItems(model, _ravenMessageStores);
            };

            private static void AssertStoreItems(MessageStoreActivationStateListModel model, IEnumerable<MessageStoreActivationState> messageStoreListItems)
            {
                foreach (var messageStoreListItem in messageStoreListItems)
                {
                    var foundStore = model.Stores.Single(s => s.Name == messageStoreListItem.Name);
                    foundStore.Name.ShouldEqual(messageStoreListItem.Name);
                    foundStore.ConnectionString.ShouldEqual(messageStoreListItem.ConnectionString);
                    foundStore.TableName.ShouldEqual(messageStoreListItem.TableName);
                    foundStore.TypeName.ShouldEqual(messageStoreListItem.TypeName);
                    foundStore.Name.ShouldEqual(messageStoreListItem.Name);
                }
            }

            private static List<MessageStoreActivationState> _sqlMessageStores;
            private static MessageStoreActivationStateListViewModelRetriever _messageStoreActivationStateListViewModelRetriever;
            private static List<MessageStoreActivationState> _ravenMessageStores;
            private static ViewModelRetrieverResult<MessageStoreActivationStateListModel, MessageStoreActivationStateListModelError> _result;
        }

        public class When_retrieving_list_with_invalid_config
        {
            private Establish _context = () =>
            {
                var fakeStoreListProvider = new FakeMessageStoreActivationStateProviderExceptionOnGet();
                _messageStoreActivationStateListViewModelRetriever = new MessageStoreActivationStateListViewModelRetriever(fakeStoreListProvider);
            };

            private Because _of_GET = () => _result = _messageStoreActivationStateListViewModelRetriever.Get();

            private It should_return_error = () =>
            {
                var model = _result.Result;
                model.ShouldBeNull();
                _result.IsError.ShouldBeTrue();
                _result.Error.ShouldEqual(MessageStoreActivationStateListModelError.GetActivationStateFromConfigError);
                _result.Exception.ShouldNotBeNull();
            };

            private static MessageStoreActivationStateListViewModelRetriever _messageStoreActivationStateListViewModelRetriever;
            private static ViewModelRetrieverResult<MessageStoreActivationStateListModel, MessageStoreActivationStateListModelError> _result;
        }
    }
}