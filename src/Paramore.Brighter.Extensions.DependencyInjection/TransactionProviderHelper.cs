#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Linq;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Helper class for extracting transaction type information from transaction providers.
    /// Used to discover the generic type parameter from IAmABoxTransactionProvider{T} implementations.
    /// </summary>
    internal static class TransactionProviderHelper
    {
        /// <summary>
        /// Extracts the transaction type from a transaction provider that implements IAmABoxTransactionProvider{T}.
        /// </summary>
        /// <param name="transactionProviderType">The type of the transaction provider</param>
        /// <returns>The transaction type T, or null if not found</returns>
        internal static Type? GetTransactionType(Type transactionProviderType)
        {
            Type transactionProviderInterface = typeof(IAmABoxTransactionProvider<>);

            foreach (Type i in transactionProviderType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == transactionProviderInterface))
            {
                return i.GetGenericArguments()[0];
            }

            return null;
        }

        /// <summary>
        /// Validates that a transaction provider implements IAmABoxTransactionProvider{T} and returns the transaction type.
        /// </summary>
        /// <param name="transactionProviderType">The type of the transaction provider</param>
        /// <returns>The transaction type T</returns>
        /// <exception cref="ConfigurationException">Thrown if the provider does not implement the required interface</exception>
        internal static Type GetTransactionTypeOrThrow(Type transactionProviderType)
        {
            var transactionType = GetTransactionType(transactionProviderType);

            if (transactionType == null)
            {
                throw new ConfigurationException(
                    $"Unable to register provider of type {transactionProviderType.Name}. " +
                    $"It does not implement {typeof(IAmABoxTransactionProvider<>).Name}.");
            }

            return transactionType;
        }
    }
}
