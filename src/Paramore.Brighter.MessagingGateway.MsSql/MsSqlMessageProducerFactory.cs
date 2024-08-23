#region Licence
/* The MIT License (MIT)
Copyright © 2024 Dominic Hickie <dominichickie@gmail.com>

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
using System.Collections.Generic;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly MsSqlConfiguration _msSqlConfiguration;
        private readonly IEnumerable<Publication> _publications;

        /// <summary>
        /// Creates a collection of MsSQL message producers from the MsSQL publication information
        /// </summary>
        /// <param name="msSqlConfiguration">The connection to use to connect to MsSQL</param>
        /// <param name="publications">The publications describing the MySQL topics that we want to use</param>
        public MsSqlMessageProducerFactory(
            MsSqlConfiguration msSqlConfiguration,
            IEnumerable<Publication> publications)
        {
            _msSqlConfiguration = 
                msSqlConfiguration ?? throw new ArgumentNullException(nameof(msSqlConfiguration));
            if (string.IsNullOrEmpty(msSqlConfiguration.QueueStoreTable))
                throw new ArgumentNullException(nameof(msSqlConfiguration.QueueStoreTable));
            _publications = publications;
        }

        /// <inheritdoc />
        public Dictionary<string,IAmAMessageProducer> Create()
        {
            var producers = new Dictionary<string, IAmAMessageProducer>();

            foreach (var publication in _publications)
            {
                producers[publication.Topic] = new MsSqlMessageProducer(_msSqlConfiguration, publication);
            }

            return producers;
        }
    }
}
