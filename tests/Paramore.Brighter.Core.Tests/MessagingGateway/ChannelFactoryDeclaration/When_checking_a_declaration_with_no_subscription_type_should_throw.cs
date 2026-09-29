#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using Paramore.Brighter.Core.Tests.MessagingGateway.ChannelFactoryDeclaration.TestDoubles;





namespace Paramore.Brighter.Core.Tests.MessagingGateway.ChannelFactoryDeclaration;



public class ChannelFactoryDeclarationNoSubscriptionTypeTests

{

    [Test]

    public async System.Threading.Tasks.Task When_checking_a_declaration_with_no_subscription_type_should_throw()

    {

        // Arrange

        // Evident Data: subscriptionType is literally null; declaredFactoryType is a sound declaration,

        // so only the null subscriptionType can be responsible for the exception

        var declaredFactoryType = typeof(SoundChannelFactory);



        // Act & Assert

        await Assert.That(() => Paramore.Brighter.SubscriptionChannelFactoryDeclaration.Check(null!, declaredFactoryType)).ThrowsExactly<ArgumentNullException>();

    }

}
