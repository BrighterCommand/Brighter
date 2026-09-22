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
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageProducerFactory : MsSqlMessagingGateway, IAmAMessageProducerFactory
    {
        private readonly RelationalDatabaseConfiguration _msSqlConfiguration;
        private readonly IEnumerable<Publication> _publications;

        /// <summary>
        /// Creates a collection of MsSQL message producers from the MsSQL publication information
        /// </summary>
        /// <param name="msSqlConfiguration">The connection to use to connect to MsSQL</param>
        /// <param name="publications">The publications describing the MySQL topics that we want to use</param>
        public MsSqlMessageProducerFactory(
            RelationalDatabaseConfiguration msSqlConfiguration,
            IEnumerable<Publication> publications)
            : base(msSqlConfiguration ?? throw new ArgumentNullException(nameof(msSqlConfiguration)))
        {
            _msSqlConfiguration = 
                msSqlConfiguration ?? throw new ArgumentNullException(nameof(msSqlConfiguration));
            if (string.IsNullOrEmpty(msSqlConfiguration.QueueStoreTable))
                throw new ArgumentNullException(nameof(msSqlConfiguration.QueueStoreTable));
            _publications = publications;
        }

        /// <summary>
        /// Creates a dictionary of in-memory message producers.
        /// </summary>
        /// <returns>A dictionary of <see cref="IAmAMessageProducer"/> indexed by <see cref="RoutingKey"/></returns>
        /// <exception cref="ArgumentException">Thrown when a publication does not have a topic</exception>
        public Dictionary<ProducerKey,IAmAMessageProducer> Create()
        {
            var producers = new Dictionary<ProducerKey, IAmAMessageProducer>();

            foreach (var publication in _publications)
            {
                RequireTopic(publication);

                //A sender may be the first thing to run against a new database, so the producer
                //side provisions too rather than waiting for a consumer to have done it.
                EnsureQueueStoreExists(publication.MakeChannels);

                AddProducer(producers, publication);
            }

            return producers;
        }

        /// <summary>
        /// Creates a dictionary of in-memory message producers.
        /// </summary>
        /// <returns>A dictionary of <see cref="IAmAMessageProducer"/> indexed by <see cref="RoutingKey"/></returns>
        /// <exception cref="ArgumentException">Thrown when a publication does not have a topic</exception>
        /// <remarks>
        /// Written out rather than wrapping Create() in a completed task: this is the path the
        /// producer registry takes, and provisioning opens a connection and runs a DDL batch. Under
        /// Task.FromResult(Create()) that I/O ran synchronously on an async startup path.
        /// </remarks>
        public async Task<Dictionary<ProducerKey, IAmAMessageProducer>> CreateAsync()
        {
            var producers = new Dictionary<ProducerKey, IAmAMessageProducer>();

            foreach (var publication in _publications)
            {
                RequireTopic(publication);

                await EnsureQueueStoreExistsAsync(publication.MakeChannels);

                AddProducer(producers, publication);
            }

            return producers;
        }

        //The two bodies above are the house style here — PostgresMessageProducerFactory duplicates
        //its pair the same way — but everything except the one differing call lives in these two,
        //so the surface on which they can silently drift is that call and the await.
        private static void RequireTopic(Publication publication)
        {
            if (publication.Topic is null)
                throw new ConfigurationException("MS SQL Message Producer Factory: Topic is missing from the publication");
        }

        private void AddProducer(
            Dictionary<ProducerKey, IAmAMessageProducer> producers, Publication publication)
        {
            var producer = new MsSqlMessageProducer(_msSqlConfiguration, publication);
            producer.Publication = publication;
            var producerKey = new ProducerKey(publication.Topic!, publication.Type);
            if (producers.ContainsKey(producerKey))
                throw new ConfigurationException($"MS SQL Message Producer Factory: A publication with the topic {publication.Topic} and {publication.Type} already exists in the producer registry. Each topic + type must be unique in the producer registry. If you did not set a type, we will match against an empty type, so you cannot have two publications with the same topic and no type in the producer registry.");
            producers[producerKey] = producer;
        }
    }
}
