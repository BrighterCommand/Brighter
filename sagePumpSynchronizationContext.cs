[33mcommit c9cff972c5742e5f25a4315b4ed063f488411ddd[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Dec 30 22:46:28 2016 +0000

    split python projects from paramore.brighter

 delete mode 100644 Brighter/.gitignore
 delete mode 100644 Brighter/.nuget/packages.config
 delete mode 100644 Brightmgmnt/MANIFEST.in
 delete mode 100644 Brightmgmnt/README.rst
 delete mode 100644 Brightmgmnt/brightmgmnt/__init__.py
 delete mode 100644 Brightmgmnt/brightmgmnt/__main__.py
 delete mode 100644 Brightmgmnt/brightmgmnt/__pycache__/__init__.cpython-34.pyc
 delete mode 100644 Brightmgmnt/brightmgmnt/configuration.py
 delete mode 100644 Brightmgmnt/brightmgmnt/messaging.py
 delete mode 100644 Brightmgmnt/brightmgmnt/publisher.py
 delete mode 100644 Brightmgmnt/cfg/brightmgmnt.ini
 delete mode 100644 Brightmgmnt/env/Lib/no-global-site-packages.txt
 delete mode 100644 Brightmgmnt/ez_setup.py
 delete mode 100644 Brightmgmnt/pycharm_debug.py
 delete mode 100644 Brightmgmnt/setup.cfg
 delete mode 100644 Brightmgmnt/setup.py
 delete mode 100644 Brightmntr/MANIFEST.in
 delete mode 100644 Brightmntr/README.rst
 delete mode 100644 Brightmntr/RunMe.Txt
 delete mode 100644 Brightmntr/brightmntr/__init__.py
 delete mode 100644 Brightmntr/brightmntr/__main__.py
 delete mode 100644 Brightmntr/brightmntr/__pycache__/configuration.cpython-34.pyc
 delete mode 100644 Brightmntr/brightmntr/configuration.py
 delete mode 100644 Brightmntr/brightmntr/worker.py
 delete mode 100644 Brightmntr/cfg/brightmntr.ini
 delete mode 100644 Brightmntr/pycharm_debug.py
 delete mode 100644 Brightmntr/setup.cfg
 delete mode 100644 Brightmntr/setup.py
 delete mode 100644 Brightside/README.rst
 delete mode 100644 Brightside/arame/__init__.py
 delete mode 100644 Brightside/arame/gateway.py
 delete mode 100644 Brightside/arame/messaging.py
 delete mode 100644 Brightside/core/__init__.py
 delete mode 100644 Brightside/core/channels.py
 delete mode 100644 Brightside/core/command_processor.py
 delete mode 100644 Brightside/core/exceptions.py
 delete mode 100644 Brightside/core/handler.py
 delete mode 100644 Brightside/core/log_handler.py
 delete mode 100644 Brightside/core/messaging.py
 delete mode 100644 Brightside/core/registry.py
 delete mode 100644 Brightside/manifest.in
 delete mode 100644 Brightside/serviceactivator/__init__.py
 delete mode 100644 Brightside/serviceactivator/message_pump.py
 delete mode 100644 Brightside/setup.cfg
 delete mode 100644 Brightside/setup.py
 delete mode 100644 Brightside/tests/__init__.py
 delete mode 100644 Brightside/tests/arame_gateway_tests.py
 delete mode 100644 Brightside/tests/channel_tests.py
 delete mode 100644 Brightside/tests/channels_testdoubles.py
 delete mode 100644 Brightside/tests/command_processor_tests.py
 delete mode 100644 Brightside/tests/handlers_testdoubles.py
 delete mode 100644 Brightside/tests/logging_and_monitoring_tests.py
 delete mode 100644 Brightside/tests/message_pump_doubles.py
 delete mode 100644 Brightside/tests/message_pump_tests.py
 delete mode 100644 Brightside/tests/messaging_testdoubles.py
 delete mode 100644 Brightside/tests/post_to_producer_tests.py
 delete mode 100644 Brightside/tests/retry_and_cicruit_breaker_tests.py

[33mcommit 8c65001087a0d3745e3d5217b6b2449c4198ec99[m
Author: Daniel Stockhammer <daniel.stockhammer@huddle.com>
Date:   Wed Dec 21 15:03:20 2016 +0000

    add internal liblog (4.2.6) to all assemblies that require logging and remove ILog from all public interfaces

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests.nunit/CommandProcessors/TestDoubles/FakeLogProvider.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests.nunit/CommandProcessors/TestDoubles/MyLogWritingCommand.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests.nunit/CommandProcessors/TestDoubles/MyLogWritingCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests.nunit/CommandProcessors/When_building_a_command_processor_with_a_logProvider.cs

[33mcommit 3f360f9dea27e071c1dd94d16a0f82b58b927465[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Tue Dec 20 12:33:18 2016 +0000

    More updates to project.json for nuget packages

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.eventstore/paramore.brighter.commandprocessor.messagestore.eventstore.v2.ncrunchproject
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.mssql/paramore.brighter.commandprocessor.messagestore.mssql.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.mssql/paramore.brighter.commandprocessor.messagestore.mssql.v2.ncrunchproject

[33mcommit d4b38516527f28903f271469b6d30567db8a50ea[m
Author: icooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Dec 9 19:28:59 2016 +0000

    [Birghtside] working through message pump tests[skip ci]

 delete mode 100644 Brightside/core/dumper.py

[33mcommit fcf1e46b77fe9e324a8b9a48be07916f8b2cb88f[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Dec 8 22:42:02 2016 +0000

    [Brightside] Fix assertions on logging [skip ci]

 delete mode 100644 Brightside/core/logging.py

[33mcommit c75e98846aacd1243f2c9d7dc331ac1f7cc580b1[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Mon Nov 21 15:03:55 2016 +0000

    Removes old sqlce code

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.commandstore.mssql/DDL Scripts/SQLCE/CommandStore.sqlce
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.commandstore.mssql/DDL Scripts/SQLite/CommandStore.sql

[33mcommit 0e6d10ba8d0787bc736426147ee0d067c3b96bee[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Mon Nov 21 14:14:12 2016 +0000

    Removes configuration from Service Activator

 delete mode 100644 Brighter/paramore.brighter.serviceactivator/Configuration/ConnectionElement.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/Configuration/ConnectionFactory.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/Configuration/Subscriptions.cs

[33mcommit 8708611a7f2c7c886d96aa3961f91deddad9234a[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Tue Nov 8 12:29:52 2016 +0000

    cleaning up crap

 delete mode 100644 Brighter/Examples/EventSourcing/App.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/PortabilityAnalysis(1).html
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/PortabilityAnalysis(2).html
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/PortabilityAnalysis.html

[33mcommit d855e8ffb84eeaccfc0b42594710beb467ff3e57[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Wed Oct 26 18:21:28 2016 +0100

    Add Tasks example back into solution and tests passing

 delete mode 100644 Brighter/Examples/Tasks/Adapters/Tests/TaskCommandHandlersFixture.cs
 delete mode 100644 Brighter/Examples/Tasks/Adapters/Tests/TaskDAOFixture.cs
 delete mode 100644 Brighter/Examples/Tasks/Adapters/Tests/TaskMailerServiceFixture.cs

[33mcommit d3ed589cc24cc70d5aa365bccb43432cc57a6022[m
Author: ian <ian@huddle.com>
Date:   Tue Oct 25 15:46:09 2016 +0100

    clean up some test issues

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests.nunit/MessageDispatch/When_configuring_a_message_dispatcher.cs

[33mcommit e6ff9e673e8894223eb7665fdcdc77681efadb18[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Tue Oct 25 13:58:22 2016 +0100

    Fixes MS SQL Tests

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.mssql/DDL Scripts/SQLCE/CreateMessageStore.sqlce
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.mssql/DDL Scripts/SQLite/MessageStore.sql

[33mcommit c342c16253efa36491bdde39f7c6f0d06e0661bc[m
Author: ian <ian@huddle.com>
Date:   Wed Oct 12 15:27:38 2016 +0100

    clear the backup files

 delete mode 100644 Brighter/Examples/EventSourcing/EventSourcing.csproj.bak
 delete mode 100644 Brighter/Examples/EventSourcing/packages.config.bak
 delete mode 100644 Brighter/Examples/GenericListener/App.config.bak
 delete mode 100644 Brighter/Examples/GenericListener/GenericListener.csproj.bak
 delete mode 100644 Brighter/Examples/GenericListener/packages.config.bak
 delete mode 100644 Brighter/Examples/Greetings/Greetings.csproj.bak
 delete mode 100644 Brighter/Examples/Greetings/Ports/App.config.bak
 delete mode 100644 Brighter/Examples/Greetings/packages.config.bak
 delete mode 100644 Brighter/Examples/HelloAsyncListeners/App.config.bak
 delete mode 100644 Brighter/Examples/HelloAsyncListeners/HelloAsyncListeners.csproj.bak
 delete mode 100644 Brighter/Examples/HelloAsyncListeners/packages.config.bak
 delete mode 100644 Brighter/Examples/HelloWorldAsync/HelloWorldAsync.csproj.bak
 delete mode 100644 Brighter/Examples/HelloWorldAsync/packages.config.bak
 delete mode 100644 Brighter/Examples/ManagementAndMonitoring/ManagementAndMonitoring.csproj.bak
 delete mode 100644 Brighter/Examples/ManagementAndMonitoring/packages.config.bak
 delete mode 100644 Brighter/Examples/Tasks/Tasks.csproj.bak
 delete mode 100644 Brighter/Examples/Tasks/packages.config.bak
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/obj/Debug/net452/.IncrementalCache
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/obj/Debug/net452/.SDKVersion
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/obj/Debug/net452/dotnet-compile-csc.rsp
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/obj/Debug/net452/dotnet-compile.assemblyinfo.cs
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/obj/Debug/net452/dotnet-compile.rsp
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/obj/Debug/netstandard1.6/.IncrementalCache
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/obj/Debug/netstandard1.6/.SDKVersion
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/obj/Debug/netstandard1.6/dotnet-compile-csc.rsp
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/obj/Debug/netstandard1.6/dotnet-compile.assemblyinfo.cs
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/obj/Debug/netstandard1.6/dotnet-compile.rsp
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/net452/.IncrementalCache
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/net452/.SDKVersion
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/net452/dotnet-compile-csc.rsp
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/net452/dotnet-compile.assemblyinfo.cs
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/net452/dotnet-compile.rsp
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/netstandard1.6/.IncrementalCache
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/netstandard1.6/.SDKVersion
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/netstandard1.6/dotnet-compile-csc.rsp
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/netstandard1.6/dotnet-compile.assemblyinfo.cs
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/netstandard1.6/dotnet-compile.rsp
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.commandstore.mssql/packages.config.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.commandstore.mssql/paramore.brighter.commandprocessor.commandstore.mssql.csproj.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.eventstore/packages.config.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.eventstore/paramore.brighter.commandprocessor.messagestore.eventstore.csproj.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.mssql/packages.config.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.mssql/paramore.brighter.commandprocessor.messagestore.mssql.csproj.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messageviewer/packages.config.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messageviewer/paramore.brighter.commandprocessor.messageviewer.csproj.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.awssqs/packages.config.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.awssqs/paramore.brighter.commandprocessor.messaginggateway.awssqs.csproj.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.azureservicebus/packages.config.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.azureservicebus/paramore.brighter.commandprocessor.messaginggateway.azureservicebus.csproj.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.restms/packages.config.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.restms/paramore.brighter.commandprocessor.messaginggateway.restms.csproj.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/packages.config.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/paramore.brighter.commandprocessor.messaginggateway.rmq.csproj.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/packages.config.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/paramore.brighter.commandprocessor.csproj.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/paramore.brighter.commandprocessor.nuspec.bak
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/packages.config.bak
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/paramore.brighter.serviceactivator.csproj.bak

[33mcommit 053cf16f3aa822eb15fb003d652b76d1f3838612[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Wed Oct 12 11:22:05 2016 +0100

    updated gitignore, removed project.lock.json in lib folder

 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/project.lock.json
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/project.lock.json

[33mcommit ddf9b8f1b80fcda7e32c11d4fa07a20f4362b772[m
Author: ian.pender <ian.pender@huddle.com>
Date:   Wed Oct 12 11:24:02 2016 +0100

    Fix for tests dupe and viewer test proj.json issue

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/.gitignore
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ArrayCache.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/MyEventHandlerAsyncWithContinuation.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/FakeErroringMessageProducer.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/FakeLogProvider.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/FakeMessageProducer.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/FakeMessageStore.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/FakeRepository.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/FakeSession.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/IAmAnAggregate.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/IRepository.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/IUnitOfWork.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyAbortingHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyAbortingHandlerAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyAggregate.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyCancellableCommandHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyCommand.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyCommandHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyCommandMessageMapper.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyContextAwareCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyDependentCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyDoubleDecoratedHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyDoubleDecoratedHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyEvent.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyEventHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyEventHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyImplicitHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyImplicitHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyLogWritingCommand.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyLogWritingCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyLoggingHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyLoggingHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyLoggingHandlerAsyncAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyLoggingHandlerAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyMixedImplicitHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyMixedImplicitHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyObsoleteCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyObsoleteCommandHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyOtherEventHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyOtherEventHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyPostLoggingHandlerAsyncAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyPostLoggingHandlerAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyPreAndPostDecoratedHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyPreAndPostDecoratedHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyPreValidationHandlerAsyncAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyPreValidationHandlerAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyThrowingEventHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyThrowingEventHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyUnUsedCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyValidationHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyValidationHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyValidationHandlerAsyncAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyValidationHandlerAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/TestHandlerFactory.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/TestHandlerFactoryAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/TinyIoCMessageMapperFactory.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/TinyIocHandlerFactory.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/TinyIocHandlerFactoryAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_A_Handler_Is_Part_Of_An_Async_Pipeline.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_A_Handler_Is_Part_of_A_Pipeline.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Building_A_Handler_For_A_Command.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Building_A_Pipeline_Allow_ForiegnAttribues.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Building_A_Pipeline_Allow_Pre_And_Post_Tasks.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Building_A_Pipeline_Preserve_The_Order.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Building_A_Sync_Pipeline_That_Has_Async_Handlers.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Building_An_Async_Pipeline_Allow_ForiegnAttribues.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Building_An_Async_Pipeline_Allow_Pre_And_Post_Tasks.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Building_An_Async_Pipeline_Preserve_The_Order.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Building_An_Async_Pipeline_That_Has_Sync_Handlers.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Building_With_A_Default_Policy_Sufficient_To_Post.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Cancelling_An_Async_Command.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Finding_A_Hander_That_Has_Dependencies.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Finding_A_Handler_For_A_Command.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_A_Message_And_There_Is_No_Message_Mapper_Registry.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_A_Message_And_There_Is_No_Message_Mapper_Registry_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_A_Message_And_There_Is_No_Message_Producer.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_A_Message_And_There_Is_No_Message_Producer_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_A_Message_And_There_Is_No_Message_Store.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_A_Message_And_There_Is_No_Message_Store_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_A_Message_To_The_Command_Processor.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_A_Message_To_The_Command_Processor_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_Via_A_Control_Bus_Sender.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_Via_A_Control_Bus_Sender_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_With_An_In_Memory_Message_Store.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Posting_With_An_In_Memory_Message_Store_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Publishing_To_Multiple_Subscribers_Should_Aggregate_Exceptions_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_Sending_A_Command_To_The_Processor_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_There_Are_Multiple_Possible_Command_Handlers_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_There_Are_Multiple_Subscribers_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_There_Are_No_Command_Handlers_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_There_Are_No_Subscribers_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_There_Is_No_Handler_Factory_On_A_Publish.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_There_Is_No_Handler_Factory_On_A_Publish_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_There_Is_No_Handler_Factory_On_A_Send.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_There_Is_No_Handler_Factory_On_A_Send_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_an_error_should_break_the_circuit.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_an_exception_is_thrown_terminate_the_pipeline.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_building_a_command_processor_with_a_logProvider.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_building_a_handler_for_an_async_command.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_publishing_an_event_to_the_processor.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_publishing_to_multiple_subscribers_should_aggregate_exceptions.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_putting_a_variable_into_the_bag_should_be_accessible_in_the_handler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_sending_a_command_to_the_processor.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_there_are_multiple_possible_command_handlers.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_there_are_multiple_subscribers.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_there_are_no_command_handlers.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_there_are_no_failures_execute_all_the_steps_in_the_pipeline.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_there_are_no_subscribers.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/When_we_have_exercised_the_pipeline_cleanup_its_handlers.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandStore/MsSsql/DatabaseHelper.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandStore/MsSsql/When_The_Message_Is_Already_In_The_Command_Store_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandStore/MsSsql/When_There_Is_No_Message_In_The_Sql_Command_Store_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandStore/MsSsql/When_Writing_A_Message_To_The_Command_Store_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandStore/MsSsql/When_the_message_is_already_in_the_command_store.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandStore/MsSsql/When_there_is_no_message_in_the_sql_command_store.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandStore/MsSsql/When_writing_a_message_to_the_command_store.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_configuring_a_control_bus.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_creating_a_control_bus_sender.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_mapping_from_a_configuration_command_from_a_message.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_mapping_from_a_heartbeat_reply_to_a_message.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_mapping_from_a_heartbeat_request_to_a_message.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_mapping_from_a_message_to_a_heartbeat_reply.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_mapping_from_a_message_to_a_heartbeat_request.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_mapping_to_a_wire_message_from_a_configuration_command.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_receiving_a_start_message_for_a_connection.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_receiving_a_stop_message_for_a_connection.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_receiving_an_all_start_message.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_receiving_an_all_stop_message.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_recieving_a_heartbeat_message.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/When_we_build_a_control_bus_we_can_send_configuration_messages_to_it.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/EventSourcing/TestDoubles/MyStoredCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/EventSourcing/TestDoubles/MyStoredCommandHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/EventSourcing/When_Handling_A_Command_With_A_Command_Store_Enabled_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/EventSourcing/When_handling_a_command_with_a_command_store_enabled.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/MyFailsWithDivideByZeroHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/TestDoubles/MyDoesNotFailPolicyHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/TestDoubles/MyDoesNotFailPolicyHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/TestDoubles/MyFailsWithDivideByZeroHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/TestDoubles/MyFailsWithFallbackBrokenCircuitHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/TestDoubles/MyFailsWithFallbackDivideByZeroHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/TestDoubles/MyFailsWithFallbackDivideByZeroHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/TestDoubles/MyFailsWithFallbackMultipleHandlers.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/TestDoubles/MyFailsWithUnsupportedExceptionForFallback.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/When_A_Fallback_Is_Broken_Ciruit_Only.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/When_Raising_A_Broken_Circuit_Exception_Can_Fallback.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/When_Raising_An_Exception_Can_Fallback.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/When_Raising_An_Exception_Run_Fallback_Chain.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/When_Sending_A_Command_And_The_Policy_Is_Not_In_The_Registry.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/When_Sending_A_Command_And_The_Policy_Is_Not_In_The_Registry_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/When_Sending_A_Command_That_Passes_Policy_Check.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/When_Sending_A_Command_That_Repeatedely_Fails_Break_The_Circuit.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/When_Sending_A_Command_That_Repeatedely_Fails_Break_The_Circuit_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/When_Sending_A_Command_That_Should_Retry_Failure.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/When_Sending_A_Command_That_Should_Retry_Failure_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/GlobalSuppressions.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Logging/TestDoubles/MyLoggedHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Logging/TestDoubles/MyLoggedHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Logging/TestDoubles/SpyLog.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Logging/When_A_Request_Logger_Is_In_The_Pipeline.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Logging/When_A_Request_Logger_Is_In_The_Pipeline_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/TestDoubles/FailingChannel.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/TestDoubles/FailingEventMessageMapper.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/TestDoubles/MyEventMessageMapper.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/TestDoubles/SpyCommandProcessor.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_A_Message_Dispatcher_Is_Asked_To_Connect_A_Channel_And_Handler_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_A_Message_Is_Dispatched_It_Should_Reach_A_Handler_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_Building_A_Dispatcher_With_Named_Gateway.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_channel_failure_exception_is_thrown_for_command_should_retry_until_connection_re_established.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_channel_failure_exception_is_thrown_for_event_should_retry_until_connection_re_established.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_message_dispatcher_has_a_new_connection_added_while_running.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_message_dispatcher_is_asked_to_connect_a_channel_and_handler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_message_dispatcher_restarts_a_connection.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_message_dispatcher_restarts_a_connection_after_all_connections_have_stopped.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_message_dispatcher_shuts_a_connection.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_message_dispatcher_starts_different_types_of_performers.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_message_dispatcher_starts_multiple_performers.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_message_fails_to_be_mapped_to_a_request.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_message_fails_to_be_mapped_to_a_request_and_the_unacceptable_message_limit_is_reached.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_message_is_dispatched_it_should_reach_a_handler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_requeue_count_threshold_for_commands_has_been_reached.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_requeue_count_threshold_for_events_has_been_reached.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_requeue_of_command_exception_is_thrown.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_a_requeue_of_event_exception_is_thrown.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_an_unacceptable_message_is_recieved.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_an_unacceptable_message_limit_is_reached.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_building_a_dispatcher.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_configuring_a_message_dispatcher.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_reading_a_message_from_a_channel_pump_out_to_command_processor.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/When_running_a_message_pump_on_a_thread_should_be_able_to_stop.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/EventStore/When_There_Is_No_Message_In_The_Message_Store.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/EventStore/When_There_Is_No_Message_In_The_Message_Store_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/EventStore/When_Writing_Messages_To_The_Message_Store.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/EventStore/When_Writing_Messages_To_The_Message_Store_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/MsSql/When_The_Message_Is_Already_In_The_Message_Store.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/MsSql/When_The_Message_Is_Already_In_The_Message_Store_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/MsSql/When_There_Are_Multiple_Messages_In_The_Message_Store_And_A_Range_Is_Fetched.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/MsSql/When_There_Are_Multiple_Messages_In_The_Message_Store_And_A_Range_Is_Fetched_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/MsSql/When_There_Is_No_Message_In_The_Sql_Message_Store.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/MsSql/When_There_Is_No_Message_In_The_Sql_Message_Store_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/MsSql/When_Writing_A_Message_To_The_Message_Store.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/MsSql/When_Writing_A_Message_To_The_Message_Store_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/MsSql/When_Writing_Messages_To_The_Message_Store.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/MsSql/When_Wrting_Messages_To_The_Message_Store_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/NoOpStore/When_reading_from_noopstore.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/NoOpStore/When_writing_to_noopstore.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/Sqlite/SqlMessageStoreMigrationTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/TestDoubles/TestDoubleRmqMessageConsumer.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/When_a_stop_message_is_added_to_a_channel.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/When_acknowledge_is_called_on_a_channel.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/When_disposing_input_channel.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/When_listening_to_messages_on_a_channel.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/When_no_acknowledge_is_called_on_a_channel.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/When_requeuing_a_message_with_no_delay.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/When_requeuing_a_message_with_supported_and_enabled_delay.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/When_requeuing_a_message_with_supported_but_disabled_delay.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/When_requeuing_a_message_with_unsupported_delay.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/awssqs/TestAWSQueueListener.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/awssqs/When_posting_a_message_via_the_messaging_gateway.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/awssqs/When_posting_a_message_via_the_messaging_gateway_and_sns_topic_does_not_exist.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/awssqs/When_purging_the_queue.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/awssqs/When_reading_a_message_via_the_messaging_gateway.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/awssqs/When_rejecting_a_message_through_gateway_with_requeue.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/awssqs/When_rejecting_a_message_through_gateway_without_requeue.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/awssqs/When_requeueing_a_message.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/restms/When_parsing_a_restMS_domain.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/restms/When_posting_a_message_via_the_messaging_gateway.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/rmq/TestHelpers.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/rmq/When_a_message_consumer_throws_an_already_closed_exception_when_connecting_should_retry_until_circuit_breaks.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/rmq/When_a_message_consumer_throws_an_not_supported_exception_when_connecting_should_retry_until_circuit_breaks.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/rmq/When_a_message_consumer_throws_an_operation_interrupted_exception_when_connecting_should_retry_until_circuit_breaks.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/rmq/When_multiple_threads_try_to_post_a_message_at_the_same_time.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/rmq/When_posting_a_message_via_a_named_messaging_gateway.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/rmq/When_posting_a_message_via_the_messaging_gateway.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/rmq/When_reading_a_delayed_message_via_the_messaging_gateway.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Monitoring/TestDoubles/MyMonitoredHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Monitoring/TestDoubles/MyMonitoredHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Monitoring/TestDoubles/MyMonitoredHandlerThatThrows.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Monitoring/TestDoubles/MyMonitoredHandlerThatThrowsAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Monitoring/TestDoubles/SpyControlBusSender.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Monitoring/When_Monitoring_Is_On_For_A_Handler_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Monitoring/When_Monitoring_We_Should_Record_But_Rethrow_Exceptions_Async.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Monitoring/When_monitoring_is_on_for_a_handler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Monitoring/When_monitoring_we_should_record_but_rethrow_exceptions.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Monitoring/When_serializing_a_monitoring_event.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Properties/AssemblyInfo.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/README.md
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Timeout/Test Doubles/MyFailsDueToTimeoutHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Timeout/Test Doubles/MyPassesTimeoutHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Timeout/When_sending_a_command_to_the_processor_failing_a_timeout_policy_check.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/Timeout/When_sending_a_command_to_the_processor_passing_a_timeout_policy_check.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/TinyIoC.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/paramore.brighter.commandprocessor.tests.v2.ncrunchproject
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/paramore.brighter.commandprocessor.tests.xproj
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/project.json

[33mcommit 1dbf94ac56901f8c274b43d6f3345fbc8d889672[m
Author: ian <ian@huddle.com>
Date:   Wed Oct 12 14:27:42 2016 +0100

    remove version

 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/net452/nunit.framework.dll
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/netstandard1.6/NUnit.Specifications.NUnit3.coreCLR.deps.json
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/net452/nunit.framework.dll
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/netstandard1.6/nUnitShouldAdapter.deps.json

[33mcommit 6eddfcaf2c9d7a42c73db37e5637c49fa0218546[m
Author: ian.pender <ian.pender@huddle.com>
Date:   Wed Oct 12 10:10:18 2016 +0100

    Sqllite MessageStore/CommandStore self contained, closes connections

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests.nunit/CommandStore/DatabaseHelper.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests.nunit/CommandStore/Sqlite/SqlLiteTestHelper.cs

[33mcommit d93297591a8e36bc51deab5938155e70f4bad293[m
Author: ian.pender <ian.pender@huddle.com>
Date:   Thu Sep 29 09:09:00 2016 +0100

    Change viewer tests to 4.6

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/packages.config.bak
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/paramore.brighter.commandprocessor.viewer.tests.csproj.bak

[33mcommit 7df158794a9d95dfccf4bbd3b0b88c55f4538ab4[m
Author: ian.pender <ian.pender@huddle.com>
Date:   Wed Sep 28 16:42:21 2016 +0100

    Remove orig merge files

 delete mode 100644 Brighter/Examples/DocumentsAndFolders.Sqs.EventsGenerator/DocumentsAndFolders.Sqs.EventsGenerator.csproj.orig
 delete mode 100644 Brighter/Examples/DocumentsAndFolders.Sqs.WindowsService/DocumentsAndFolders.Sqs.csproj.orig
 delete mode 100644 Brighter/Examples/EventSourcing/EventSourcing.csproj.bak.orig
 delete mode 100644 Brighter/Examples/GenericListener/GenericListener.csproj.bak.orig
 delete mode 100644 Brighter/Examples/Greetings/Greetings.csproj.bak.orig
 delete mode 100644 Brighter/Examples/ManagementAndMonitoring/ManagementAndMonitoring.csproj.orig
 delete mode 100644 Brighter/Examples/TaskList.AzureServiceBus/TaskList.AzureServiceBus.csproj.orig
 delete mode 100644 Brighter/Examples/tasklist/TaskList.csproj.orig
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/net452/NUnit.Specifications.NUnit3.coreCLR.dll.orig
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/net452/NUnit.Specifications.NUnit3.coreCLR.pdb.orig
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/netstandard1.6/NUnit.Specifications.NUnit3.coreCLR.dll.orig
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/netstandard1.6/NUnit.Specifications.NUnit3.coreCLR.pdb.orig
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/ShouldExtensions.cs.orig
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/net452/nUnitShouldAdapter.dll.orig
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/net452/nUnitShouldAdapter.pdb.orig
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/netstandard1.6/nUnitShouldAdapter.dll.orig
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/netstandard1.6/nUnitShouldAdapter.pdb.orig
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/netstandard1.6/dotnet-compile-csc.rsp.orig
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/obj/Debug/netstandard1.6/dotnet-compile.rsp.orig
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/project.lock.json.orig
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messageviewer/paramore.brighter.commandprocessor.messageviewer.csproj.bak.orig
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests.nunit/Timeout/When_sending_a_command_to_the_processor_failing_a_timeout_policy_check.cs.orig
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests.nunit/Timeout/When_sending_a_command_to_the_processor_passing_a_timeout_policy_check.cs.orig
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/paramore.brighter.commandprocessor.viewer.tests.csproj.bak.orig
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/packages.config.bak.orig
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/paramore.brighter.commandprocessor.csproj.bak.orig
 delete mode 100644 Brighter/paramore.brighter.monitoringconsole/paramore.brighter.monitoringconsole.csproj.orig

[33mcommit 33504aff6903ba2a356e80bd1b7154d879303f67[m
Author: ian <ian@huddle.com>
Date:   Wed Sep 28 12:13:49 2016 +0100

    new should extensions

 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/net452/NUnit.Specifications.NUnit3.coreCLR.dll
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/net452/NUnit.Specifications.NUnit3.coreCLR.pdb
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/net452/nunit.framework.dll
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/netstandard1.6/NUnit.Specifications.NUnit3.coreCLR.deps.json
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/netstandard1.6/NUnit.Specifications.NUnit3.coreCLR.dll
 delete mode 100644 Brighter/lib/NUnit.Specifications.NUnit3.coreCLR/bin/Debug/netstandard1.6/NUnit.Specifications.NUnit3.coreCLR.pdb
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/net452/nUnitShouldAdapter.dll
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/net452/nUnitShouldAdapter.pdb
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/net452/nunit.framework.dll
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/netstandard1.6/nUnitShouldAdapter.deps.json
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/netstandard1.6/nUnitShouldAdapter.dll
 delete mode 100644 Brighter/lib/nUnitShouldAdapter/bin/Debug/netstandard1.6/nUnitShouldAdapter.pdb

[33mcommit 4515214f7bffb68a7bd15e938a08a18eaaec28b2[m
Author: ian.pender <ian.pender@huddle.com>
Date:   Wed Sep 28 09:15:18 2016 +0100

    Introduce NUnit.Specifications (ported to core), add Should adaptor, convert viewer tests, restore removed tests, all green

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/TestBehaviours/ModuleWithNoStoreConnectionBehavior.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/TestBehaviours/ModuleWithStoreCantGetBehaviour.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/TestBehaviours/ModuleWithStoreNotViewerBehavior.cs

[33mcommit f974e53b1d81da0bab4b3711304725465a1367c9[m
Author: ian <ian@huddle.com>
Date:   Thu Sep 22 09:14:30 2016 +0100

    Move to upper level of repo

 delete mode 100644 Brighter/brightmgmnt/brightmgmnt/__pycache__/__init__.cpython-34.pyc
 delete mode 100644 Brighter/brightmgmnt/env/Lib/no-global-site-packages.txt
 delete mode 100644 Brighter/brightmgmnt/env/Lib/site-packages/pip/_vendor/html5lib/filters/__init__.py
 delete mode 100644 Brighter/brightmgmnt/env/Lib/site-packages/pip/_vendor/html5lib/treeadapters/__init__.py
 delete mode 100644 Brighter/brightmgmnt/env/Lib/site-packages/pip/operations/__init__.py
 delete mode 100644 Brighter/brightmntr/MANIFEST.in
 delete mode 100644 Brighter/brightmntr/README.rst
 delete mode 100644 Brighter/brightmntr/RunMe.Txt
 delete mode 100644 Brighter/brightmntr/brightmntr/__init__.py
 delete mode 100644 Brighter/brightmntr/brightmntr/__main__.py
 delete mode 100644 Brighter/brightmntr/brightmntr/__pycache__/configuration.cpython-34.pyc
 delete mode 100644 Brighter/brightmntr/brightmntr/configuration.py
 delete mode 100644 Brighter/brightmntr/brightmntr/worker.py
 delete mode 100644 Brighter/brightmntr/cfg/brightmntr.ini
 delete mode 100644 Brighter/brightmntr/pycharm_debug.py
 delete mode 100644 Brighter/brightmntr/setup.cfg
 delete mode 100644 Brighter/brightmntr/setup.py

[33mcommit c589bd3f5914d3903342c1d72c6cdc075dbe93da[m
Author: ian <ian@huddle.com>
Date:   Tue Sep 13 16:32:48 2016 +0100

    working on sqs

 delete mode 100644 Brighter/Examples/DocumentsAndFolders.Sqs.EventsGenerator/DocumentsAndFolders.Sqs.EventsGenerator.csproj
 delete mode 100644 Brighter/Examples/DocumentsAndFolders.Sqs.EventsGenerator/packages.config

[33mcommit dbf1e39342283d482c8d974328c80ebb52869baa[m
Author: ian <ian@huddle.com>
Date:   Fri Sep 9 20:56:58 2016 +0100

    working on project builds

 delete mode 100644 Brighter/Examples/TaskMailer/TaskMailer.csproj.bak
 delete mode 100644 Brighter/Examples/TaskMailer/TaskMailer.csproj.orig.bak
 delete mode 100644 Brighter/Examples/TaskMailer/TaskMailer.v2.ncrunchproject.bak
 delete mode 100644 Brighter/Examples/TaskMailer/packages.config.bak

[33mcommit b4c490cd84a5366753efe1ebf155c94becd237ff[m
Author: ian <ian@huddle.com>
Date:   Fri Sep 9 18:39:54 2016 +0100

    Fix issues with AWS tests and examples

 delete mode 100644 Brighter/Examples/DocumentsAndFolders.Sqs/App.config
 delete mode 100644 Brighter/Examples/DocumentsAndFolders.Sqs/DocumentsAndFolders.Sqs.csproj
 delete mode 100644 Brighter/Examples/DocumentsAndFolders.Sqs/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/awssqs/AWSSQSMessagingGatewayTests.cs

[33mcommit 44db1d0cb4fc1fa9272e21c47234c85d3ea8501f[m
Author: ian.pender <ian.pender@huddle.com>
Date:   Tue Aug 23 09:46:36 2016 +0100

    MessageViewer now works in Core and 4.5.2

 delete mode 100644 Brighter/Paramore.CommandProcessor.sln.orig
 delete mode 100644 Brighter/global.json.orig
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/Adaptors/API/Configuration/NancyBootstrapper.cs
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/Scripts/jquery-2.2.2.intellisense.js~074b84b331dc1d4a8578c4b408df31d7cfe52cde
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/Scripts/jquery-2.2.2.js~074b84b331dc1d4a8578c4b408df31d7cfe52cde
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/project.json
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/read.md
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/Adaptors/MessagesModuleTests/MessagesModuleFilterTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/Adaptors/MessagesModuleTests/MessagesModuleGetTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/Adaptors/MessagesModuleTests/MessagesModuleRePostTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/Adaptors/NancyModuleTestBuilder.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/Adaptors/StoresModuleTests/StoresModuleIndexTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/Adaptors/StoresModuleTests/StoresModuleItemTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/Ports/MessageStoreActivationStateListViewModelRetrieverTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/Ports/MessageStoreModelFactoryTestsBasic.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/Ports/MessageStoreModelFactoryTestsComplex.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/Ports/MessageStoreViewerModelRetrieverTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/TestDoubles/FakeMessageStoreConfigProviderExceptionOnGet.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/TestDoubles/FakeStoreActivationStateProvider.cs

[33mcommit 224701068f3b1d975cc4235af4feffae0ed82bda[m
Author: ian <ian@huddle.com>
Date:   Fri Aug 19 18:27:34 2016 +0100

    app.config deprecated, and add a test runner

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/app.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/app.with-delay.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/app.with-multiple-gateways.config

[33mcommit 3640cc6e4d78449674037f9330c37dd59f981fbe[m
Author: ian <ian@huddle.com>
Date:   Wed Aug 17 15:25:40 2016 +0100

    Fix configuration of ServiceActivator

 delete mode 100644 Brighter/paramore.brighter.serviceactivator/ServiceActivatorConfiguration/ConnectionElement.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/ServiceActivatorConfiguration/ServiceActivatorConfiguration.cs

[33mcommit a748d8a8c33a5e0f59986e7527404cc3b91b1f03[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Tue Aug 16 15:19:05 2016 +0100

    Fixed issues with test project, build with proper errors now

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/NuGet.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/AppConfig.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/nunit.specifications/Catch.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/nunit.specifications/Categories/AcceptanceAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/nunit.specifications/Categories/ComponentAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/nunit.specifications/Categories/IntegrationAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/nunit.specifications/Categories/SubcutaneousAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/nunit.specifications/Categories/SubjectAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/nunit.specifications/Categories/UnitAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/nunit.specifications/ContextSpecification.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/nunit.specifications/README.md
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/nunit.specifications/license.txt

[33mcommit 310a32f76643044a31a555e9f05c7d297d8b83c9[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Fri Aug 12 18:17:16 2016 +0100

    structured map fixes container issues

 delete mode 100644 Brighter/Examples/TasksApi/DependencyRegistrar.cs

[33mcommit d9498b8ae1d3da9322a24c46fe20ee3123e078e8[m
Author: ian <ian@huddle.com>
Date:   Thu Aug 11 17:11:12 2016 +0100

    Azure Service Bus now builds; uses amqplite for recieve, but REST for management

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.azureservicebus/AzureServiceBusMessagingGatewayConfigurationSection.cs

[33mcommit b6e87dbe881daf3677681d47a4acb84c9b0fa6ec[m
Author: ian <ian@huddle.com>
Date:   Mon Aug 8 14:10:53 2016 +0100

    Get RestMS client building

 delete mode 100644 Brighter/lib/hawk/net452/Hawk.dll
 delete mode 100644 Brighter/lib/hawk/net452/Hawk.dll.config
 delete mode 100644 Brighter/lib/hawk/net452/Hawk.pdb
 delete mode 100644 Brighter/lib/hawk/netstandard1.6/Hawk.deps.json
 delete mode 100644 Brighter/lib/hawk/netstandard1.6/Hawk.dll
 delete mode 100644 Brighter/lib/hawk/netstandard1.6/Hawk.pdb

[33mcommit e1909d21258bab13fcb348dec162dd2f93b9c2cb[m
Author: ian <ian@huddle.com>
Date:   Fri Jul 29 19:16:59 2016 +0100

    Working on fixes to RMS; using a forked Thinktecture Hawk DLL for Identity Model

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.restms/MessagingGatewayConfiguration/RestMSMessagingGatewayConfigurationSection.cs

[33mcommit 2af33f3e7b70fdcfaf7efdd1c8947e16214000f5[m
Author: ian.pender <ian.pender@huddle.com>
Date:   Fri Aug 5 08:15:26 2016 +0100

    Make ManagementAndMonitoring compile, copy MsSql CommandStore & MessageStore to sqllite and ensure compiles withsqllite refs

 delete mode 100644 Brighter/Examples/ManagementAndMonitoring/App_Data/MessageStore.sdf

[33mcommit 0106214aff95b3fcceb475b6a8b97411c7d1b86d[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Tue Aug 2 15:18:18 2016 +0100

    Added nuspec infomation to project.json for the commandprocessor

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/project.json.orig

[33mcommit eb8d3ac1590f8fbc07dcdf4f4c9bba88033d2aaf[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Tue Aug 2 15:22:53 2016 +0100

    The start of Tasks example

 delete mode 100644 Brighter/Examples/Tasks/Tasks.v2.ncrunchproject

[33mcommit f44c805f818a50ca86f029f1e7a350509e10fa88[m
Author: toby.henderson <toby.henderson@huddle.com>
Date:   Wed Jul 20 14:50:55 2016 +0100

    Greetings example building and running. core console and net452 windows service

 delete mode 100644 Brighter/Examples/Greetings/Adapters/ServiceHost/Program.cs

[33mcommit afbb6c9b6864e4577ff856c016ab32b71cbbe68c[m
Author: ian <ian@huddle.com>
Date:   Tue Jul 19 09:19:50 2016 +0100

    include back files for reference during conversion

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/paramore.brighter.commandprocessor.viewer.tests.csproj

[33mcommit 8dda18bca028272c74b90c68026df9fd5f8da3c8[m
Author: ian <ian@huddle.com>
Date:   Tue Jul 19 09:10:05 2016 +0100

    .bak files for reference during conversion

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.restms/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.restms/paramore.brighter.commandprocessor.messaginggateway.restms.csproj

[33mcommit 498d08ad7d1c265dfd542e7a94aa8ae38db7e5d8[m
Author: ian <ian@huddle.com>
Date:   Mon Jul 18 09:24:38 2016 +0100

    Moving to dotnetcore

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.commandstore.mssql/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.commandstore.mssql/paramore.brighter.commandprocessor.commandstore.mssql.csproj
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.eventstore/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.eventstore/paramore.brighter.commandprocessor.messagestore.eventstore.csproj
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.mssql/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.mssql/paramore.brighter.commandprocessor.messagestore.mssql.csproj
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.awssqs/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.awssqs/paramore.brighter.commandprocessor.messaginggateway.awssqs.csproj
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.azureservicebus/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.azureservicebus/paramore.brighter.commandprocessor.messaginggateway.azureservicebus.csproj

[33mcommit 9a4b73da3fad3d21445ddf643377d3bf7d090901[m
Author: ian <ian@huddle.com>
Date:   Fri Jul 15 11:33:02 2016 +0100

    Working on the test project

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/paramore.brighter.commandprocessor.tests.csproj

[33mcommit e28219fb04ac6a486c63fbcdf28487c998dff913[m
Author: Toby Henderson <toby.henderson@huddle.com>
Date:   Thu Jul 7 18:50:23 2016 +0100

    Service Activator build on dotnet core

 delete mode 100644 Brighter/paramore.brighter.serviceactivator/ServiceActivatorConfiguration/ConnectionElement.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/ServiceActivatorConfiguration/ConnectionElements.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/ServiceActivatorConfiguration/ConnectionFactory.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/ServiceActivatorConfiguration/ServiceActivatorConfigurationSection.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/app.config.install.xdt
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/app.config.uninstall.xdt

[33mcommit 3b2a78809a7f469644dc103f8acaf7ec5d571cb7[m
Author: ian <ian@huddle.com>
Date:   Wed Jul 6 18:14:47 2016 +0100

    Hello world works!

 delete mode 100644 Brighter/Examples/HelloWorld/HelloWorld.csproj
 delete mode 100644 Brighter/Examples/HelloWorld/packages.config

[33mcommit 18738af777c51f9efd2f8f0da9e44e85e98cad84[m
Author: ian <ian@huddle.com>
Date:   Wed Jul 6 17:42:11 2016 +0100

    CommandProcessor building under dotnetstandard 1.6

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/paramore.brighter.commandprocessor.csproj

[33mcommit 8bab2a5be1743eb8b4ad0d494a3ca9d9d5e2b872[m
Author: ian.pender <ian.pender@huddle.com>
Date:   Wed Apr 27 13:42:33 2016 +0100

    Merge results

 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/assets/views/index.html.orig

[33mcommit 09e401d87242fb761c4cc3cf2a90555523fe0f73[m
Author: ian <ian@huddle.com>
Date:   Wed Mar 30 13:05:39 2016 +0100

    Swap back C#5 compliant async handler code; update nuget package dependencies; fix api issues

 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery-2.2.2.js
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/Scripts/jquery-2.1.3.intellisense.js
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/Scripts/jquery-2.1.3.js

[33mcommit 844c1d972bcb6227a323e3fde832b0bad4f72005[m
Author: ian <ian@huddle.com>
Date:   Thu Apr 21 18:46:03 2016 +0100

    Don't include idea files

 delete mode 100644 Brightside/.idea/Brightside.iml
 delete mode 100644 Brightside/.idea/encodings.xml
 delete mode 100644 Brightside/.idea/modules.xml
 delete mode 100644 Brightside/.idea/vcs.xml
 delete mode 100644 Brightside/.idea/workspace.xml

[33mcommit d878fefc137eb016f03b6a2da6c0194a0fcf53b3[m
Author: ian <ian@huddle.com>
Date:   Thu Apr 21 18:29:46 2016 +0100

    Setting up the package structure, with one basic test

 delete mode 100644 Brightside/tests/context.py

[33mcommit 72750c8ba14909905421d454c0595268e84198ea[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Apr 21 18:27:04 2016 +0100

    Delete Readme.rst

 delete mode 100644 Brightside/Readme.rst

[33mcommit f961b9f3f9cb2b085474caddaf987e7f6ca7e601[m
Author: ian <ian@huddle.com>
Date:   Thu Apr 7 08:54:24 2016 +0100

    Commit to switch branches

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/PostBackItem.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/MessagePumpSynchronizationContext.cs

[33mcommit a0de108217ac6ee29e7ec28ad5eb71ba048ec043[m
Author: 'ian' <'ian@huddle.com'>
Date:   Wed Mar 30 13:05:39 2016 +0100

    Swap back C#5 compliant async handler code; update nuget package dependencies; fix api issues

 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery-2.1.4.min.js
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery-2.1.4.min.map
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/Scripts/jquery-2.1.4.min.js
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/Scripts/jquery-2.1.4.min.map

[33mcommit 5602aff2520f0fbdd9bc8e61d30e9a380d27c9c6[m
Author: ian <ian@huddle.com>
Date:   Thu Mar 3 15:34:23 2016 +0000

    Add constructor that injects logger for tests

 delete mode 100644 Brighter/Examples/HelloWorldAsync/GreetingCommandRequestHandlerAsyncHandler.cs

[33mcommit 62a79b0760734fac20e713a136a801b375b9aaf3[m
Author: 'ian' <'ian@huddle.com'>
Date:   Tue Mar 1 16:57:30 2016 +0000

    Checked documentation, updated; removed caching message consumer as code not implemented. Will address when we re-vivify the SQS+SNS support. May also be informed by moving to seperate priority queue over relying on broker for delay.

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/IAmAMessageGatewaySupportingCache.cs

[33mcommit 07c6c5294e44ceeb620ac8ff52af7808b3424322[m
Author: 'ian' <'ian@huddle.com'>
Date:   Tue Mar 1 10:44:05 2016 +0000

    Add tests around monitoring using an async approach

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/monitoring/Attributes/AsyncMonitorAttribute.cs

[33mcommit 005d00189f492b020a005b85e367382b153e16ae[m
Author: 'ian' <'ian@huddle.com'>
Date:   Thu Feb 25 12:16:28 2016 +0000

    Clearing up test coverage post the async work

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/TestMessageMapperFactory.cs

[33mcommit 20775df854730e0e7a41d472e4b6a6b978e1a406[m
Author: 'ian' <'ian@huddle.com'>
Date:   Wed Feb 24 18:14:01 2016 +0000

    Kill OutputChannel as it is not used, we just use the producer directly; rename InputChannel to Channel now that it is the one and only

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/IAmAnInputChannel.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/IAmAnOutputChannel.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/OutputChannel.cs

[33mcommit d0e0939102b302fd5cf53033984f4c3ef8a40827[m
Author: ian <ian@huddle.com>
Date:   Wed Feb 24 10:10:35 2016 +0000

    Remove Rewind and Renegade; need to BFG at some point

 delete mode 100644 Renegade/paramore.brighter.restms.core/.gitignore
 delete mode 100644 Renegade/paramore.brighter.restms.core/Extensions/Each.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Globals.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/Address.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/Domain.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/Feed.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/FeedType.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/Join.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/JoinType.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/Message.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/MessageContent.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/MessageHeaders.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/Name.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/Pipe.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/PipeType.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/Profile.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/Resource.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/RoutingTable.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Model/Title.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Cache/IAmACache.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/AddFeedCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/AddFeedToDomainCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/AddJoinCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/AddJoinToFeedCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/AddJoinToPipeCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/AddMessageToFeedCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/AddPipeCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/AddPipeToDomainCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/DeleteFeedCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/DeleteMessageCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/DeletePipeCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/InvalidateCacheCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/RemoveFeedFromDomainCommand.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Commands/deleteme.txt
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Common/AggregateVersion.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Common/DomainDoesNotExistException.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Common/FeedAlreadyExistsException .cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Common/FeedDoesNotExistException.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Common/IAmARepository.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Common/IAmAnAggregate.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Common/Identity.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Common/JoinDoesNotExistException.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Common/MessageDoesNotExistException.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Common/PipeDoesNotExistException.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/AddFeedCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/AddFeedToDomainCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/AddJoinCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/AddJoinToFeedCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/AddJoinToPipeCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/AddMessageToFeedCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/AddPipeCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/AddPipeToDomainCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/CacheCleaningHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/DeleteFeedCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/DeleteMessageCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/DeletePipeCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/RemoveFeedFromDomainCommandHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Handlers/deleteme.txt
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Repositories/InMemoryDomainRepository.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Repositories/InMemoryFeedRepository.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Repositories/InMemoryJoinRepository.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Repositories/InMemoryPipeRepository.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Repositories/InMemoryRepository.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSDomain.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSFeed.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSJoin.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSMessage.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSMessageContent.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSMessageHeader.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSMessageLink.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSMessagePosted.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSPipe.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSPipeLink.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSPipeNew.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/Resources/RestMSProfile.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/ViewModelRetrievers/DomainRetriever.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/ViewModelRetrievers/FeedRetriever.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/ViewModelRetrievers/JoinRetriever.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/ViewModelRetrievers/MessageRetriever.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Ports/ViewModelRetrievers/PipeRetriever.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/paramore.brighter.restms.core/packages.config
 delete mode 100644 Renegade/paramore.brighter.restms.core/paramore.brighter.restms.core.csproj
 delete mode 100644 Renegade/paramore.brighter.restms.server/.gitignore
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Cache/CacheHandler.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Configuration/RestMSServerConfiguration.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Configuration/UnityHandlerFactory.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Controllers/DomainController.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Controllers/FeedController.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Controllers/JoinController.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Controllers/MessageController.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Controllers/PipeController.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Filters/DomainNotFoundExceptionFilterAttribute.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Filters/FeedAlreadyExistsExceptionFilterAttribute.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Filters/FeedDoesNotExistExceptionFilterAttribute.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Filters/JoinDoesNotExistExceptionFilter.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Filters/PipeDoesNotExistExceptionFilter.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Formatters/ConversionStrategyFactory.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Formatters/IParseDomainPosts.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Formatters/JsonDomainPostParser.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Formatters/ParseResult.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Formatters/XmlDomainPostParser.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Program.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Security/CredentialStore.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Service/IoCConfiguration.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Service/RestMSServerBuilder.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Service/RestMSService.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Service/Startup.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/Adapters/Service/SystemDefaults.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/App.config
 delete mode 100644 Renegade/paramore.brighter.restms.server/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/paramore.brighter.restms.server/RunServer.bat
 delete mode 100644 Renegade/paramore.brighter.restms.server/packages.config
 delete mode 100644 Renegade/paramore.brighter.restms.server/paramore.brighter.restms.server.csproj
 delete mode 100644 Renegade/paramore.brighter.restms.server/restms_implementation_notes.txt
 delete mode 100644 Renegade/paramore.renegade.sln
 delete mode 100644 Renegade/paramore.renegade.sln.GhostDoc.xml
 delete mode 100644 Renegade/paramore.renegade.tests/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/paramore.renegade.tests/RestMSServer/CachingTests.cs
 delete mode 100644 Renegade/paramore.renegade.tests/RestMSServer/DomainTests.cs
 delete mode 100644 Renegade/paramore.renegade.tests/RestMSServer/FeedTests.cs
 delete mode 100644 Renegade/paramore.renegade.tests/RestMSServer/JoinTests.cs
 delete mode 100644 Renegade/paramore.renegade.tests/RestMSServer/MessageTests.cs
 delete mode 100644 Renegade/paramore.renegade.tests/RestMSServer/PipeTests.cs
 delete mode 100644 Renegade/paramore.renegade.tests/RestMSServer/SerializationTests.cs
 delete mode 100644 Renegade/paramore.renegade.tests/app.config
 delete mode 100644 Renegade/paramore.renegade.tests/packages.config
 delete mode 100644 Renegade/paramore.renegade.tests/paramore.renegade.tests.csproj
 delete mode 100644 Rewind/.nuget/NuGet.Config
 delete mode 100644 Rewind/.nuget/NuGet.exe
 delete mode 100644 Rewind/.nuget/NuGet.targets
 delete mode 100644 Rewind/Debugging_Durandal_KO.txt
 delete mode 100644 Rewind/HTTPRequest_API_Tests_Fiddler.txt
 delete mode 100644 Rewind/Lib/Paramore.CommandProcessor/paramore.commandprocessor.dll
 delete mode 100644 Rewind/Lib/Paramore.CommandProcessor/paramore.commandprocessor.ioccontainers.dll
 delete mode 100644 Rewind/Lib/Paramore.CommandProcessor/paramore.commandprocessor.ioccontainers.pdb
 delete mode 100644 Rewind/Lib/Paramore.CommandProcessor/paramore.commandprocessor.pdb
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/.gitattributes
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/.gitignore
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/.htaccess
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/404.html
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/CHANGELOG.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/CONTRIBUTING.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/LICENSE.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/README.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon-114x114-precomposed.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon-144x144-precomposed.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon-57x57-precomposed.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon-72x72-precomposed.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon-precomposed.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/crossdomain.xml
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/css/main.css
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/css/normalize.css
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/TOC.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/crossdomain.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/css.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/extend.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/faq.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/htaccess.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/html.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/js.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/misc.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/usage.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/favicon.ico
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/humans.txt
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/img/.gitignore
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/index.html
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/js/main.js
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/js/plugins.js
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/js/vendor/jquery-1.8.3.min.js
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/js/vendor/modernizr-2.6.2.min.js
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/robots.txt
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/OpenRasta.Testing.dll
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/OpenRasta.Testing.pdb
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/OpenRasta.dll
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/OpenRasta.pdb
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/Resources/error-test.htm
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/openrasta.testing.xml
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/openrasta.xml
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/version
 delete mode 100644 Rewind/Lib/openrasta-hosting-aspnet-2.1.1+151058209/bin-net35/OpenRasta.Hosting.AspNet.dll
 delete mode 100644 Rewind/Lib/openrasta-hosting-aspnet-2.1.1+151058209/bin-net35/OpenRasta.Hosting.AspNet.pdb
 delete mode 100644 Rewind/Lib/openrasta-hosting-aspnet-2.1.1+151058209/bin-net35/OpenRasta.Server.XML
 delete mode 100644 Rewind/Lib/openrasta-hosting-aspnet-2.1.1+151058209/version
 delete mode 100644 Rewind/Running Raven.txt
 delete mode 100644 Rewind/documentation/rewind_replay_sign.jpg
 delete mode 100644 Rewind/paramore.rewind.acceptancetests/paramore.acceptancetests.py
 delete mode 100644 Rewind/paramore.rewind.acceptancetests/paramore.acceptancetests.pyproj
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Configuration.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Contributors/CrossDomainPipelineContributor.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Contributors/DependencyPipelineContributor.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Default.aspx
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Default.aspx.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Default.aspx.designer.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Handlers/EntryPointHandler.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Handlers/SpeakerEndPointHandler.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Handlers/VenueEndPointHandler.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Resources/AddressResource.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Resources/ContactResource.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Resources/EntryPointResource.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Resources/Link.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Resources/SpeakerResource.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Resources/VenueResource.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Translators/ITranslator.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Translators/ParamoreGlobals.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Translators/SpeakerTranslator.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Translators/VenueTranslator.cs
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Web.Debug.config
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Web.Release.config
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/Web.config
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/packages.config
 delete mode 100644 Rewind/paramore.rewind.api/src/paramore.rewind.api/paramore.rewind.api.csproj
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/IAggregateRoot.cs
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/IAmADocument.cs
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/IAmAUnitOfWorkFactory.cs
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/IEntity.cs
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/IRepository.cs
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/IUnitOfWork.cs
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/Id.cs
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/RavenConnection.cs
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/Repository.cs
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/UnitOfWork.cs
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/UnitOfWorkFactory.cs
 delete mode 100644 Rewind/paramore.rewind.core/Adapters/Repositories/Version.cs
 delete mode 100644 Rewind/paramore.rewind.core/ConfigurationHelper.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Common/AggregateRoot.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Common/EmailAddress.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Common/IAmAValueType.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Common/PhoneNumber.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/FiftyPercentOverbookingPolicy.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/IAmAnOverbookingPolicy.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/IIssueTickets.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/IScheduler.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/Meeting.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/MeetingDate.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/MeetingDocument.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/MeetingDocumentTickets.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/MeetingState.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/Scheduler.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/Ticket.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/TicketIssuer.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Meetings/Tickets.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Speakers/Speaker.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Speakers/SpeakerBio.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Speakers/SpeakerDocument.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Speakers/SpeakerName.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Venues/Address.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Venues/Capacity.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Venues/City.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Venues/Contact.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Venues/Name.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Venues/PostCode.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Venues/Street.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Venues/Venue.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Venues/VenueDocument.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Venues/VenueMap.cs
 delete mode 100644 Rewind/paramore.rewind.core/Domain/Venues/VenueName.cs
 delete mode 100644 Rewind/paramore.rewind.core/Extensions/EnumerationExtensions.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/Commands/Meeting/ScheduleMeetingCommand.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/Commands/Speaker/AddSpeakerCommand.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/Commands/Venue/AddVenueCommand.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/Commands/Venue/DeleteVenueCommand.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/Commands/Venue/UpdateVenueCommand.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/Events/Meeting/MeetingScheduledEvent.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/Handlers/Meetings/ScheduleMeetingCommandHandler.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/Handlers/Speakers/AddSpeakerCommandHandler.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/Handlers/Venues/AddVenueCommandHandler.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/Handlers/Venues/DeleteVenueCommandHandler.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/Handlers/Venues/UpdateVenueCommandHandler.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/ThinReadLayer/IAmAViewModelReader.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/ThinReadLayer/SpeakerReader.cs
 delete mode 100644 Rewind/paramore.rewind.core/Ports/ThinReadLayer/VenueReader.cs
 delete mode 100644 Rewind/paramore.rewind.core/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.rewind.core/packages.config
 delete mode 100644 Rewind/paramore.rewind.core/paramore.rewind.core.csproj
 delete mode 100644 Rewind/paramore.rewind.sln
 delete mode 100644 Rewind/paramore.rewind.unittests/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.rewind.unittests/Translators/SpeakerTranslatorTests.cs
 delete mode 100644 Rewind/paramore.rewind.unittests/Translators/VenueTranslatorTests.cs
 delete mode 100644 Rewind/paramore.rewind.unittests/domain/Meetings/OverbookingPolicyTests.cs
 delete mode 100644 Rewind/paramore.rewind.unittests/domain/Meetings/SchedulerTests.cs
 delete mode 100644 Rewind/paramore.rewind.unittests/domain/Meetings/TicketIssuerTests.cs
 delete mode 100644 Rewind/paramore.rewind.unittests/fakes/FakeRepository.cs
 delete mode 100644 Rewind/paramore.rewind.unittests/packages.config
 delete mode 100644 Rewind/paramore.rewind.unittests/paramore.rewind.unittests.csproj
 delete mode 100644 Rewind/paramore.rewind.unittests/services/CommandHandlers/Meetings/ScheduleMeetingCommandHandlerTests.cs
 delete mode 100644 Rewind/paramore.rewind.unittests/services/CommandHandlers/Speakers/AddSpeakerCommandHandlerTests.cs
 delete mode 100644 Rewind/paramore.rewind.unittests/services/CommandHandlers/Venues/AddVenueCommandHandlerTests.cs
 delete mode 100644 Rewind/paramore.rewind.unittests/services/CommandHandlers/Venues/DeleteVenueCommandHandlerTests.cs
 delete mode 100644 Rewind/paramore.rewind.unittests/services/CommandHandlers/Venues/UpdateVenueCommandHandlerTests.cs
 delete mode 100644 Rewind/paramore.rewind.web/App_Start/DurandalBundleConfig.cs
 delete mode 100644 Rewind/paramore.rewind.web/App_Start/DurandalConfig.cs
 delete mode 100644 Rewind/paramore.rewind.web/App_Start/FilterConfig.cs
 delete mode 100644 Rewind/paramore.rewind.web/App_Start/RouteConfig.cs
 delete mode 100644 Rewind/paramore.rewind.web/Controllers/DurandalController.cs
 delete mode 100644 Rewind/paramore.rewind.web/Global.asax
 delete mode 100644 Rewind/paramore.rewind.web/Global.asax.cs
 delete mode 100644 Rewind/paramore.rewind.web/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.rewind.web/Views/Web.config
 delete mode 100644 Rewind/paramore.rewind.web/Web.Debug.config
 delete mode 100644 Rewind/paramore.rewind.web/Web.Release.config
 delete mode 100644 Rewind/paramore.rewind.web/Web.config
 delete mode 100644 Rewind/paramore.rewind.web/packages.config
 delete mode 100644 Rewind/paramore.rewind.web/paramore.rewind.web.csproj
 delete mode 100644 Rewind/theRules.txt
 delete mode 100644 Rewind/version

[33mcommit 6f57c51a65faa90bd55f6b915e38157b788f8856[m
Author: 'ian' <'ian@huddle.com'>
Date:   Tue Feb 2 09:20:13 2016 +0000

    SQL Message Store Async tests; note that no implementation of async for message store, so added

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/MsSql/SqlMessageStoreTests.cs

[33mcommit 3845eac49155132a197e7906dd0f16fe9d34018a[m
Author: ian <ian@huddle.com>
Date:   Thu Jan 21 09:59:13 2016 +0000

    rename the AsyncRequestHandler to RequestHandlerAsync to fit with the standard naming convention

 delete mode 100644 Brighter/Examples/HelloWorldAsync/GreetingCommandAsyncHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyCommandHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyDoubleDecoratedHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyEventHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyImplicitHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyLoggingHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyMixedImplicitHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyObsoleteCommandHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyOtherEventHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyPreAndPostDecoratedHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/TestDoubles/MyValidationHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/EventSourcing/TestDoubles/MyStoredCommandHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/MyFailsWithDivideByZeroHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/TestDoubles/MyFailsWithFallbackDivideByZeroHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/AsyncRequestHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/eventsourcing/Handlers/AsyncCommandSourcingHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/monitoring/Handlers/AsyncMonitorHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/policy/Handlers/ExceptionPolicyHandlerAsync.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/policy/Handlers/FallbackPolicyHandlerAsync.cs

[33mcommit 943a8503601356074c58582544b6fe7e096d1d2f[m
Author: ian <ian@huddle.com>
Date:   Thu Jan 21 09:13:40 2016 +0000

    Add tests around request logging

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/logging/Attributes/AsyncRequestLoggingAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/logging/Handlers/AsyncRequestLoggingHandler.cs

[33mcommit 92e87953e3ee72452e6d24a6b8e5de63750d3b37[m
Author: 'ian' <'ian@huddle.com'>
Date:   Wed Jan 6 16:51:56 2016 +0000

    Add a test for a pipeline of handlers; drop AsyncRequestHandlerAttribute, Just use RequestHandlerAttribute.

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/AsyncRequestHandlerAttribute.cs

[33mcommit 50261d764aa283f6ab1d8c5a6b15eb62c224a0a4[m
Author: 'ian' <'ian@huddle.com'>
Date:   Wed Jan 6 09:26:09 2016 +0000

    We had two conventions for MSpec tests. File per fixture, multiple fixtures testing similar behaviour together. I have been opionated and moved us to file per fixture, as it is easy to see tests when browsing the file system. I am sure some prefer the other style, but so be it.

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/ConfigurationCommandTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ControlBus/ControlBusMessageMapperTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/ExceptionPolicy/FallbackSupportTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/MessageDispatchTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/MessagePumpTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/MessageChannelTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/rmq/RMQMessagingGatewayTests.cs

[33mcommit 0fc0a925fc900a1ffb151411782e562b343a5b71[m
Author: ian <ian@huddle.com>
Date:   Mon Dec 14 10:45:19 2015 +0000

    Configuration of handlers and message mappers for heartbeat

 delete mode 100644 Brighter/paramore.brighter.serviceactivator/Ports/Handlers/HeartbeatCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/Ports/Mappers/HeartbeatReplyMessageMapper.cs

[33mcommit 79f51abfa7757b9235a92cf0448aa011fb0d1b85[m
Author: 'ian' <'ian@huddle.com'>
Date:   Wed Dec 9 09:12:56 2015 +0000

    Moved to explicit request and reply commands and put the complexity into the message mapper. Pollutes the preferred publish-subscribe approach less.

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/IAmACallback.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/Ports/Mappers/HeartbeatCommandMessageMapper.cs

[33mcommit 629ca38ac7bd9ffc448aa56dfe05dcdfcdac2d49[m
Author: 'ian' <'ian@huddle.com'>
Date:   Mon Oct 26 10:26:11 2015 +0000

    Change examples to use the new 'no logger' default constructor approach, not logger injection

 delete mode 100644 Brighter/Examples/TaskMailer/Ports/TaskReminderCommandMessageMapper.cs

[33mcommit d7b4b99a03d9568c57531f8eedde46b41fe745d5[m
Author: 'ian' <'ian@huddle.com'>
Date:   Sun Oct 25 16:19:27 2015 +0000

    Provide a constructor that initializes the logger from the LogProvider. Drop builder dependecies on Logger. Comment ILog constructor as tests only

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/CommandProcessorBuilderTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/CommandProcessorTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/PipelineTests.cs

[33mcommit 7e3875853617f2926954fb9573bf97d0897f4395[m
Author: Toby Henderson <toby.henderson@huddle.com>
Date:   Tue Aug 11 12:39:03 2015 +0100

    Updated bootstrap

 delete mode 100644 Brighter/Examples/TaskListUI/Icon.png
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/npm.js
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/Icon.png
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/Scripts/npm.js

[33mcommit b5fbe120c988d4a869b8853bd3271539b1bfc73a[m
Author: Ian Pender <ian.pender@huddle.com>
Date:   Wed Jul 29 14:53:44 2015 +0100

    Viewer UI smarting up. Mad tests more reliable

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.viewer.tests/TestBehaviours/ModuleWithBadConfigBehavior.cs

[33mcommit dea89380c16333b0f774145fee8d03a37bfb6561[m
Author: Toby Henderson <toby.henderson@huddle.com>
Date:   Fri Jul 17 12:12:49 2015 +0100

    remove useless build, test and package cmdlets

 delete mode 100644 Brighter/BuildMe.bat
 delete mode 100644 Brighter/Paramore.NuGetPushAll.bat
 delete mode 100644 Brighter/TestMe.bat

[33mcommit 35f85968187ebb035317e754cd3c984fef7c9374[m
Author: Richard Tappenden <richard.tappenden@huddle.net>
Date:   Fri Jul 10 15:49:07 2015 +0100

    Remove redundant config file.

 delete mode 100644 Brighter/Examples/Tasks/app.config

[33mcommit a146b88db85488f04c65f03a3f52183721b35f9f[m
Author: Benjamin Hodgson <benjamin.hodgson@huddle.com>
Date:   Wed Jul 8 15:09:54 2015 +0100

    Update all Nuget packages.
    We were getting version mismatch errors for the RabbitMQ library :(

 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery-2.1.3.min.js
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/Scripts/jquery-1.9.1.min.js
 delete mode 100644 Brighter/paramore.brighter.comandprocessor.messageviewer/Scripts/jquery-1.9.1.min.map

[33mcommit 231200f8ff00bfa47e7c30285af94dcdb463ed29[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Jul 3 12:38:02 2015 +0100

    Dropped support for RavenDB as a message store; use SQL or EventStore

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/.gitignore
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/MessageStoreFactory.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/Properties/AssemblyInfo.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/RavenMessageStore.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/paramore.brighter.commandprocessor.messagestore.ravendb.csproj
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/paramore.brighter.commandprocessor.messagestore.ravendb.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/web.config.install.xdt
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/web.config.uninstall.xdt
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageStore/RavenDb/MessageStoreTests.cs

[33mcommit 465eb8773951eea16c4eb3ab169bdc326384b3c3[m
Author: Toby Henderson <toby.henderson@huddle.com>
Date:   Wed Jun 3 17:17:53 2015 +0100

    Cleaning up code so working with dnx and Portable easier
    
    * [Serialize] no longer supported in dnx world
    * The overriding exception method not supported in dnx world
    * Reflection and the Type class has some changes http://blogs.msdn.com/b/dotnet/archive/2012/08/28/evolving-the-reflection-api.aspx

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/GlobalSuppressions.cs

[33mcommit 8c765fe8c74ac7c6a9703d8fe2f4ee816b9442d4[m
Author: Ian Pender <ian.pender@huddle.com>
Date:   Tue May 19 12:28:45 2015 +0100

    Brighter Message Viewer
    - Show msg topic
       - Format DateTime
    + test for UI Model(s)

 delete mode 100644 Brighter/Examples/TaskListUI/TaskListUI.csproj.orig
 delete mode 100644 Brighter/Examples/TaskListUI/packages.config.orig
 delete mode 100644 Brighter/Examples/Tasks/Ports/Handlers/MailTaskReminderHandler.cs.orig
 delete mode 100644 Brighter/Examples/Tasks/Ports/Handlers/ValidationHandler.cs.orig
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/ClockAbstraction.cs

[33mcommit 287334e4d934b547b1bdfbf06288d0fac382c06a[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu May 14 17:22:13 2015 +0100

    Swapping Greetings and MonitoringAndMessagine muddle back

 delete mode 100644 Brighter/Examples/Greetings/Adapters/ServiceHost/MeetingAndManagementService.cs
 delete mode 100644 Brighter/Examples/ManagementAndMonitoring/Adapters/ServiceHost/GreetingService.cs

[33mcommit cd213f6ab9cf516f1e45ac6e11ced6c06c3075b4[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed May 6 15:55:39 2015 +0100

    First time pushing successfully to amqp from controlbus script [skip ci]

 delete mode 100644 Brighter/brightmgmnt/README

[33mcommit b1e071254c98858c1a36eb41b4aa32d1665f8e42[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Apr 15 20:31:36 2015 +0100

    Merge the controlbus for serviceactivators into serviceactivator

 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/ControlBusBuilder.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/ControlBusMessageMapperFactory.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/Ports/Commands/ConfigurationCommand.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/Ports/Commands/HeartBeatCommand.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/Ports/Handlers/ConfigurationMessageHandler.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/Properties/AssemblyInfo.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/paramore.brighter.serviceactivator.controlbus.csproj
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/readme.txt

[33mcommit 024f523a3dfefa8f9db86e86f0a3ca986fc090b4[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Apr 8 17:55:32 2015 +0100

    Fixed an issue with lack of multiple UsePolicy or FallbackPolicy attributes; modified tasks to use dao throughout; added policy to tasks handlers

 delete mode 100644 Brighter/Examples/Tasks/Ports/MailTaskReminderHandler.cs

[33mcommit 83b8978429cda205543e6e368f6c74a61a8f9a85[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Apr 8 17:27:47 2015 +0100

    Fixed up tasks to use one TasksDAO and database

 delete mode 100644 Brighter/Examples/TaskList/Ports/ViewModelRetrievers/SimpleDataRetriever.cs

[33mcommit b8524a00676ee37e26581afabb61aa8a77ebe23c[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Mar 31 18:39:43 2015 +0100

    Remove RESTMS server from Brighter code; now in own solution

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/RestMSServer/CachingTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/RestMSServer/DomainTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/RestMSServer/FeedTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/RestMSServer/JoinTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/RestMSServer/MessageTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/RestMSServer/PipeTests.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/RestMSServer/SerializationTests.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/.gitignore
 delete mode 100644 Brighter/paramore.brighter.restms.core/Extensions/Each.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Globals.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/Address.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/Domain.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/Feed.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/FeedType.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/Join.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/JoinType.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/Message.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/MessageContent.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/MessageHeaders.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/Name.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/Pipe.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/PipeType.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/Profile.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/Resource.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/RoutingTable.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Model/Title.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Cache/IAmACache.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/AddFeedCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/AddFeedToDomainCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/AddJoinCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/AddJoinToFeedCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/AddJoinToPipeCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/AddMessageToFeedCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/AddPipeCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/AddPipeToDomainCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/DeleteFeedCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/DeleteMessageCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/DeletePipeCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/InvalidateCacheCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/RemoveFeedFromDomainCommand.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/deleteme.txt
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Common/AggregateVersion.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Common/DomainDoesNotExistException.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Common/FeedAlreadyExistsException .cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Common/FeedDoesNotExistException.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Common/IAmARepository.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Common/IAmAnAggregate.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Common/Identity.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Common/JoinDoesNotExistException.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Common/MessageDoesNotExistException.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Common/PipeDoesNotExistException.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/AddFeedCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/AddFeedToDomainCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/AddJoinCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/AddJoinToFeedCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/AddJoinToPipeCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/AddMessageToFeedCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/AddPipeCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/AddPipeToDomainCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/CacheCleaningHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/DeleteFeedCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/DeleteMessageCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/DeletePipeCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/RemoveFeedFromDomainCommandHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Handlers/deleteme.txt
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Repositories/InMemoryDomainRepository.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Repositories/InMemoryFeedRepository.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Repositories/InMemoryJoinRepository.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Repositories/InMemoryPipeRepository.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Repositories/InMemoryRepository.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSDomain.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSFeed.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSJoin.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSMessage.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSMessageContent.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSMessageHeader.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSMessageLink.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSMessagePosted.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSPipe.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSPipeLink.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSPipeNew.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMSProfile.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/ViewModelRetrievers/DomainRetriever.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/ViewModelRetrievers/FeedRetriever.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/ViewModelRetrievers/JoinRetriever.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/ViewModelRetrievers/MessageRetriever.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/ViewModelRetrievers/PipeRetriever.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/Properties/AssemblyInfo.cs
 delete mode 100644 Brighter/paramore.brighter.restms.core/paramore.brighter.restms.core.csproj
 delete mode 100644 Brighter/paramore.brighter.restms.server/.gitignore
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Cache/CacheHandler.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Configuration/RestMSServerConfiguration.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Configuration/UnityHandlerFactory.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Controllers/DomainController.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Controllers/FeedController.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Controllers/JoinController.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Controllers/MessageController.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Controllers/PipeController.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Filters/DomainNotFoundExceptionFilterAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Filters/FeedAlreadyExistsExceptionFilterAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Filters/FeedDoesNotExistExceptionFilterAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Filters/JoinDoesNotExistExceptionFilter.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Filters/PipeDoesNotExistExceptionFilter.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Formatters/ConversionStrategyFactory.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Formatters/IParseDomainPosts.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Formatters/JsonDomainPostParser.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Formatters/ParseResult.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Formatters/XmlDomainPostParser.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Program.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Security/CredentialStore.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Service/IoCConfiguration.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Service/RestMSServerBuilder.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Service/RestMSService.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Service/Startup.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Service/SystemDefaults.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/App.config
 delete mode 100644 Brighter/paramore.brighter.restms.server/Properties/AssemblyInfo.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/RunServer.bat
 delete mode 100644 Brighter/paramore.brighter.restms.server/packages.config
 delete mode 100644 Brighter/paramore.brighter.restms.server/paramore.brighter.restms.server.csproj
 delete mode 100644 Brighter/paramore.brighter.restms.server/restms_implementation_notes.txt

[33mcommit 3bf18ddf1982f30b191c265856de5f0abf4a7e97[m
Author: Ian Pender <ian.pender@huddle.com>
Date:   Wed Mar 11 12:30:48 2015 +0000

    TaskListUI - reformat code.

 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/_references.js

[33mcommit 73e26fef2e711579e4367044964db699eab24295[m
Author: Ian Pender <ian.pender@huddle.com>
Date:   Wed Mar 11 12:26:47 2015 +0000

    TaskListUI - Deleted all the cruft.

 delete mode 100644 Brighter/Examples/TaskListUI/App_Start/BundleConfig.cs
 delete mode 100644 Brighter/Examples/TaskListUI/App_Start/FilterConfig.cs
 delete mode 100644 Brighter/Examples/TaskListUI/Content/bootstrap.css
 delete mode 100644 Brighter/Examples/TaskListUI/Controllers/HomeController.cs
 delete mode 100644 Brighter/Examples/TaskListUI/Project_Readme.html
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/bootstrap.js
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery-2.1.3.js
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery-2.1.3.min.map
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery.validate-vsdoc.js
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery.validate.js
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery.validate.min.js
 delete mode 100644 Brighter/Examples/TaskListUI/Views/Home/About.cshtml
 delete mode 100644 Brighter/Examples/TaskListUI/Views/Home/Contact.cshtml
 delete mode 100644 Brighter/Examples/TaskListUI/Views/Home/Index.cshtml
 delete mode 100644 Brighter/Examples/TaskListUI/Views/Shared/Error.cshtml
 delete mode 100644 Brighter/Examples/TaskListUI/Views/Tasks/Create.cshtml
 delete mode 100644 Brighter/Examples/TaskListUI/Views/Tasks/Delete.cshtml
 delete mode 100644 Brighter/Examples/TaskListUI/Views/Tasks/Details.cshtml
 delete mode 100644 Brighter/Examples/TaskListUI/Views/Tasks/Edit.cshtml
 delete mode 100644 Brighter/Examples/TaskListUI/fonts/glyphicons-halflings-regular.eot
 delete mode 100644 Brighter/Examples/TaskListUI/fonts/glyphicons-halflings-regular.svg
 delete mode 100644 Brighter/Examples/TaskListUI/fonts/glyphicons-halflings-regular.ttf
 delete mode 100644 Brighter/Examples/TaskListUI/fonts/glyphicons-halflings-regular.woff

[33mcommit 595fa78ed2f14ea9f3c8e8573876d8b149d77dab[m
Author: Ian Pender <ian.pender@huddle.com>
Date:   Wed Mar 11 11:22:45 2015 +0000

    TaskListUI - added extra state on GET

 delete mode 100644 Brighter/Examples/TaskListUI/app/index.html
 delete mode 100644 Brighter/Examples/TaskListUI/app/templates.html

[33mcommit 089605e91016e720d7abd5148fc57ab226e819ce[m
Author: Ian Pender <ian.pender@huddle.com>
Date:   Tue Mar 10 18:08:03 2015 +0000

    TastListUI - mnaual CORS removed! DElete to go

 delete mode 100644 Brighter/Examples/TaskListUI/Controllers/TaskListController.cs

[33mcommit a6c1721615db21bc8084e7519f13b52d7174b79e[m
Author: Ian Pender <ian.pender@huddle.com>
Date:   Tue Mar 10 15:11:49 2015 +0000

    TaskListUI
    - Removed not needed components
    - Render GET

 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery-1.10.2.min.js
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery-1.10.2.min.map
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery.validate.unobtrusive.js
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/jquery.validate.unobtrusive.min.js
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/modernizr-2.6.2.js
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/respond.js
 delete mode 100644 Brighter/Examples/TaskListUI/Scripts/respond.min.js

[33mcommit f03a201f3b74b3ba4744a3608e49f833b07731b6[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Mar 6 17:55:09 2015 +0000

    Begin a major rework of rewind to reflect the new Brighter shape, won't build as yet [skip ci]

 delete mode 100644 Rewind/paramore.acceptancetests/.idea/.name
 delete mode 100644 Rewind/paramore.acceptancetests/.idea/encodings.xml
 delete mode 100644 Rewind/paramore.acceptancetests/.idea/misc.xml
 delete mode 100644 Rewind/paramore.acceptancetests/.idea/modules.xml
 delete mode 100644 Rewind/paramore.acceptancetests/.idea/paramore.acceptancetests.iml
 delete mode 100644 Rewind/paramore.acceptancetests/.idea/scopes/scope_settings.xml
 delete mode 100644 Rewind/paramore.acceptancetests/.idea/vcs.xml
 delete mode 100644 Rewind/paramore.acceptancetests/.idea/workspace.xml
 delete mode 100644 Rewind/paramore.api/src/paramore.api/Default.aspx
 delete mode 100644 Rewind/paramore.configuration/ConfigurationHelper.cs
 delete mode 100644 Rewind/paramore.configuration/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.configuration/paramore.configuration.csproj
 delete mode 100644 Rewind/paramore.domain/Meetings/MeetingState.cs
 delete mode 100644 Rewind/paramore.domain/packages.config
 delete mode 100644 Rewind/paramore.domain/paramore.domain.csproj
 delete mode 100644 Rewind/paramore.infrastructure/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.infrastructure/Repositories/IRepository.cs
 delete mode 100644 Rewind/paramore.infrastructure/packages.config
 delete mode 100644 Rewind/paramore.infrastructure/paramore.infrastructure.csproj
 delete mode 100644 Rewind/paramore.integrationtests/App.config
 delete mode 100644 Rewind/paramore.integrationtests/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.integrationtests/ThinReadLayer/SpeakerReaderTests.cs
 delete mode 100644 Rewind/paramore.integrationtests/ThinReadLayer/VenueReaderTests.cs
 delete mode 100644 Rewind/paramore.integrationtests/packages.config
 delete mode 100644 Rewind/paramore.integrationtests/paramore.integrationtests.csproj
 delete mode 100644 Rewind/paramore.services/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.services/ThinReadLayer/IAmAViewModelReader.cs
 delete mode 100644 Rewind/paramore.services/ThinReadLayer/IViewModelReader.cs
 delete mode 100644 Rewind/paramore.services/packages.config
 delete mode 100644 Rewind/paramore.services/paramore.services.csproj
 delete mode 100644 Rewind/paramore.sln
 delete mode 100644 Rewind/paramore.utility/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.utility/paramore.utility.csproj

[33mcommit 3c4f779129a0c4912ccacff873587f4296a05816[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Feb 25 15:25:53 2015 +0000

    Delete README.md

 delete mode 100644 Brighter/Examples/README.md

[33mcommit 9bb6baf698d97c6fe00c69943ca0fa16d0e12652[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Feb 24 16:40:49 2015 +0000

    Move TaskMailer to use direct project references over Nuget packages. Reduce the number of duplicate packages

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/RavenTests/CanCallLastOnArray.cs

[33mcommit 3c3601341f0ef4f530f76402983c5d47d0d270eb[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Mon Feb 23 13:28:00 2015 +0000

    Merging the policy and commandprocessor projects
    It increasingly made no sense to have both of these. We ask you to configure a policy registry but we don't make it clear what that is for
    unless you use the Policy package we don't offer much insight.
    Using the command processor features of Brighter to provide quality of service guarantees through policy is a key part of our use case,
    so we should make it clear what you can do with policy by including these in the core project.
    It is worth noting that the package naming conventions mean that this should be non-breaking change as the same namespace is used whether as a standalone
    project or as part of the top level project

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/.gitignore
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/Attributes/TimeoutPolicyAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/Attributes/UsePolicyAtttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/Handlers/ExceptionPolicyHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/Handlers/TimeoutPolicyHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/Properties/AssemblyInfo.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/paramore.brighter.commandprocessor.policy.2.0.1.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/paramore.brighter.commandprocessor.policy.csproj
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/paramore.brighter.commandprocessor.policy.nuspec

[33mcommit 5170402620b894c82b6384d0f69b4aa4abcbedc9[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Feb 17 12:29:39 2015 +0000

    Provide a channel failure message to wrap channel errors and allow message pump retry

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/TestDoubles/TestRmqMessageConsumer.cs

[33mcommit 6106c59588653f2b25c08d1a8a33514ea3d5a27c[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Feb 11 16:10:46 2015 +0000

    control bus has standard channels

 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/Ports/Commands/ConfigurationMessage.cs

[33mcommit 4303b39b81c298a64a8275be5e70015edb3ab5bc[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Feb 6 17:32:11 2015 +0000

    Drop Common.Logging in favour of LibLog

 delete mode 100644 Brighter/paramore.brighter.restms.core/packages.config
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/packages.config

[33mcommit 344dfb0de8a7e1e7bae682e9f55e2b20eaad390f[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Jan 30 17:38:15 2015 +0000

    make controlbus configurable item within serviceactivator

 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/ControlBusConfiguration/ControlBusConfigurationSection.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/Properties/AssemblyInfo.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/app.config.install.xdt
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/app.config.uninstall.xdt
 delete mode 100644 Brighter/paramore.brighter.serviceactivator.controlbus/paramore.brighter.serviceactivator.controlbus.csproj

[33mcommit 18edf44e1eb7e2b67c910140a40ad57d9b94c234[m
Author: Toby Henderson <toby.henderson@huddle.com>
Date:   Tue Jan 13 19:12:33 2015 +0000

    Updates for Nuget Packages

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.mssql/NuGetPkgMake.bat
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.mssql/paramore.brighter.commandprocessor.messagestore.mssql.1.0.0.0.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/NuGetPkgMake.bat
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/paramore.brighter.commandprocessor.messagestore.ravendb.1.0.0.0.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/NuGetPkgMake.bat
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/paramore.brighter.commandprocessor.messaginggateway.rmq.1.0.0.0.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/NuGetPkgMake.bat
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.policy/Paramore.Brighter.CommandProcessor.ExceptionPolicy.1.0.0.0.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/NuGetPkgMake.bat
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/Paramore.Brighter.CommandProcessor.1.0.0.0.nuspec
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/NuGetPkgMake.bat
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/paramore.brighter.serviceactivator.1.0.0.0.nuspec

[33mcommit dd751ac817de61322e29860fb40137e38b672fed[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Dec 31 15:01:06 2014 +0000

    Working on the RestMS messagegateway

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.restms/RestMSClientRequestHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.restms/RestMSServerRequestHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessagingGateway/restms/RestMSClientTests.cs

[33mcommit e4e04576a6b633f915dd59bb12ae0d9da81cb752[m
Author: Toby Henderson <toby.henderson@huddle.com>
Date:   Tue Dec 23 16:57:55 2014 +0000

    Removing ignored files

 delete mode 100644 Brighter/Examples/Tasks/App_Data/Tasks.sdf
 delete mode 100644 Brighter/Examples/tasklist/App_Data/Tasks.sdf
 delete mode 100644 Brighter/Paramore.CommandProcessor.sln.GhostDoc.xml
 delete mode 100644 Rewind/_ReSharper.paramore/JbDecompilerCache/decompiler/Raven.Client.Lightweight-48e2/t/Raven/Client/Document/DocumentStore.cs
 delete mode 100644 Rewind/_ReSharper.paramore/JbDecompilerCache/decompiler/Raven.Client.Lightweight-55ec/t/Raven/Client/IDocumentStore.cs
 delete mode 100644 Rewind/_ReSharper.paramore/JbDecompilerCache/decompiler/Raven.Client.Lightweight-a53c/t/Raven/Client/IDocumentSession.cs
 delete mode 100644 Rewind/_ReSharper.paramore/JbDecompilerCache/decompiler/Raven.Client.Lightweight-a53c/t/Raven/Client/Linq/IRavenQueryable`1.cs
 delete mode 100644 Rewind/_ReSharper.paramore/JbDecompilerCache/decompiler/Raven.Client.Lightweight-d271/t/Raven/Client/Document/DocumentStore.cs

[33mcommit c3cd4c16611e56804d9365c0d9263d36a35cef18[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Dec 19 12:12:00 2014 +0000

    Combine the *policy projects into one, no need for seperate packages

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.exceptionpolicy/paramore.brighter.commandprocessor.exceptionpolicy.csproj
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.timeoutpolicy/.gitignore
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.timeoutpolicy/Attributes/TimeoutPolicyAttribute.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.timeoutpolicy/Handlers/TimeoutPolicyHandler.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.timeoutpolicy/NuGetPkgMake.bat
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.timeoutpolicy/Properties/AssemblyInfo.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.timeoutpolicy/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.timeoutpolicy/paramore.brighter.commandprocessor.timeoutpolicy.1.0.0.0.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.timeoutpolicy/paramore.brighter.commandprocessor.timeoutpolicy.csproj

[33mcommit 800a27a992231b67282132dc6ecac54f66d582c9[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Dec 18 20:11:04 2014 +0000

    Added in the Postel message handling

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/RMQInputChannel.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/RMQMessagingGateway.cs

[33mcommit d3f9da488661fe81e8dc7f3f09da72fa14bc20d3[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Dec 17 19:33:57 2014 +0000

    Working on client for RestMS

 delete mode 100644 Brighter/Examples/restsms.hawkauthentication/App.config
 delete mode 100644 Brighter/Examples/restsms.hawkauthentication/Program.cs
 delete mode 100644 Brighter/Examples/restsms.hawkauthentication/packages.config

[33mcommit b8146dad26ab792a0da0558c44b8d29198e91d4c[m
Author: Bob Gregory <bob@huddle.net>
Date:   Tue Dec 9 16:27:42 2014 +0000

    tidying up

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/MessageTypeReader.cs

[33mcommit 0ec70fac8f6d01a3936cb6f2485ef0b29604efbb[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Nov 28 16:35:35 2014 +0000

    Trying to work through formatting issues between .NET and desired XML output

 delete mode 100644 Brighter/Examples/AcceptanceTests/AcceptanceTests.pyproj
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Service/UnityResolver.cs

[33mcommit 54487059e3fe9b3fe15f5c42e17cb4b09d267147[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Nov 12 19:02:30 2014 +0000

    server config

 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Configuration/UnityMessageMapperFactory.cs

[33mcommit c27bb7f30d051c6711fe7332e56f6dda138c18e7[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Nov 5 17:08:24 2014 +0000

    Post big edit cleanup of the files

 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Commands/RemoveMessageCommand.cs

[33mcommit 5a961af31b7e6543016efd97ec1375a32b17a7e4[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Nov 5 16:01:25 2014 +0000

    Copy messages into pipes; delete kills message for that pipe only and
    disposes content; href uses pipe address

 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Repositories/InMemoryMessageRepository.cs

[33mcommit 8092abbdb01c81afb3573ca802360d3741e03c48[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Oct 31 17:07:16 2014 +0000

    More work on RestMS server

 delete mode 100644 Brighter/paramore.brighter.restms.core/Ports/Resources/RestMS.cs

[33mcommit 81515d97cf669591c112adfcb93c69c2d0a548d5[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Sep 27 14:50:22 2014 +0100

    Switch to Unity, avoids multiple IoC problem and works easily with WebAPI
    Fix issues, first version of running server with default domain exposed

 delete mode 100644 Brighter/paramore.brighter.restms.server/TinyIoC.cs

[33mcommit 0680bea67fec67d7cfc0fba05cef7e8a2df42f89[m
Author: iancooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Sep 27 10:54:50 2014 +0100

    Split restms between testable core and adapter layer which we don't want
    to unit test. Prevents issues where we pull TinyIoC into the unit tests
    which already has a TinyIoC implementation for testing purposes

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/RestMSServer/RestMSServerBuilderTests.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Repositories/InMemoryFeedRepository.cs
 delete mode 100644 Brighter/paramore.brighter.restms.server/Ports/Commands/deleteme.txt
 delete mode 100644 Brighter/paramore.brighter.restms.server/Ports/Handlers/deleteme.txt
 delete mode 100644 Brighter/paramore.brighter.restms.server/Ports/ViewModelRetrievers/deleteme.txt

[33mcommit 333da6a22f897a71704f22c5ed14fb7b29bd78d2[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Mon Sep 22 20:53:51 2014 +0100

    RestMS Domain Model

 delete mode 100644 Brighter/paramore.brighter.restms.server/Adapters/Repositories/DomainRepository.cs

[33mcommit 917b8158197d2721b362d194b118814f79b64362[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Sep 3 09:46:10 2014 +0100

    Moving the renegade project to the Boneyard to avoid confusion with
    Brighter and the Paramore example

 delete mode 100644 Renegade/UserGroupManagement.CommandHandlers/IHandleCommands.cs
 delete mode 100644 Renegade/UserGroupManagement.CommandHandlers/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/UserGroupManagement.CommandHandlers/ScheduleMeetingCommandHandler.cs
 delete mode 100644 Renegade/UserGroupManagement.CommandHandlers/UserGroupManagement.CommandHandlers.csproj
 delete mode 100644 Renegade/UserGroupManagement.Commands/Command.cs
 delete mode 100644 Renegade/UserGroupManagement.Commands/ICommand.cs
 delete mode 100644 Renegade/UserGroupManagement.Commands/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/UserGroupManagement.Commands/ScheduleMeetingCommand.cs
 delete mode 100644 Renegade/UserGroupManagement.Commands/UserGroupManagement.Commands.csproj
 delete mode 100644 Renegade/UserGroupManagement.Configuration/ConfigurationHelper.cs
 delete mode 100644 Renegade/UserGroupManagement.Configuration/DomainDatabaseBootStrapper.cs
 delete mode 100644 Renegade/UserGroupManagement.Configuration/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/UserGroupManagement.Configuration/ReportingDatabaseBootStrapper.cs
 delete mode 100644 Renegade/UserGroupManagement.Configuration/UserGroupManagement.Configuration.csproj
 delete mode 100644 Renegade/UserGroupManagement.Domain/Common/Address.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Common/EmailAddress.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Common/PhoneNumber.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Location/ContactName.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Location/Location.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Location/LocationContact.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Location/LocationFactory.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Location/LocationMap.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Meetings/Meeting.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Meetings/MeetingFactory.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Momentos/LocationMemento.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Momentos/LocationName.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Momentos/MeetingMemento.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Momentos/SpeakerMemento.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Speakers/Speaker.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Speakers/SpeakerBio.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Speakers/SpeakerFactory.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/Speakers/SpeakerName.cs
 delete mode 100644 Renegade/UserGroupManagement.Domain/UserGroupManagement.Domain.csproj
 delete mode 100644 Renegade/UserGroupManagement.Events/Location/LocationCreatedEvent.cs
 delete mode 100644 Renegade/UserGroupManagement.Events/Meeting/MeetingScheduledEvent.cs
 delete mode 100644 Renegade/UserGroupManagement.Events/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/UserGroupManagement.Events/Speaker/SpeakerCreatedEvent.cs
 delete mode 100644 Renegade/UserGroupManagement.Events/UserGroupManagement.Events.csproj
 delete mode 100644 Renegade/UserGroupManagement.Features/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/UserGroupManagement.Features/ScheduleAMeeting.feature.cs
 delete mode 100644 Renegade/UserGroupManagement.Features/Steps/ScheduleAMeetingSteps.cs
 delete mode 100644 Renegade/UserGroupManagement.Features/UserGroupManagement.Features.csproj
 delete mode 100644 Renegade/UserGroupManagement.Reporting.Dto/MeetingDetailsReport.cs
 delete mode 100644 Renegade/UserGroupManagement.Reporting.Dto/MeetingReport.cs
 delete mode 100644 Renegade/UserGroupManagement.Reporting.Dto/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/UserGroupManagement.Reporting.Dto/UserGroupManagement.Reporting.Dto.csproj
 delete mode 100644 Renegade/UserGroupManagement.SlowTests/ConcerningScheduleMeetingCommandHandler/WhenSchedulingANewMeeting.cs
 delete mode 100644 Renegade/UserGroupManagement.SlowTests/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/UserGroupManagement.SlowTests/Reporting/Dto/ReportFixture.cs
 delete mode 100644 Renegade/UserGroupManagement.SlowTests/UserGroupManagement.SlowTests.csproj
 delete mode 100644 Renegade/UserGroupManagement.Tests/Commands/ShouldHaveCommandHandlerForCommand.cs
 delete mode 100644 Renegade/UserGroupManagement.Tests/Commands/ShouldHaveSerializeableCommands.cs
 delete mode 100644 Renegade/UserGroupManagement.Tests/Configuration/ConcerningConfigurationHelper/WhenLookingForAllTheCommandHandlers.cs
 delete mode 100644 Renegade/UserGroupManagement.Tests/Configuration/ConcerningConfigurationHelper/WhenLookingForAllTheCommands.cs
 delete mode 100644 Renegade/UserGroupManagement.Tests/Domain/ConcerningLocations/WhenCreatingAMementoForALocation.cs
 delete mode 100644 Renegade/UserGroupManagement.Tests/Domain/ConcerningMeetings/WhenCreatingAMementoForAMeeting.cs
 delete mode 100644 Renegade/UserGroupManagement.Tests/Domain/ConcerningSpeakers/WhenCreatingAMementoForASpeaker.cs
 delete mode 100644 Renegade/UserGroupManagement.Tests/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/UserGroupManagement.Tests/UserGroupManagement.Tests.csproj
 delete mode 100644 Renegade/UserGroupManagement.Utility/EnumerationExtensions.cs
 delete mode 100644 Renegade/UserGroupManagement.Utility/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/UserGroupManagement.Utility/UserGroupManagement.Utility.csproj
 delete mode 100644 Renegade/UserGroupManagement.sln
 delete mode 100644 Renegade/UserGroupManagement/Controllers/AccountController.cs
 delete mode 100644 Renegade/UserGroupManagement/Controllers/HomeController.cs
 delete mode 100644 Renegade/UserGroupManagement/Global.asax
 delete mode 100644 Renegade/UserGroupManagement/Global.asax.cs
 delete mode 100644 Renegade/UserGroupManagement/Models/AccountModels.cs
 delete mode 100644 Renegade/UserGroupManagement/Properties/AssemblyInfo.cs
 delete mode 100644 Renegade/UserGroupManagement/UserGroupManagement.csproj
 delete mode 100644 Renegade/UserGroupManagement/Views/Account/ChangePassword.aspx
 delete mode 100644 Renegade/UserGroupManagement/Views/Account/ChangePasswordSuccess.aspx
 delete mode 100644 Renegade/UserGroupManagement/Views/Account/LogOn.aspx
 delete mode 100644 Renegade/UserGroupManagement/Views/Account/Register.aspx
 delete mode 100644 Renegade/UserGroupManagement/Views/Home/About.aspx
 delete mode 100644 Renegade/UserGroupManagement/Views/Home/Index.aspx
 delete mode 100644 Renegade/UserGroupManagement/Views/Shared/Error.aspx

[33mcommit 9c45f7c321c48e394e3b8308ee4195b9dd1dc138[m
Author: iancooper <ian@ukwsianco01.huddle.local>
Date:   Tue Sep 2 13:52:26 2014 +0100

    Name changes to better reflect POSA patterns

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/IAmASendMessageGateway.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/IAmAReceiveMessageGateway.cs

[33mcommit 95e446950dc4f6d5d7289119835096377a97c136[m
Author: iancooper <ian@ukwsianco01.huddle.local>
Date:   Tue Aug 26 17:13:10 2014 +0100

    Improving documentation

 delete mode 100644 Brighter/paramore.brighter.commandprocessor/LifetimeManager.cs

[33mcommit 819bdb7ffc8d4880e762b3601ff578dd56b170d2[m
Author: iancooper <ian@ukwsianco01.huddle.local>
Date:   Tue Jul 29 15:21:25 2014 +0100

    Now split the gateway into send and receive as well

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/RMQMessagingGateway.cs

[33mcommit f844864280a2d90b9444b7feb941761f38dedc61[m
Author: iancooper <ian@ukwsianco01.huddle.local>
Date:   Thu Jul 3 11:47:26 2014 +0100

    replacing conforming container

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/CommandProcessors/CommandProcessorFactoryTests.cs

[33mcommit 127856a1cecd9b47910c3ae1ae687e639dd49a9c[m
Author: iancooper <ian@ukwsianco01.huddle.local>
Date:   Wed Jul 2 18:44:39 2014 +0100

    Dropping conforming container

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.ioccontainers.tinyioc/.gitignore
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.ioccontainers.tinyioc/Adapters/TinyIoCAdapter.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.ioccontainers.tinyioc/NuGetPkgMake.bat
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.ioccontainers.tinyioc/Properties/AssemblyInfo.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.ioccontainers.tinyioc/packages.config
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.ioccontainers.tinyioc/paramore.brighter.commandprocessor.ioccontainers.tinyioc.1.0.0.0.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.ioccontainers.tinyioc/paramore.brighter.commandprocessor.ioccontainers.tinyioc.csproj
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/IoCContainers/TinyIoCContainerFixture.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/CommandProcessorFactory.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/TrackedServiceLocator.cs

[33mcommit 056bf9b2c1169ca640b61d299d47d15905766884[m
Author: iancooper <ian@ukwsianco01.huddle.local>
Date:   Wed Jun 11 17:30:42 2014 +0100

    Moving to a common core for the example - broken check-in

 delete mode 100644 Brighter/Examples/TaskMailer/Adapters/MailGateway/IAmAMailGateway.cs
 delete mode 100644 Brighter/Examples/TaskMailer/Ports/TaskReminderCommand.cs

[33mcommit e74abab03fca14903aac0a4774a86a3d66703af0[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri May 30 10:12:31 2014 +0100

    Updating nuget packages for builders and use with service activators

 delete mode 100644 Brighter/paramore.brighter.serviceactivator/CommandProcessorConfiguration.cs

[33mcommit 40b4a122778027092e8dc1f36a9ff1fddfc7e7b3[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu May 29 16:36:38 2014 +0100

    Make it easier to configure

 delete mode 100644 Brighter/_ReSharper.Paramore.CommandProcessor/JbDecompilerCache/decompiler/Simple.Data-57fc/t/Simple/Data/IAdapterTransaction.cs
 delete mode 100644 Brighter/_ReSharper.Paramore.CommandProcessor/JbDecompilerCache/decompiler/Simple.Data-57fc/t/Simple/Data/IAdapterWithTransactions.cs
 delete mode 100644 Brighter/_ReSharper.Paramore.CommandProcessor/JbDecompilerCache/decompiler/mscorlib-d4f7/t/System/IDisposable.cs

[33mcommit 9ee9a649b392b63f8877a8ba20d74ea2a06a5e87[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed May 7 19:43:29 2014 +0100

    Get the configuration section working

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatch/MessageDispatchConfiguration.cs

[33mcommit c7b197e0189892669026426006fd08df8894f15f[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sun Apr 27 14:17:19 2014 +0100

    Cleaning up the NuGet packaging

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.exceptionpolicy/Paramore.Brighter.CommandProcessor.ExceptionPolicy.1.0.0-alpha001.Symbols.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.exceptionpolicy/Paramore.Brighter.CommandProcessor.ExceptionPolicy.1.0.0-alpha001.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.exceptionpolicy/Paramore.Brighter.CommandProcessor.ExceptionPolicy.1.0.0-alpha001.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.ioccontainers.tinyioc/paramore.brighter.commandprocessor.ioccontainers.tinyioc.1.0.0-alpha001.Symbols.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.ioccontainers.tinyioc/paramore.brighter.commandprocessor.ioccontainers.tinyioc.1.0.0-alpha001.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.ioccontainers.tinyioc/paramore.brighter.commandprocessor.ioccontainers.tinyioc.1.0.0-alpha001.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/paramore.brighter.commandprocessor.messagestore.ravendb.1.0.0-alpha001.Symbols.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/paramore.brighter.commandprocessor.messagestore.ravendb.1.0.0-alpha001.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/paramore.brighter.commandprocessor.messagestore.ravendb.1.0.0-alpha001.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/paramore.brighter.commandprocessor.messaginggateway.rmq.1.0.0-alpha001.Symbols.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/paramore.brighter.commandprocessor.messaginggateway.rmq.1.0.0-alpha001.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messaginggateway.rmq/paramore.brighter.commandprocessor.messaginggateway.rmq.1.0.0-alpha001.symbols.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.timeoutpolicy/paramore.brighter.commandprocessor.timeoutpolicy.1.0.0-alpha001.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.timeoutpolicy/paramore.brighter.commandprocessor.timeoutpolicy.1.0.0-alpha001.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor.timeoutpolicy/paramore.brighter.commandprocessor.timeoutpolicy.1.0.0-alpha001.symbols.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/Paramore.Brighter.CommandProcessor.1.0.0-alpha001.Symbols.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/Paramore.Brighter.CommandProcessor.1.0.0-alpha001.Symbols.nuspec
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/Paramore.Brighter.CommandProcessor.1.0.0-alpha001.nupkg
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/Paramore.Brighter.CommandProcessor.1.0.0-alpha001.nuspec
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/paramore.brighter.serviceactivator.1.0.0-alpha001.Symbols.nupkg
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/paramore.brighter.serviceactivator.1.0.0-alpha001.nupkg
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/paramore.brighter.serviceactivator.1.0.0-alpha001.nuspec

[33mcommit 7925b8c703db80dbdaa5ba62a9eb8f352234bfff[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Apr 26 19:59:29 2014 +0100

    RMQ implementation of gateway and channel

 delete mode 100644 Brighter/paramore.brighter.serviceactivator/IAmAMessageChannel.cs

[33mcommit ae456fe61d80a982bb751f7ef0c33026b4145d78[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Apr 23 00:03:13 2014 +0100

    Rename away from switchboard metaphor

 delete mode 100644 Brighter/paramore.brighter.serviceactivator/Lamp.cs
 delete mode 100644 Brighter/paramore.brighter.serviceactivator/Plug.cs

[33mcommit 4df0908ef30e18c93e64e9af4ebd55eb904c33d8[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Mon Apr 21 16:58:13 2014 +0100

    First version of dispatcher - lots more to do

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.tests/MessageDispatcher/MessageDispatchFixture.cs

[33mcommit 1ccc9a2be171c342023ae812c0958473dc4d8690[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Apr 19 15:26:23 2014 +0100

    Flow logging through the command processor

 delete mode 100644 Brighter/Examples/tasklist/Ports/Handlers/TraceHandler.cs
 delete mode 100644 Brighter/Examples/tasklist/Ports/ITraceOutput.cs
 delete mode 100644 Brighter/Examples/tasklist/Ports/TraceAttribute.cs
 delete mode 100644 Brighter/Examples/tasklist/Utilities/ConsoleTrace.cs
 delete mode 100644 Brighter/paramore.brighter.commandprocessor/RequestLoggingHandlerAttribute.cs

[33mcommit 9a9c3862da2f2d825e90934e933cc094bebb42ef[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sun Apr 13 22:20:02 2014 +0100

    Install and uninstall of configuration elements

 delete mode 100644 Brighter/paramore.brighter.commandprocessor.messagestore.ravendb/app.config

[33mcommit 63fc077f5ed4b0d99509f479a0a703df78622166[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Apr 10 16:30:02 2014 +0100

    Dont' include

 delete mode 100644 Brighter/build/NuGet/Paramore.Brighter.CommandProcessor.1.0.0-alpha.nupkg

[33mcommit 6c17da1b008fc179a81ec27e10704c05186d97aa[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Apr 8 12:26:52 2014 +0100

    Making some change pre-producing a new NuGet Build

 delete mode 100644 Brighter/build/NuGet/Paramore.CommandProcessor.1.0.0.1.nupkg
 delete mode 100644 Brighter/build/NuGet/Paramore.CommandProcessor.IocContainers.1.0.0.1.nupkg

[33mcommit 3ae4b27639a630ff514ec3d6fa4c8898a25962a6[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Mon Mar 31 19:02:00 2014 +0100

    Working on the rmq gateway

 delete mode 100644 Brighter/paramore.commandprocessor.tests/RavenTests/CanCallLastOnArray.cs

[33mcommit 1c19b840ef05af68f0738e7f5bca0f84c741bbd2[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Mar 26 13:31:04 2014 +0000

    Preparing to write a Raven DB message store

 delete mode 100644 Brighter/paramore.commandprocessor/CommandMessage.cs

[33mcommit e9c314d9e6166717cba4bf3c5e14dd77285dd703[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Mar 15 20:21:11 2014 +0000

    Working on timeout for a pipeline

 delete mode 100644 Brighter/paramore.commandprocessor.timeoutpolicy/Extensions/TaskTimeoutExtensions.cs

[33mcommit 99c3011cd0a6c31c711c31c8dc91ff23935e13d1[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Feb 26 12:11:29 2014 +0000

    Playing with surfacing chain of responsibility in IOC

 delete mode 100644 Brighter/paramore.commandprocessor/Pipelines.cs

[33mcommit f70f4690a5269498626bdfeeece059c0d911eda7[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Feb 26 11:55:29 2014 +0000

    Rename Chain of Responsibility to Pipeline - we execute all steps, not
    just one

 delete mode 100644 Brighter/paramore.commandprocessor.tests/CommandProcessors/ChainOfResponsibilityTests.cs
 delete mode 100644 Brighter/paramore.commandprocessor/IChainofResponsibilityBuilder.cs

[33mcommit 5804b1b5df2dd034f45e3fd0b4e3c8f28829569b[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Feb 12 22:33:29 2014 +0000

    Fix the source of the hostname

 delete mode 100644 Brighter/Examples/tasklist/Adapters/API/Resources/TaskListGlobals.cs

[33mcommit add5aa622a9ee7e5a4a8874db764cc5a8424ca66[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Feb 6 14:34:24 2014 +0000

    Changes to get the task example working, driven by Python system tests

 delete mode 100644 Brighter/Examples/AcceptanceTests/AcceptanceTests.py

[33mcommit b859ffe921ae71d5422eda202e65a86b1265796e[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Jan 29 15:12:28 2014 +0000

    Interim - working on problems exposed by system tests

 delete mode 100644 Brighter/Examples/tasklist/Adapters/API/Handlers/TaskListEndPointHandler.cs

[33mcommit 82300635e35f2b688e0406bf483a4ec470c77d9e[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Jan 28 12:24:11 2014 +0000

    fixing package library naming issues and versions

 delete mode 100644 Rewind/Packages/AmplifyJS.1.1.0/AmplifyJS.1.1.0.nupkg
 delete mode 100644 Rewind/Packages/FakeItEasy.1.10.0/FakeItEasy.1.10.0.nupkg
 delete mode 100644 Rewind/Packages/Machine.Fakes.1.4.0/Machine.Fakes.1.4.0.nupkg
 delete mode 100644 Rewind/Packages/Machine.Fakes.FakeItEasy.1.4.0/Machine.Fakes.FakeItEasy.1.4.0.nupkg
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.12/Machine.Specifications.0.5.12.nupkg
 delete mode 100644 Rewind/Packages/Microsoft.AspNet.Mvc.4.0.20710.0/Microsoft.AspNet.Mvc.4.0.20710.0.nupkg
 delete mode 100644 Rewind/Packages/Microsoft.AspNet.Razor.2.0.20715.0/Microsoft.AspNet.Razor.2.0.20715.0.nupkg
 delete mode 100644 Rewind/Packages/Microsoft.AspNet.Web.Optimization.1.0.0/Microsoft.AspNet.Web.Optimization.1.0.0.nupkg
 delete mode 100644 Rewind/Packages/Microsoft.AspNet.WebPages.2.0.20710.0/Microsoft.AspNet.WebPages.2.0.20710.0.nupkg
 delete mode 100644 Rewind/Packages/Microsoft.Web.Infrastructure.1.0.0.0/Microsoft.Web.Infrastructure.1.0.0.0.nupkg
 delete mode 100644 Rewind/Packages/Moment.js.2.0.0/Moment.js.2.0.0.nupkg
 delete mode 100644 Rewind/Packages/MomentDatepicker.1.1.0/MomentDatepicker.1.1.0.nupkg
 delete mode 100644 Rewind/Packages/NUnit.2.6.2/NUnit.2.6.2.nupkg
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/RavenDB.1.0.573.nupkg
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2330/RavenDB.Client.2.0.2330.nupkg
 delete mode 100644 Rewind/Packages/jQuery.1.9.1/jQuery.1.9.1.nupkg
 delete mode 100644 Rewind/Packages/json2.1.0.2/json2.1.0.2.nupkg
 delete mode 100644 Rewind/Packages/knockoutjs.2.2.1/knockoutjs.2.2.1.nupkg

[33mcommit 905bfc6f8d8b726b3987fc9c5dadee4a364bbdf2[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sun Dec 8 17:24:13 2013 +0000

    Get the examples for command processor working with SQL Compact

 delete mode 100644 Brighter/Examples/tasklist/CommandProcessorBootstrapper.cs
 delete mode 100644 Brighter/Examples/tasklist/Contributors/DependencyPipelineContributor.cs

[33mcommit 72fa0ac50e96b1b17157a1d1b89b48d4d6a28ef3[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Dec 7 17:52:27 2013 +0000

    cleaning up

 delete mode 100644 Brighter/Examples/AcceptanceTests/AcceptanceTests.py
 delete mode 100644 Brighter/Examples/AcceptanceTests/AcceptanceTests.pyproj
 delete mode 100644 Brighter/Examples/database/tasks.sqlite
 delete mode 100644 Brighter/Examples/tasklist/Libs/OpenRasta/OpenRasta.Hosting.AspNet.dll
 delete mode 100644 Brighter/Examples/tasklist/Libs/OpenRasta/OpenRasta.Hosting.AspNet.pdb
 delete mode 100644 Brighter/Examples/tasklist/Libs/OpenRasta/OpenRasta.dll
 delete mode 100644 Brighter/Examples/tasklist/Libs/OpenRasta/OpenRasta.pdb
 delete mode 100644 Brighter/Examples/tasklist/Tasklist.csproj
 delete mode 100644 Brighter/Examples/tasklist/Tasklist.csproj.user
 delete mode 100644 Brighter/Examples/tasklist/Web.Debug.config
 delete mode 100644 Brighter/Examples/tasklist/Web.Release.config
 delete mode 100644 Brighter/Examples/tasklist/Web.config
 delete mode 100644 Brighter/Examples/tasklist/packages.config
 delete mode 100644 Brighter/Paramore.CommandProcessor.sln
 delete mode 100644 Brighter/paramore.commandprocessor.ioccontainers/packages.config
 delete mode 100644 Brighter/paramore.commandprocessor.ioccontainers/paramore.commandprocessor.ioccontainers.csproj
 delete mode 100644 Brighter/paramore.commandprocessor.tests/packages.config
 delete mode 100644 Brighter/paramore.commandprocessor.tests/paramore.commandprocessor.tests.csproj
 delete mode 100644 Brighter/paramore.commandprocessor/packages.config
 delete mode 100644 Brighter/paramore.commandprocessor/paramore.commandprocessor.csproj
 delete mode 100644 README.md
 delete mode 100644 Renegade/Lib/Antlr3.Runtime.dll
 delete mode 100644 Renegade/Lib/Castle.Core.dll
 delete mode 100644 Renegade/Lib/Castle.Core.xml
 delete mode 100644 Renegade/Lib/Castle.DynamicProxy2.dll
 delete mode 100644 Renegade/Lib/Castle.DynamicProxy2.xml
 delete mode 100644 Renegade/Lib/Castle.Facilities.Logging.dll
 delete mode 100644 Renegade/Lib/Castle.Facilities.Logging.xml
 delete mode 100644 Renegade/Lib/Castle.MicroKernel.dll
 delete mode 100644 Renegade/Lib/Castle.MicroKernel.xml
 delete mode 100644 Renegade/Lib/Castle.Services.Logging.NLogIntegration.dll
 delete mode 100644 Renegade/Lib/Castle.Services.Logging.NLogIntegration.xml
 delete mode 100644 Renegade/Lib/Castle.Services.Logging.log4netIntegration.dll
 delete mode 100644 Renegade/Lib/Castle.Services.Logging.log4netIntegration.xml
 delete mode 100644 Renegade/Lib/Castle.Windsor.dll
 delete mode 100644 Renegade/Lib/Castle.Windsor.xml
 delete mode 100644 Renegade/Lib/Fohjin.DDD.Bus.dll
 delete mode 100644 Renegade/Lib/Fohjin.DDD.Bus.pdb
 delete mode 100644 Renegade/Lib/Fohjin.DDD.EventStore.SQLite.dll
 delete mode 100644 Renegade/Lib/Fohjin.DDD.EventStore.SQLite.pdb
 delete mode 100644 Renegade/Lib/Fohjin.DDD.EventStore.dll
 delete mode 100644 Renegade/Lib/Fohjin.DDD.EventStore.pdb
 delete mode 100644 Renegade/Lib/Fohjin.DDD.Events.dll
 delete mode 100644 Renegade/Lib/Fohjin.DDD.Events.pdb
 delete mode 100644 Renegade/Lib/Fohjin.DDD.Reporting.dll
 delete mode 100644 Renegade/Lib/Fohjin.DDD.Reporting.pdb
 delete mode 100644 Renegade/Lib/NLog.dll
 delete mode 100644 Renegade/Lib/NLog.xml
 delete mode 100644 Renegade/Lib/NLog.xsd
 delete mode 100644 Renegade/Lib/Rhino.Mocks.dll
 delete mode 100644 Renegade/Lib/Rhino.Mocks.xml
 delete mode 100644 Renegade/Lib/SQLite.NET/Doc/SQLite.NET.chm
 delete mode 100644 Renegade/Lib/SQLite.NET/readme.htm
 delete mode 100644 Renegade/Lib/SQLite/sqlite-3_6_23_1.zip
 delete mode 100644 Renegade/Lib/SQLite/sqlite3.def
 delete mode 100644 Renegade/Lib/SQLite/sqlite3.dll
 delete mode 100644 Renegade/Lib/SQLite/sqlite3.exe
 delete mode 100644 Renegade/Lib/SQLite/sqlite3_analyzer-3.6.1.zip
 delete mode 100644 Renegade/Lib/SQLite/sqlite3_analyzer.exe
 delete mode 100644 Renegade/Lib/SQLite/sqlitedll-3_6_23_1.zip
 delete mode 100644 Renegade/Lib/SpecUnit.dll
 delete mode 100644 Renegade/Lib/SpecUnit.pdb
 delete mode 100644 Renegade/Lib/TechTalk.SpecFlow.Generator.dll
 delete mode 100644 Renegade/Lib/TechTalk.SpecFlow.Parser.dll
 delete mode 100644 Renegade/Lib/TechTalk.SpecFlow.Reporting.dll
 delete mode 100644 Renegade/Lib/TechTalk.SpecFlow.VsIntegration.dll
 delete mode 100644 Renegade/Lib/TechTalk.SpecFlow.dll
 delete mode 100644 Renegade/Lib/TechTalk.SpecFlow.targets
 delete mode 100644 Renegade/Lib/TechTalk.SpecFlow.tasks
 delete mode 100644 Renegade/Lib/acknowledgements.txt
 delete mode 100644 Renegade/Lib/changelog.txt
 delete mode 100644 Renegade/Lib/license.txt
 delete mode 100644 Renegade/Lib/log4net.dll
 delete mode 100644 Renegade/Lib/log4net.license.txt
 delete mode 100644 Renegade/Lib/log4net.xml
 delete mode 100644 Renegade/Lib/nunit.framework.dll
 delete mode 100644 Renegade/Lib/nunit.framework.xml
 delete mode 100644 Renegade/Lib/specflow.exe
 delete mode 100644 Renegade/Lib/test.exe
 delete mode 100644 Renegade/Lib/test.exe.config
 delete mode 100644 Renegade/NothingToSeeHere.txt
 delete mode 100644 Renegade/UserGroupManagement.CommandHandlers/UserGroupManagement.CommandHandlers.csproj
 delete mode 100644 Renegade/UserGroupManagement.Commands/UserGroupManagement.Commands.csproj
 delete mode 100644 Renegade/UserGroupManagement.Commands/UserGroupManagement.Commands.csproj.user
 delete mode 100644 Renegade/UserGroupManagement.Configuration/UserGroupManagement.Configuration.csproj
 delete mode 100644 Renegade/UserGroupManagement.Configuration/UserGroupManagement.Configuration.csproj.user
 delete mode 100644 Renegade/UserGroupManagement.Domain/UserGroupManagement.Domain.csproj
 delete mode 100644 Renegade/UserGroupManagement.Events/UserGroupManagement.Events.csproj
 delete mode 100644 Renegade/UserGroupManagement.Features/ScheduleAMeeting.feature
 delete mode 100644 Renegade/UserGroupManagement.Features/UserGroupManagement.Features.csproj
 delete mode 100644 Renegade/UserGroupManagement.Reporting.Dto/UserGroupManagement.Reporting.Dto.csproj
 delete mode 100644 Renegade/UserGroupManagement.SlowTests/UserGroupManagement.SlowTests.csproj
 delete mode 100644 Renegade/UserGroupManagement.Tests/UserGroupManagement.Tests.csproj
 delete mode 100644 Renegade/UserGroupManagement.Utility/UserGroupManagement.Utility.csproj
 delete mode 100644 Renegade/UserGroupManagement.sln
 delete mode 100644 Renegade/UserGroupManagement/Content/Site.css
 delete mode 100644 Renegade/UserGroupManagement/Global.asax
 delete mode 100644 Renegade/UserGroupManagement/Scripts/MicrosoftAjax.debug.js
 delete mode 100644 Renegade/UserGroupManagement/Scripts/MicrosoftAjax.js
 delete mode 100644 Renegade/UserGroupManagement/Scripts/MicrosoftMvcAjax.debug.js
 delete mode 100644 Renegade/UserGroupManagement/Scripts/MicrosoftMvcAjax.js
 delete mode 100644 Renegade/UserGroupManagement/Scripts/MicrosoftMvcValidation.debug.js
 delete mode 100644 Renegade/UserGroupManagement/Scripts/MicrosoftMvcValidation.js
 delete mode 100644 Renegade/UserGroupManagement/Scripts/jquery-1.4.1-vsdoc.js
 delete mode 100644 Renegade/UserGroupManagement/Scripts/jquery-1.4.1.js
 delete mode 100644 Renegade/UserGroupManagement/Scripts/jquery-1.4.1.min.js
 delete mode 100644 Renegade/UserGroupManagement/Scripts/jquery.validate-vsdoc.js
 delete mode 100644 Renegade/UserGroupManagement/Scripts/jquery.validate.js
 delete mode 100644 Renegade/UserGroupManagement/Scripts/jquery.validate.min.js
 delete mode 100644 Renegade/UserGroupManagement/UserGroupManagement.csproj
 delete mode 100644 Renegade/UserGroupManagement/UserGroupManagement.csproj.user
 delete mode 100644 Renegade/UserGroupManagement/Views/Shared/LogOnUserControl.ascx
 delete mode 100644 Renegade/UserGroupManagement/Views/Shared/Site.Master
 delete mode 100644 Renegade/UserGroupManagement/Views/Web.config
 delete mode 100644 Renegade/UserGroupManagement/Web.Debug.config
 delete mode 100644 Renegade/UserGroupManagement/Web.Release.config
 delete mode 100644 Renegade/UserGroupManagement/Web.config
 delete mode 100644 Rewind/.nuget/NuGet.Config
 delete mode 100644 Rewind/.nuget/NuGet.exe
 delete mode 100644 Rewind/.nuget/NuGet.targets
 delete mode 100644 Rewind/Debugging_Durandal_KO.txt
 delete mode 100644 Rewind/HTTPRequest_API_Tests_Fiddler.txt
 delete mode 100644 Rewind/Lib/Paramore.CommandProcessor/paramore.commandprocessor.dll
 delete mode 100644 Rewind/Lib/Paramore.CommandProcessor/paramore.commandprocessor.ioccontainers.dll
 delete mode 100644 Rewind/Lib/Paramore.CommandProcessor/paramore.commandprocessor.ioccontainers.pdb
 delete mode 100644 Rewind/Lib/Paramore.CommandProcessor/paramore.commandprocessor.pdb
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/.gitattributes
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/.gitignore
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/.htaccess
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/404.html
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/CHANGELOG.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/CONTRIBUTING.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/LICENSE.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/README.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon-114x114-precomposed.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon-144x144-precomposed.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon-57x57-precomposed.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon-72x72-precomposed.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon-precomposed.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/apple-touch-icon.png
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/crossdomain.xml
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/css/main.css
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/css/normalize.css
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/TOC.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/crossdomain.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/css.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/extend.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/faq.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/htaccess.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/html.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/js.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/misc.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/doc/usage.md
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/favicon.ico
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/humans.txt
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/img/.gitignore
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/index.html
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/js/main.js
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/js/plugins.js
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/js/vendor/jquery-1.8.3.min.js
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/js/vendor/modernizr-2.6.2.min.js
 delete mode 100644 Rewind/Lib/html5-boilerplate/h5bp-html5-boilerplate-0a340fb/robots.txt
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/OpenRasta.Testing.dll
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/OpenRasta.Testing.pdb
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/OpenRasta.dll
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/OpenRasta.pdb
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/Resources/error-test.htm
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/openrasta.testing.xml
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/bin-net35/openrasta.xml
 delete mode 100644 Rewind/Lib/openrasta-core-2.1.1.151056433/version
 delete mode 100644 Rewind/Lib/openrasta-hosting-aspnet-2.1.1+151058209/bin-net35/OpenRasta.Hosting.AspNet.dll
 delete mode 100644 Rewind/Lib/openrasta-hosting-aspnet-2.1.1+151058209/bin-net35/OpenRasta.Hosting.AspNet.pdb
 delete mode 100644 Rewind/Lib/openrasta-hosting-aspnet-2.1.1+151058209/bin-net35/OpenRasta.Server.XML
 delete mode 100644 Rewind/Lib/openrasta-hosting-aspnet-2.1.1+151058209/version
 delete mode 100644 Rewind/Packages/AmplifyJS.1.1.0/content/Scripts/amplify-vsdoc.js
 delete mode 100644 Rewind/Packages/AmplifyJS.1.1.0/content/Scripts/amplify.js
 delete mode 100644 Rewind/Packages/AmplifyJS.1.1.0/content/Scripts/amplify.min.js
 delete mode 100644 Rewind/Packages/Antlr34.3.4.19004.1/tools/Antlr3.exe.config
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/Durandal.1.2.0.nupkg
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/Durandal.1.2.0.nuspec
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/amd/almond-custom.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/amd/optimizer.exe
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/amd/r.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/amd/require.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/amd/text.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/app.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/composition.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/events.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/http.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/messageBox.html
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/messageBox.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/modalDialog.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/system.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/viewEngine.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/viewLocator.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/viewModel.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/viewModelBinder.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/App/durandal/widget.js
 delete mode 100644 Rewind/Packages/Durandal.1.2.0/content/Content/durandal.css
 delete mode 100644 Rewind/Packages/Durandal.Router.1.2.0/Durandal.Router.1.2.0.nupkg
 delete mode 100644 Rewind/Packages/Durandal.Router.1.2.0/Durandal.Router.1.2.0.nuspec
 delete mode 100644 Rewind/Packages/Durandal.Router.1.2.0/content/App/durandal/plugins/router.js
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/Durandal.StarterKit.1.2.0.nupkg
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/Durandal.StarterKit.1.2.0.nuspec
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/App/main-built.js
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/App/main.js
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/App/viewmodels/flickr.js
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/App/viewmodels/shell.js
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/App/viewmodels/welcome.js
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/App/views/detail.html
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/App/views/flickr.html
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/App/views/shell.html
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/App/views/welcome.html
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/App_Start/DurandalBundleConfig.cs.pp
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/App_Start/DurandalConfig.cs.pp
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/Content/app.css
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/Content/ie10mobile.css
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/Content/images/icon.png
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/Content/images/ios-startup-image-landscape.png
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/Content/images/ios-startup-image-portrait.png
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/Controllers/DurandalController.cs.pp
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/Views/Durandal/Index.cshtml
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/Views/Durandal/_splash.cshtml
 delete mode 100644 Rewind/Packages/Durandal.StarterKit.1.2.0/content/readme.md
 delete mode 100644 Rewind/Packages/Durandal.Transitions.1.2.0/Durandal.Transitions.1.2.0.nupkg
 delete mode 100644 Rewind/Packages/Durandal.Transitions.1.2.0/Durandal.Transitions.1.2.0.nuspec
 delete mode 100644 Rewind/Packages/Durandal.Transitions.1.2.0/content/App/durandal/transitions/entrance.js
 delete mode 100644 Rewind/Packages/FontAwesome.3.0.2.3/FontAwesome.3.0.2.3.nupkg
 delete mode 100644 Rewind/Packages/FontAwesome.3.0.2.3/FontAwesome.3.0.2.3.nuspec
 delete mode 100644 Rewind/Packages/FontAwesome.3.0.2.3/content/Content/font-awesome-ie7.min.css
 delete mode 100644 Rewind/Packages/FontAwesome.3.0.2.3/content/Content/font-awesome.css
 delete mode 100644 Rewind/Packages/FontAwesome.3.0.2.3/content/Content/font-awesome.min.css
 delete mode 100644 Rewind/Packages/FontAwesome.3.0.2.3/content/Content/font/FontAwesome.otf
 delete mode 100644 Rewind/Packages/FontAwesome.3.0.2.3/content/Content/font/fontawesome-webfont.eot
 delete mode 100644 Rewind/Packages/FontAwesome.3.0.2.3/content/Content/font/fontawesome-webfont.svg
 delete mode 100644 Rewind/Packages/FontAwesome.3.0.2.3/content/Content/font/fontawesome-webfont.ttf
 delete mode 100644 Rewind/Packages/FontAwesome.3.0.2.3/content/Content/font/fontawesome-webfont.woff
 delete mode 100644 Rewind/Packages/FontAwesome.3.0.2.3/tools/install.ps1
 delete mode 100644 Rewind/Packages/Glimpse.1.3.0/Glimpse.1.3.0.nupkg
 delete mode 100644 Rewind/Packages/Glimpse.1.3.0/Glimpse.1.3.0.nuspec
 delete mode 100644 Rewind/Packages/Glimpse.1.3.0/docs/Glimpse.Core.Documentation.chm
 delete mode 100644 Rewind/Packages/Glimpse.1.3.0/lib/net35/Glimpse.Core.dll
 delete mode 100644 Rewind/Packages/Glimpse.1.3.0/lib/net40/Glimpse.Core.dll
 delete mode 100644 Rewind/Packages/Glimpse.1.3.0/lib/net45/Glimpse.Core.dll
 delete mode 100644 Rewind/Packages/Glimpse.1.3.0/tools/glimpse.psm1
 delete mode 100644 Rewind/Packages/Glimpse.1.3.0/tools/init.ps1
 delete mode 100644 Rewind/Packages/Glimpse.1.3.0/tools/install.ps1
 delete mode 100644 Rewind/Packages/Glimpse.1.3.0/tools/uninstall.ps1
 delete mode 100644 Rewind/Packages/Glimpse.AspNet.1.2.1/Glimpse.AspNet.1.2.1.nupkg
 delete mode 100644 Rewind/Packages/Glimpse.AspNet.1.2.1/Glimpse.AspNet.1.2.1.nuspec
 delete mode 100644 Rewind/Packages/Glimpse.AspNet.1.2.1/content/GlimpseSecurityPolicy.cs.pp
 delete mode 100644 Rewind/Packages/Glimpse.AspNet.1.2.1/content/web.config.transform
 delete mode 100644 Rewind/Packages/Glimpse.AspNet.1.2.1/lib/net35/Glimpse.AspNet.dll
 delete mode 100644 Rewind/Packages/Glimpse.AspNet.1.2.1/lib/net40/Glimpse.AspNet.dll
 delete mode 100644 Rewind/Packages/Glimpse.AspNet.1.2.1/lib/net45/Glimpse.AspNet.dll
 delete mode 100644 Rewind/Packages/Glimpse.AspNet.1.2.1/tools/install.ps1
 delete mode 100644 Rewind/Packages/Glimpse.AspNet.1.2.1/tools/uninstall.ps1
 delete mode 100644 Rewind/Packages/Glimpse.Mvc4.1.2.1/Glimpse.Mvc4.1.2.1.nupkg
 delete mode 100644 Rewind/Packages/Glimpse.Mvc4.1.2.1/Glimpse.Mvc4.1.2.1.nuspec
 delete mode 100644 Rewind/Packages/Glimpse.Mvc4.1.2.1/lib/net40/Glimpse.Mvc4.dll
 delete mode 100644 Rewind/Packages/Glimpse.Mvc4.1.2.1/tools/install.ps1
 delete mode 100644 Rewind/Packages/Glimpse.Mvc4.1.2.1/tools/uninstall.ps1
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.12/tools/mspec-clr4.exe.config
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.12/tools/mspec-x86-clr4.exe.config
 delete mode 100644 Rewind/Packages/Microsoft.jQuery.Unobtrusive.Ajax.2.0.20710.0/Content/Scripts/jquery.unobtrusive-ajax.js
 delete mode 100644 Rewind/Packages/Microsoft.jQuery.Unobtrusive.Ajax.2.0.20710.0/Content/Scripts/jquery.unobtrusive-ajax.min.js
 delete mode 100644 Rewind/Packages/Microsoft.jQuery.Unobtrusive.Ajax.2.0.30116.0/Content/Scripts/jquery.unobtrusive-ajax.js
 delete mode 100644 Rewind/Packages/Microsoft.jQuery.Unobtrusive.Ajax.2.0.30116.0/Content/Scripts/jquery.unobtrusive-ajax.min.js
 delete mode 100644 Rewind/Packages/Microsoft.jQuery.Unobtrusive.Validation.2.0.20710.0/Content/Scripts/jquery.validate.unobtrusive.js
 delete mode 100644 Rewind/Packages/Microsoft.jQuery.Unobtrusive.Validation.2.0.20710.0/Content/Scripts/jquery.validate.unobtrusive.min.js
 delete mode 100644 Rewind/Packages/Microsoft.jQuery.Unobtrusive.Validation.2.0.30116.0/Content/Scripts/jquery.validate.unobtrusive.js
 delete mode 100644 Rewind/Packages/Microsoft.jQuery.Unobtrusive.Validation.2.0.30116.0/Content/Scripts/jquery.validate.unobtrusive.min.js
 delete mode 100644 Rewind/Packages/Modernizr.2.6.2/Modernizr.2.6.2.nupkg
 delete mode 100644 Rewind/Packages/Modernizr.2.6.2/Tools/common.ps1
 delete mode 100644 Rewind/Packages/Modernizr.2.6.2/Tools/install.ps1
 delete mode 100644 Rewind/Packages/Modernizr.2.6.2/Tools/uninstall.ps1
 delete mode 100644 Rewind/Packages/Moment.js.2.0.0/Content/Scripts/moment.js
 delete mode 100644 Rewind/Packages/Moment.js.2.0.0/Content/Scripts/moment.min.js
 delete mode 100644 Rewind/Packages/MomentDatepicker.1.1.0/content/Content/moment-datepicker/datepicker.css
 delete mode 100644 Rewind/Packages/MomentDatepicker.1.1.0/content/Scripts/moment-datepicker-ko.js
 delete mode 100644 Rewind/Packages/MomentDatepicker.1.1.0/content/Scripts/moment-datepicker.js
 delete mode 100644 Rewind/Packages/MomentDatepicker.1.1.0/content/Scripts/moment-datepicker.min.js
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/NLog.2.0.0.2000.nupkg
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/net20/NLog.dll
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/net20/NLog.xml
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/net35/NLog.dll
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/net35/NLog.xml
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/net40/NLog.dll
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/net40/NLog.xml
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/sl2/NLog.dll
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/sl2/NLog.xml
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/sl3-wp/NLog.dll
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/sl3-wp/NLog.xml
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/sl3/NLog.dll
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/sl3/NLog.xml
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/sl4-windowsphone71/NLog.dll
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/sl4-windowsphone71/NLog.xml
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/sl4/NLog.dll
 delete mode 100644 Rewind/Packages/NLog.2.0.0.2000/lib/sl4/NLog.xml
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/Newtonsoft.Json.4.0.5.nupkg
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/net20/Newtonsoft.Json.dll
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/net20/Newtonsoft.Json.pdb
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/net20/Newtonsoft.Json.xml
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/net35/Newtonsoft.Json.dll
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/net35/Newtonsoft.Json.pdb
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/net35/Newtonsoft.Json.xml
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/net40/Newtonsoft.Json.dll
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/net40/Newtonsoft.Json.pdb
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/net40/Newtonsoft.Json.xml
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/sl3-wp/Newtonsoft.Json.dll
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/sl3-wp/Newtonsoft.Json.pdb
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/sl3-wp/Newtonsoft.Json.xml
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/sl4-windowsphone71/Newtonsoft.Json.dll
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/sl4-windowsphone71/Newtonsoft.Json.pdb
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/sl4-windowsphone71/Newtonsoft.Json.xml
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/sl4/Newtonsoft.Json.dll
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/sl4/Newtonsoft.Json.pdb
 delete mode 100644 Rewind/Packages/Newtonsoft.Json.4.0.5/lib/sl4/Newtonsoft.Json.xml
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Server.exe.config
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2330/tools/Raven.Server.exe.config
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2330/tools/local.config
 delete mode 100644 Rewind/Packages/Sammy.js.0.7.4/Sammy.js.0.7.4.nupkg
 delete mode 100644 Rewind/Packages/Sammy.js.0.7.4/Sammy.js.0.7.4.nuspec
 delete mode 100644 Rewind/Packages/Sammy.js.0.7.4/Tools/common.ps1
 delete mode 100644 Rewind/Packages/Sammy.js.0.7.4/Tools/install.ps1
 delete mode 100644 Rewind/Packages/Sammy.js.0.7.4/Tools/uninstall.ps1
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/LICENSE.txt
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/SpecFlow.1.8.1.nupkg
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/changelog.txt
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/content/App.config.transform
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/lib/net35/TechTalk.SpecFlow.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/lib/sl3/TechTalk.SpecFlow.Silverlight3.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/lib/sl4-wp/TechTalk.SpecFlow.WindowsPhone7.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/Gherkin.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/IKVM.OpenJDK.Core.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/IKVM.OpenJDK.Security.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/IKVM.OpenJDK.Text.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/IKVM.OpenJDK.Util.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/IKVM.Runtime.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/TechTalk.SpecFlow.Generator.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/TechTalk.SpecFlow.Parser.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/TechTalk.SpecFlow.Reporting.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/TechTalk.SpecFlow.Utils.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/TechTalk.SpecFlow.dll
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/TechTalk.SpecFlow.targets
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/TechTalk.SpecFlow.tasks
 delete mode 100644 Rewind/Packages/SpecFlow.1.8.1/tools/specflow.exe
 delete mode 100644 Rewind/Packages/TinyIoC.1.1.1/Content/TinyIoC.cs
 delete mode 100644 Rewind/Packages/TinyIoC.1.1.1/TinyIoC.1.1.1.nupkg
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.2.3.1/Twitter.Bootstrap.2.3.1.nupkg
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.2.3.1/Twitter.Bootstrap.2.3.1.nuspec
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.2.3.1/content/Content/bootstrap-responsive.css
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.2.3.1/content/Content/bootstrap-responsive.min.css
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.2.3.1/content/Content/bootstrap.css
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.2.3.1/content/Content/bootstrap.min.css
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.2.3.1/content/Content/images/glyphicons-halflings-white.png
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.2.3.1/content/Content/images/glyphicons-halflings.png
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.MVC.1.1.6/Twitter.Bootstrap.MVC.1.1.6.nupkg
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.MVC.1.1.6/Twitter.Bootstrap.MVC.1.1.6.nuspec
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.MVC.1.1.6/content/net40/App_Start/BootstrapBundleConfig.cs.pp
 delete mode 100644 Rewind/Packages/WebActivator.1.5.3/WebActivator.1.5.3.nupkg
 delete mode 100644 Rewind/Packages/WebActivator.1.5.3/WebActivator.1.5.3.nuspec
 delete mode 100644 Rewind/Packages/WebActivator.1.5.3/lib/net40/WebActivator.dll
 delete mode 100644 Rewind/Packages/WebActivatorEx.2.0.1/WebActivatorEx.2.0.1.nupkg
 delete mode 100644 Rewind/Packages/WebActivatorEx.2.0.1/WebActivatorEx.2.0.1.nuspec
 delete mode 100644 Rewind/Packages/WebActivatorEx.2.0.1/lib/net40/WebActivatorEx.dll
 delete mode 100644 Rewind/Packages/WebGrease.1.1.0/WebGrease.1.1.0.nupkg
 delete mode 100644 Rewind/Packages/WebGrease.1.1.0/lib/Antlr3.Runtime.dll
 delete mode 100644 Rewind/Packages/WebGrease.1.1.0/lib/WebGrease.dll
 delete mode 100644 Rewind/Packages/WebGrease.1.1.0/tools/WG.exe
 delete mode 100644 Rewind/Packages/WebGrease.1.3.0/WebGrease.1.3.0.nupkg
 delete mode 100644 Rewind/Packages/WebGrease.1.3.0/lib/Antlr3.Runtime.dll
 delete mode 100644 Rewind/Packages/WebGrease.1.3.0/lib/WebGrease.dll
 delete mode 100644 Rewind/Packages/WebGrease.1.3.0/tools/WG.exe
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-bootstrap-prettify.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-bootstrap-prettify.min.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-bootstrap.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-bootstrap.min.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-cookies.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-cookies.min.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-loader.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-loader.min.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-mocks.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-resource.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-resource.min.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-sanitize.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-sanitize.min.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular-scenario.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/angular.min.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_am-et.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_am.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ar-eg.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ar.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_bg-bg.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_bg.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_bn-bd.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_bn.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ca-es.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ca.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_cs-cz.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_cs.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_da-dk.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_da.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_de-at.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_de-be.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_de-ch.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_de-de.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_de-lu.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_de.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_el-gr.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_el-polyton.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_el.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-as.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-au.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-dsrt-us.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-dsrt.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-gb.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-gu.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-ie.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-in.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-iso.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-mh.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-mp.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-sg.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-um.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-us.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-vi.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-za.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en-zz.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_en.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_es-es.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_es.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_et-ee.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_et.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_eu-es.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_eu.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fa-ir.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fa.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fi-fi.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fi.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fil-ph.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fil.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fr-bl.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fr-ca.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fr-fr.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fr-gp.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fr-mc.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fr-mf.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fr-mq.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fr-re.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_fr.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_gl-es.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_gl.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_gsw-ch.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_gsw.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_gu-in.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_gu.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_he-il.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_he.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_hi-in.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_hi.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_hr-hr.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_hr.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_hu-hu.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_hu.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_id-id.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_id.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_in.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_is-is.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_is.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_it-it.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_it.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_iw.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ja-jp.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ja.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_kn-in.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_kn.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ko-kr.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ko.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ln-cd.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ln.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_lt-lt.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_lt.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_lv-lv.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_lv.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ml-in.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ml.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_mo.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_mr-in.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_mr.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ms-my.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ms.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_mt-mt.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_mt.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_nl-nl.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_nl.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_no.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_or-in.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_or.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_pl-pl.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_pl.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_pt-br.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_pt-pt.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_pt.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ro-ro.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ro.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ru-ru.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ru.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sk-sk.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sk.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sl-si.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sl.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sq-al.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sq.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sr-cyrl-rs.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sr-latn-rs.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sr-rs.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sr.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sv-se.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sv.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sw-tz.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_sw.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ta-in.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ta.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_te-in.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_te.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_th-th.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_th.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_tl-ph.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_tl.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_tr-tr.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_tr.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_uk-ua.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_uk.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ur-pk.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_ur.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_vi-vn.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_vi.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_zh-cn.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_zh-hans-cn.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_zh-hans.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_zh-hk.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_zh-tw.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/i18n/angular-locale_zh.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/jstd-scenario-adapter-config.js
 delete mode 100644 Rewind/Packages/angularjs.1.0.3/content/Scripts/jstd-scenario-adapter.js
 delete mode 100644 Rewind/Packages/jQuery.1.9.0/Content/Scripts/jquery-1.9.0-vsdoc.js
 delete mode 100644 Rewind/Packages/jQuery.1.9.0/Content/Scripts/jquery-1.9.0.js
 delete mode 100644 Rewind/Packages/jQuery.1.9.0/Content/Scripts/jquery-1.9.0.min.js
 delete mode 100644 Rewind/Packages/jQuery.1.9.0/Tools/jquery-1.9.0.intellisense.js
 delete mode 100644 Rewind/Packages/jQuery.1.9.1/Content/Scripts/jquery-1.9.1-vsdoc.js
 delete mode 100644 Rewind/Packages/jQuery.1.9.1/Content/Scripts/jquery-1.9.1.js
 delete mode 100644 Rewind/Packages/jQuery.1.9.1/Content/Scripts/jquery-1.9.1.min.js
 delete mode 100644 Rewind/Packages/jQuery.1.9.1/Tools/jquery-1.9.1.intellisense.js
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-bg_flat_0_aaaaaa_40x100.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-bg_flat_75_ffffff_40x100.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-bg_glass_55_fbf9ee_1x400.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-bg_glass_65_ffffff_1x400.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-bg_glass_75_dadada_1x400.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-bg_glass_75_e6e6e6_1x400.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-bg_glass_95_fef1ec_1x400.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-bg_highlight-soft_75_cccccc_1x100.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-icons_222222_256x240.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-icons_2e83ff_256x240.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-icons_454545_256x240.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-icons_888888_256x240.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/images/ui-icons_cd0a0a_256x240.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery-ui.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.accordion.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.all.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.autocomplete.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.base.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.button.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.core.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.datepicker.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.dialog.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.menu.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.progressbar.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.resizable.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.selectable.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.slider.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.spinner.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.tabs.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.theme.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/jquery.ui.tooltip.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-bg_flat_0_aaaaaa_40x100.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-bg_flat_75_ffffff_40x100.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-bg_glass_55_fbf9ee_1x400.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-bg_glass_65_ffffff_1x400.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-bg_glass_75_dadada_1x400.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-bg_glass_75_e6e6e6_1x400.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-bg_glass_95_fef1ec_1x400.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-bg_highlight-soft_75_cccccc_1x100.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-icons_222222_256x240.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-icons_2e83ff_256x240.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-icons_454545_256x240.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-icons_888888_256x240.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/images/ui-icons_cd0a0a_256x240.png
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery-ui.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.accordion.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.autocomplete.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.button.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.core.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.datepicker.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.dialog.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.menu.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.progressbar.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.resizable.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.selectable.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.slider.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.spinner.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.tabs.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.theme.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Content/themes/base/minified/jquery.ui.tooltip.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Scripts/jquery-ui-1.10.0.js
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.0/Content/Scripts/jquery-ui-1.10.0.min.js
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery-ui.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.accordion.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.all.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.autocomplete.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.base.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.button.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.core.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.datepicker.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.dialog.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.menu.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.progressbar.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.resizable.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.selectable.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.slider.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.spinner.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.tabs.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.theme.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/jquery.ui.tooltip.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery-ui.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.accordion.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.autocomplete.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.button.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.core.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.datepicker.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.dialog.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.menu.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.progressbar.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.resizable.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.selectable.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.slider.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.spinner.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.tabs.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.theme.min.css
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Content/themes/base/minified/jquery.ui.tooltip.min.css
 delete mode 100644 Rewind/Packages/jQuery.Validation.1.10.0/Content/Scripts/jquery.validate-vsdoc.js
 delete mode 100644 Rewind/Packages/jQuery.Validation.1.10.0/Content/Scripts/jquery.validate.js
 delete mode 100644 Rewind/Packages/jQuery.Validation.1.10.0/Content/Scripts/jquery.validate.min.js
 delete mode 100644 Rewind/Packages/jQuery.Validation.1.11.1/Content/Scripts/jquery.validate-vsdoc.js
 delete mode 100644 Rewind/Packages/jQuery.Validation.1.11.1/Content/Scripts/jquery.validate.js
 delete mode 100644 Rewind/Packages/jQuery.Validation.1.11.1/Content/Scripts/jquery.validate.min.js
 delete mode 100644 Rewind/Packages/json2.1.0.2/content/Scripts/json2.js
 delete mode 100644 Rewind/Packages/json2.1.0.2/content/Scripts/json2.min.js
 delete mode 100644 Rewind/Packages/knockoutjs.2.1.0/Content/Scripts/knockout-2.1.0.debug.js
 delete mode 100644 Rewind/Packages/knockoutjs.2.1.0/Content/Scripts/knockout-2.1.0.js
 delete mode 100644 Rewind/Packages/knockoutjs.2.2.1/Content/Scripts/knockout-2.2.1.debug.js
 delete mode 100644 Rewind/Packages/knockoutjs.2.2.1/Content/Scripts/knockout-2.2.1.js
 delete mode 100644 Rewind/Packages/repositories.config
 delete mode 100644 Rewind/Running Raven.txt
 delete mode 100644 Rewind/_ReSharper.paramore/XmlIndex/Microsoft.CSharp.4.0.0.0.Nullness.Generated.xml/10F25633.bin
 delete mode 100644 Rewind/_ReSharper.paramore/XmlIndex/Microsoft.CSharp.xml/4DDA1CCA.bin
 delete mode 100644 Rewind/paramore.acceptancetests/paramore.acceptancetests.py
 delete mode 100644 Rewind/paramore.acceptancetests/paramore.acceptancetests.pyproj
 delete mode 100644 Rewind/paramore.api/src/paramore.api/Web.Debug.config
 delete mode 100644 Rewind/paramore.api/src/paramore.api/Web.Release.config
 delete mode 100644 Rewind/paramore.api/src/paramore.api/Web.config
 delete mode 100644 Rewind/paramore.api/src/paramore.api/packages.config
 delete mode 100644 Rewind/paramore.api/src/paramore.api/paramore.api.csproj
 delete mode 100644 Rewind/paramore.api/src/paramore.api/paramore.api.csproj.user
 delete mode 100644 Rewind/paramore.configuration/paramore.configuration.csproj
 delete mode 100644 Rewind/paramore.configuration/paramore.configuration.csproj.user
 delete mode 100644 Rewind/paramore.domain/packages.config
 delete mode 100644 Rewind/paramore.domain/paramore.domain.csproj
 delete mode 100644 Rewind/paramore.infrastructure/packages.config
 delete mode 100644 Rewind/paramore.infrastructure/paramore.infrastructure.csproj
 delete mode 100644 Rewind/paramore.integrationtests/App.config
 delete mode 100644 Rewind/paramore.integrationtests/packages.config
 delete mode 100644 Rewind/paramore.integrationtests/paramore.integrationtests.csproj
 delete mode 100644 Rewind/paramore.services/packages.config
 delete mode 100644 Rewind/paramore.services/paramore.services.csproj
 delete mode 100644 Rewind/paramore.sln
 delete mode 100644 Rewind/paramore.tests/packages.config
 delete mode 100644 Rewind/paramore.tests/paramore.unittests.csproj
 delete mode 100644 Rewind/paramore.utility/paramore.utility.csproj
 delete mode 100644 Rewind/paramore.web/App/config.js
 delete mode 100644 Rewind/paramore.web/App/durandal/amd/almond-custom.js
 delete mode 100644 Rewind/paramore.web/App/durandal/amd/optimizer.exe
 delete mode 100644 Rewind/paramore.web/App/durandal/amd/r.js
 delete mode 100644 Rewind/paramore.web/App/durandal/amd/require.js
 delete mode 100644 Rewind/paramore.web/App/durandal/amd/text.js
 delete mode 100644 Rewind/paramore.web/App/durandal/app.js
 delete mode 100644 Rewind/paramore.web/App/durandal/composition.js
 delete mode 100644 Rewind/paramore.web/App/durandal/events.js
 delete mode 100644 Rewind/paramore.web/App/durandal/http.js
 delete mode 100644 Rewind/paramore.web/App/durandal/messageBox.html
 delete mode 100644 Rewind/paramore.web/App/durandal/messageBox.js
 delete mode 100644 Rewind/paramore.web/App/durandal/modalDialog.js
 delete mode 100644 Rewind/paramore.web/App/durandal/plugins/router.js
 delete mode 100644 Rewind/paramore.web/App/durandal/system.js
 delete mode 100644 Rewind/paramore.web/App/durandal/transitions/entrance.js
 delete mode 100644 Rewind/paramore.web/App/durandal/viewEngine.js
 delete mode 100644 Rewind/paramore.web/App/durandal/viewLocator.js
 delete mode 100644 Rewind/paramore.web/App/durandal/viewModel.js
 delete mode 100644 Rewind/paramore.web/App/durandal/viewModelBinder.js
 delete mode 100644 Rewind/paramore.web/App/durandal/widget.js
 delete mode 100644 Rewind/paramore.web/App/main-built.js
 delete mode 100644 Rewind/paramore.web/App/main.js
 delete mode 100644 Rewind/paramore.web/App/services/dataService.js
 delete mode 100644 Rewind/paramore.web/App/services/dataService.speakers.js
 delete mode 100644 Rewind/paramore.web/App/services/dataService.venues.js
 delete mode 100644 Rewind/paramore.web/App/services/mocks/mockSpeakers.js
 delete mode 100644 Rewind/paramore.web/App/services/mocks/mockVenues.js
 delete mode 100644 Rewind/paramore.web/App/viewmodels/addVenueModal.js
 delete mode 100644 Rewind/paramore.web/App/viewmodels/shell.js
 delete mode 100644 Rewind/paramore.web/App/viewmodels/speakers.js
 delete mode 100644 Rewind/paramore.web/App/viewmodels/venues.js
 delete mode 100644 Rewind/paramore.web/App/views/addVenueModal.html
 delete mode 100644 Rewind/paramore.web/App/views/detail.html
 delete mode 100644 Rewind/paramore.web/App/views/nav.html
 delete mode 100644 Rewind/paramore.web/App/views/shell.html
 delete mode 100644 Rewind/paramore.web/App/views/speakers.html
 delete mode 100644 Rewind/paramore.web/App/views/venues.html
 delete mode 100644 Rewind/paramore.web/Content/app.css
 delete mode 100644 Rewind/paramore.web/Content/bootstrap-responsive.css
 delete mode 100644 Rewind/paramore.web/Content/bootstrap-responsive.min.css
 delete mode 100644 Rewind/paramore.web/Content/bootstrap.css
 delete mode 100644 Rewind/paramore.web/Content/bootstrap.min.css
 delete mode 100644 Rewind/paramore.web/Content/durandal.css
 delete mode 100644 Rewind/paramore.web/Content/font-awesome-ie7.min.css
 delete mode 100644 Rewind/paramore.web/Content/font-awesome.css
 delete mode 100644 Rewind/paramore.web/Content/font-awesome.min.css
 delete mode 100644 Rewind/paramore.web/Content/font/FontAwesome.otf
 delete mode 100644 Rewind/paramore.web/Content/font/fontawesome-webfont.eot
 delete mode 100644 Rewind/paramore.web/Content/font/fontawesome-webfont.svg
 delete mode 100644 Rewind/paramore.web/Content/font/fontawesome-webfont.ttf
 delete mode 100644 Rewind/paramore.web/Content/font/fontawesome-webfont.woff
 delete mode 100644 Rewind/paramore.web/Content/ie10mobile.css
 delete mode 100644 Rewind/paramore.web/Content/main.css
 delete mode 100644 Rewind/paramore.web/Content/moment-datepicker/datepicker.css
 delete mode 100644 Rewind/paramore.web/Content/normalize.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery-ui.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.accordion.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.all.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.autocomplete.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.base.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.button.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.core.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.datepicker.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.dialog.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.menu.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.progressbar.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.resizable.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.selectable.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.slider.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.spinner.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.tabs.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.theme.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/jquery.ui.tooltip.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery-ui.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.accordion.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.autocomplete.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.button.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.core.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.datepicker.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.dialog.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.menu.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.progressbar.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.resizable.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.selectable.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.slider.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.spinner.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.tabs.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.theme.min.css
 delete mode 100644 Rewind/paramore.web/Content/themes/base/minified/jquery.ui.tooltip.min.css
 delete mode 100644 Rewind/paramore.web/Global.asax
 delete mode 100644 Rewind/paramore.web/Scripts/amplify-vsdoc.js
 delete mode 100644 Rewind/paramore.web/Scripts/amplify.js
 delete mode 100644 Rewind/paramore.web/Scripts/amplify.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/jquery-1.9.1.intellisense.js
 delete mode 100644 Rewind/paramore.web/Scripts/jquery-1.9.1.js
 delete mode 100644 Rewind/paramore.web/Scripts/jquery-1.9.1.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/json2.js
 delete mode 100644 Rewind/paramore.web/Scripts/json2.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/knockout-2.2.1.debug.js
 delete mode 100644 Rewind/paramore.web/Scripts/knockout-2.2.1.js
 delete mode 100644 Rewind/paramore.web/Scripts/moment-datepicker-ko.js
 delete mode 100644 Rewind/paramore.web/Scripts/moment-datepicker.js
 delete mode 100644 Rewind/paramore.web/Scripts/moment-datepicker.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/moment.js
 delete mode 100644 Rewind/paramore.web/Scripts/moment.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/sammy-0.7.4.js
 delete mode 100644 Rewind/paramore.web/Scripts/sammy-0.7.4.min.js
 delete mode 100644 Rewind/paramore.web/Views/Durandal/Index.cshtml
 delete mode 100644 Rewind/paramore.web/Views/Durandal/_splash.cshtml
 delete mode 100644 Rewind/paramore.web/Views/Web.config
 delete mode 100644 Rewind/paramore.web/Web.Debug.config
 delete mode 100644 Rewind/paramore.web/Web.Release.config
 delete mode 100644 Rewind/paramore.web/Web.config
 delete mode 100644 Rewind/paramore.web/packages.config
 delete mode 100644 Rewind/paramore.web/paramore.web.csproj
 delete mode 100644 Rewind/paramore.web/paramore.web.csproj.user
 delete mode 100644 Rewind/theRules.txt

[33mcommit bf05468aa240fa6e613f2e6cf189c573cf5fe054[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Dec 3 17:31:52 2013 +0000

    Delete README.MD

 delete mode 100644 README.MD

[33mcommit 4f9bbfcb73bf16dd0d4508ae9af9a22008c0064c[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Nov 22 15:53:47 2013 +0000

    moving example to OR; adding python based acceptance tests

 delete mode 100644 Brighter/Examples/tasklist/Adapters/API/Handlers/TaskHandler.cs
 delete mode 100644 Brighter/Examples/tasklist/Adapters/API/Resources/TaskResource.cs

[33mcommit 788ce9d1d4e77f2b7bdfac9950ab98be64287857[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Nov 5 18:30:41 2013 +0000

    New vesion of project

 delete mode 100644 Brighter/Examples/tasklist.web/Modules/TaskModule.cs
 delete mode 100644 Brighter/Examples/tasklist.web/Web.config

[33mcommit 037c20af9b6e16a2d1c66a2a4e17e0de337c2420[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Oct 31 19:10:28 2013 +0000

    moving brighter example to OR

 delete mode 100644 Brighter/Examples/tasklist.web/Tests/tasklistfixture_acceptancetests.cs
 delete mode 100644 Brighter/Examples/tasklist.web/Views/AddTask.sshtml
 delete mode 100644 Brighter/Examples/tasklist.web/Views/EditTask.sshtml
 delete mode 100644 Brighter/Examples/tasklist.web/Views/Index.sshtml
 delete mode 100644 Brighter/Examples/tasklist.web/Views/TaskDetail.sshtml
 delete mode 100644 Brighter/Examples/tasklist.web/content/jquery-1.7.1.min.js
 delete mode 100644 Brighter/Examples/tasklist.web/content/jquery-ui-1.8.18.custom.css
 delete mode 100644 Brighter/Examples/tasklist.web/content/jquery-ui-1.8.18.custom.min.js
 delete mode 100644 Brighter/Examples/tasklist.web/content/main.css

[33mcommit a698f79818e8a665f048b578b166cfed7ff522ad[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Oct 22 11:14:44 2013 +0100

    interim to move some files around

 delete mode 100644 Rewind/paramore.services/Handlers/Speaker/AddSpeakerCommandHandler.cs

[33mcommit 1308d880bc722e7159e3bc36693d4c08a9bec65c[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sun May 26 21:56:37 2013 +0100

    Adding speaker page, deleting tutorial pages, end-to-end on speaker add
    (no update - excercise for tutorial)

 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/local.config
 delete mode 100644 Rewind/paramore.tests/domain/Venues/VenueParsingTests.cs
 delete mode 100644 Rewind/paramore.web/App/viewmodels/flickr.js
 delete mode 100644 Rewind/paramore.web/App/viewmodels/welcome.js
 delete mode 100644 Rewind/paramore.web/App/views/flickr.html
 delete mode 100644 Rewind/paramore.web/App/views/welcome.html

[33mcommit a4ce4f5988cb1c8d2c5f541cf6fb29a83fb61380[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri May 10 00:45:38 2013 +0100

    Trying to get amplify to work

 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/local.config

[33mcommit 1d29f355fe73e0884174cfcdf8e8fd69b60d8651[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri May 3 00:12:12 2013 +0100

    Get Durandal plugged in with venues

 delete mode 100644 Rewind/paramore.web/App/logger.js

[33mcommit df18c795af1fe5b6b940ed4289f4c00f6528e16e[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu May 2 09:15:08 2013 +0100

    :Go to a venueList

 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/local.config

[33mcommit c1c074a5ae14e793a4f2bbfd184bd694ec9b8571[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu May 2 00:37:53 2013 +0100

    Getting durandal running

 delete mode 100644 Rewind/.nuget/packages.config
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/ASL - Apache Software Foundation License.txt
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/BreakingChanges.txt
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/Castle.Core.3.1.0.nupkg
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/Castle.Core.3.1.0.nuspec
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/Changes.txt
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/Committers.txt
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/License.txt
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/lib/net35/Castle.Core.dll
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/lib/net35/Castle.Core.xml
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/lib/net40-client/Castle.Core.dll
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/lib/net40-client/Castle.Core.xml
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/lib/sl4/Castle.Core.dll
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/lib/sl4/Castle.Core.xml
 delete mode 100644 Rewind/Packages/Castle.Core.3.1.0/readme.txt
 delete mode 100644 Rewind/Packages/Machine.Fakes.1.1.0/Machine.Fakes.1.1.0.nupkg
 delete mode 100644 Rewind/Packages/Machine.Fakes.1.1.0/Machine.Fakes.1.1.0.nuspec
 delete mode 100644 Rewind/Packages/Machine.Fakes.1.1.0/lib/net40/Machine.Fakes.dll
 delete mode 100644 Rewind/Packages/Machine.Fakes.1.1.0/lib/net40/Machine.Fakes.xml
 delete mode 100644 Rewind/Packages/Machine.Fakes.FakeItEasy.1.1.0/Machine.Fakes.FakeItEasy.1.1.0.nupkg
 delete mode 100644 Rewind/Packages/Machine.Fakes.FakeItEasy.1.1.0/Machine.Fakes.FakeItEasy.1.1.0.nuspec
 delete mode 100644 Rewind/Packages/Machine.Fakes.FakeItEasy.1.1.0/lib/net40/Machine.Fakes.Adapters.FakeItEasy.dll
 delete mode 100644 Rewind/Packages/Machine.Fakes.FakeItEasy.1.1.0/lib/net40/Machine.Fakes.Adapters.FakeItEasy.xml
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/Machine.Specifications.0.5.11.nupkg
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/Machine.Specifications.0.5.11.nuspec
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/lib/net20/Machine.Specifications.TDNetRunner.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/lib/net20/Machine.Specifications.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/lib/net20/Machine.Specifications.dll.tdnet
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/lib/net40/Machine.Specifications.Clr4.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/lib/net40/Machine.Specifications.TDNetRunner.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/lib/net40/Machine.Specifications.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/lib/net40/Machine.Specifications.dll.tdnet
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/CommandLine.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/CommandLine.xml
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/InstallDotCoverRunner.2.0.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/InstallDotCoverRunner.2.1.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/InstallDotCoverRunner.2.2.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/InstallResharperRunner.6.1.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/InstallResharperRunner.7.0.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/InstallResharperRunner.7.1.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/InstallTDNetRunner.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/InstallTDNetRunnerSilent.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/JetBrains.dotCover.Resources.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/License.txt
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.Clr4.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.GallioAdapter.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.GallioAdapter.plugin
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.ReSharperRunner.6.1.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.ReSharperRunner.7.0.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.ReSharperRunner.7.1.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.Reporting.Templates.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.Reporting.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.SeleniumSupport.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.TDNetRunner.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.WatinSupport.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.WebDriverSupport.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.dll.tdnet
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.dotCoverRunner.2.0.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.dotCoverRunner.2.1.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Machine.Specifications.dotCoverRunner.2.2.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/Spark.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/TestDriven.Framework.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/install.ps1
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/mspec-clr4.exe
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/mspec-x86-clr4.exe
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/mspec-x86.exe
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.11/tools/mspec.exe
 delete mode 100644 Rewind/Packages/Modernizr.2.6.2/Content/Scripts/modernizr-2.6.2.js
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/Logo.ico
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/NUnit.2.5.10.11092.nupkg
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/NUnitFitTests.html
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/fit-license.txt
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/lib/nunit.framework.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/lib/nunit.framework.xml
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/lib/nunit.mocks.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/lib/pnunit.framework.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/license.txt
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/NUnitTests.VisualState.xml
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/NUnitTests.config
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/NUnitTests.nunit
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/TestResult.xml
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/agent.conf
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/agent.log.conf
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/launcher.log.conf
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/Failure.png
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/Ignored.png
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/Inconclusive.png
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/Skipped.png
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/Success.png
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/fit.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/log4net.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/nunit-console-runner.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/nunit-gui-runner.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/nunit.core.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/nunit.core.interfaces.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/nunit.fixtures.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/nunit.uiexception.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/nunit.uikit.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/lib/nunit.util.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit-agent-x86.exe
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit-agent-x86.exe.config
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit-agent.exe
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit-agent.exe.config
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit-console-x86.exe
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit-console-x86.exe.config
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit-console.exe
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit-console.exe.config
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit-x86.exe
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit-x86.exe.config
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit.exe
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit.exe.config
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/nunit.framework.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/pnunit-agent.exe
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/pnunit-agent.exe.config
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/pnunit-launcher.exe
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/pnunit-launcher.exe.config
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/pnunit.framework.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/pnunit.tests.dll
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/runFile.exe
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/runFile.exe.config
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/runpnunit.bat
 delete mode 100644 Rewind/Packages/NUnit.2.5.10.11092/tools/test.conf
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/RavenDB.Client.2.0.2230.nupkg
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/net40/Raven.Abstractions.dll
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/net40/Raven.Abstractions.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/net40/Raven.Abstractions.xml
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/net40/Raven.Client.Lightweight.dll
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/net40/Raven.Client.Lightweight.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/net40/Raven.Client.Lightweight.xml
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/sl40/AsyncCtpLibrary_Silverlight.dll
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/sl40/AsyncCtpLibrary_Silverlight.xml
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/sl40/Raven.Client.Silverlight-4.dll
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/sl40/Raven.Client.Silverlight-4.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/sl50/AsyncCtpLibrary_Silverlight5.dll
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/sl50/AsyncCtpLibrary_Silverlight5.xml
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/sl50/Raven.Client.Silverlight.dll
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/sl50/Raven.Client.Silverlight.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Client.2.0.2230/lib/sl50/Raven.Client.Silverlight.xml
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/RavenDB.Server.2.0.2230.nupkg
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Esent.Interop.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Esent.Interop.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Esent.Interop.xml
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/GeoAPI.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/ICSharpCode.NRefactory.CSharp.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/ICSharpCode.NRefactory.CSharp.xml
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/ICSharpCode.NRefactory.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/ICSharpCode.NRefactory.xml
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Jint.Raven.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Jint.Raven.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Lucene.Net.Contrib.Spatial.NTS.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Lucene.Net.Contrib.Spatial.NTS.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Lucene.Net.Contrib.Spatial.NTS.xml
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Lucene.Net.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Lucene.Net.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Lucene.Net.xml
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Mono.Cecil.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/NLog.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/NLog.xml
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/NetTopologySuite.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/PowerCollections.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Raven.Abstractions.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Raven.Abstractions.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Raven.Abstractions.xml
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Raven.Database.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Raven.Database.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Raven.Server.exe
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Raven.Server.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Spatial4n.Core.NTS.dll
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Spatial4n.Core.NTS.pdb
 delete mode 100644 Rewind/Packages/RavenDB.Server.2.0.2230/tools/Spatial4n.Core.NTS.xml
 delete mode 100644 Rewind/Packages/Sammy.js.0.7.4/Content/Scripts/sammy-0.7.4.js
 delete mode 100644 Rewind/Packages/Sammy.js.0.7.4/Content/Scripts/sammy-0.7.4.min.js
 delete mode 100644 Rewind/paramore.web/html/Venues.html
 delete mode 100644 Rewind/paramore.web/js/venues.js

[33mcommit 636a3dc86b3ba31ef297685229c1e8f3bdc3ff92[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed May 1 17:27:22 2013 +0100

    Getting Durandal working

 delete mode 100644 Rewind/paramore.web/Controllers/HomeController.cs
 delete mode 100644 Rewind/paramore.web/GlimpseSecurityPolicy.cs
 delete mode 100644 Rewind/paramore.web/Views/Home/Index.cshtml

[33mcommit 108521b62fdf5436b7b8e85a58428dd7792c648b[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Apr 18 18:05:08 2013 +0100

    Plugging in the command processor; returning entity body on add; sending
    add command

 delete mode 100644 Rewind/paramore.api/src/paramore.api/Contributors/DepdencencyPipelineContributor.cs

[33mcommit d834a3b7704f4df28a3d7f1e8c3d624c73b6f15a[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sun Apr 14 16:04:02 2013 +0100

    New web project to clean up adding of libs etc.

 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.2.3.1/content/Scripts/bootstrap.js
 delete mode 100644 Rewind/Packages/Twitter.Bootstrap.2.3.1/content/Scripts/bootstrap.min.js
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Scripts/jquery-ui-1.10.1.js
 delete mode 100644 Rewind/Packages/jQuery.UI.Combined.1.10.1/Content/Scripts/jquery-ui-1.10.1.min.js
 delete mode 100644 Rewind/Packages/jQuery.Validation.1.11.0/Content/Scripts/jquery.validate.js
 delete mode 100644 Rewind/Packages/jQuery.Validation.1.11.0/Content/Scripts/jquery.validate.min.js
 delete mode 100644 Rewind/paramore.web/404.html
 delete mode 100644 Rewind/paramore.web/App_Start/BootstrapBundleConfig.cs
 delete mode 100644 Rewind/paramore.web/App_Start/BundleConfig.cs
 delete mode 100644 Rewind/paramore.web/Scripts/lib/bootstrap.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/bootstrap.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_am-et.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_am.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ar-eg.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ar.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_bg-bg.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_bg.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_bn-bd.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_bn.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ca-es.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ca.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_cs-cz.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_cs.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_da-dk.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_da.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_de-at.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_de-be.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_de-ch.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_de-de.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_de-lu.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_de.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_el-gr.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_el-polyton.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_el.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-as.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-au.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-dsrt-us.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-dsrt.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-gb.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-gu.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-ie.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-in.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-iso.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-mh.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-mp.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-sg.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-um.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-us.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-vi.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-za.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en-zz.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_en.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_es-es.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_es.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_et-ee.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_et.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_eu-es.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_eu.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fa-ir.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fa.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fi-fi.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fi.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fil-ph.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fil.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fr-bl.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fr-ca.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fr-fr.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fr-gp.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fr-mc.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fr-mf.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fr-mq.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fr-re.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_fr.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_gl-es.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_gl.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_gsw-ch.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_gsw.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_gu-in.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_gu.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_he-il.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_he.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_hi-in.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_hi.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_hr-hr.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_hr.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_hu-hu.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_hu.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_id-id.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_id.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_in.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_is-is.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_is.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_it-it.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_it.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_iw.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ja-jp.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ja.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_kn-in.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_kn.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ko-kr.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ko.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ln-cd.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ln.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_lt-lt.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_lt.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_lv-lv.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_lv.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ml-in.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ml.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_mo.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_mr-in.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_mr.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ms-my.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ms.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_mt-mt.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_mt.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_nl-nl.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_nl.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_no.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_or-in.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_or.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_pl-pl.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_pl.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_pt-br.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_pt-pt.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_pt.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ro-ro.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ro.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ru-ru.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ru.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sk-sk.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sk.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sl-si.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sl.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sq-al.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sq.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sr-cyrl-rs.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sr-latn-rs.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sr-rs.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sr.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sv-se.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sv.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sw-tz.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_sw.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ta-in.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ta.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_te-in.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_te.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_th-th.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_th.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_tl-ph.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_tl.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_tr-tr.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_tr.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_uk-ua.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_uk.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ur-pk.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_ur.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_vi-vn.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_vi.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_zh-cn.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_zh-hans-cn.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_zh-hans.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_zh-hk.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_zh-tw.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/i18n/angular-locale_zh.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-1.9.0-vsdoc.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-1.9.0.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-1.9.0.min.map
 delete mode 100644 Rewind/paramore.web/Scripts/lib/modernizr-2.6.2.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/sammy-0.7.4.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/sammy-0.7.4.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/venues.js
 delete mode 100644 Rewind/paramore.web/Views/Venues.html
 delete mode 100644 Rewind/paramore.web/favicon.ico
 delete mode 100644 Rewind/paramore.web/humans.txt
 delete mode 100644 Rewind/paramore.web/robots.txt

[33mcommit 0735e81cf0f2ec5dd3f26171662625ed7b6487cc[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Apr 13 23:39:09 2013 +0100

    Working on a first binding to the client

 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-1.9.1.intellisense.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-1.9.1.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-1.9.1.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-ui-1.10.0.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-ui-1.10.0.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-ui-1.10.1.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-ui-1.10.1.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery.unobtrusive-ajax.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery.unobtrusive-ajax.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery.validate-vsdoc.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery.validate.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery.validate.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery.validate.unobtrusive.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery.validate.unobtrusive.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jstd-scenario-adapter-config.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jstd-scenario-adapter.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/knockout-2.1.0.js
 delete mode 100644 Rewind/paramore.web/Scripts/mocks/venuedata.js

[33mcommit 1c86dfe75f4cf38d068909bf470cb6a17de59fb0[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Apr 13 20:10:01 2013 +0100

    trying to simplify js landscape initially

 delete mode 100644 Rewind/paramore.web/Content/main.css
 delete mode 100644 Rewind/paramore.web/Content/normalize.css
 delete mode 100644 Rewind/paramore.web/Scripts/_references.js
 delete mode 100644 Rewind/paramore.web/Scripts/jquery.validate-vsdoc.js
 delete mode 100644 Rewind/paramore.web/Scripts/jquery.validate.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/main.js
 delete mode 100644 Rewind/paramore.web/Scripts/plugins.js
 delete mode 100644 Rewind/paramore.web/index.html

[33mcommit 266e462460f4b483db8aadeb41254c8233898151[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Mar 22 17:34:43 2013 +0000

    Changing the JS libs

 delete mode 100644 Rewind/Packages/EntityFramework.5.0.0/tools/Redirect.VS11.config
 delete mode 100644 Rewind/Packages/EntityFramework.5.0.0/tools/Redirect.config
 delete mode 100644 Rewind/paramore.web/App_Start/AuthConfig.cs
 delete mode 100644 Rewind/paramore.web/App_Start/WebApiConfig.cs
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-bootstrap-prettify.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-bootstrap-prettify.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-bootstrap.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-bootstrap.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-cookies.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-cookies.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-loader.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-loader.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-mocks.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-resource.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-resource.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-sanitize.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-sanitize.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular-scenario.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/angular.min.js

[33mcommit 0163b82ad9c2ddfc192ca010fee4f377e448e9bc[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Mar 7 11:31:40 2013 +0000

    Links

 delete mode 100644 Rewind/paramore.api/src/paramore.api/Translators/Globals.cs

[33mcommit 2fef9106c9233ca1eb0322b73e5dac1b73d3bdfb[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Mar 7 10:05:53 2013 +0000

    Update packages

 delete mode 100644 Rewind/Packages/Castle.Core.2.5.2/Castle.Core.2.5.2.nupkg
 delete mode 100644 Rewind/Packages/Castle.Core.2.5.2/lib/NET35/Castle.Core.dll
 delete mode 100644 Rewind/Packages/Castle.Core.2.5.2/lib/NET40ClientProfile/Castle.Core.dll
 delete mode 100644 Rewind/Packages/Castle.Core.2.5.2/lib/SL3/Castle.Core.dll
 delete mode 100644 Rewind/Packages/Castle.Core.2.5.2/lib/SL3/Castle.Core.xml
 delete mode 100644 Rewind/Packages/Castle.Core.2.5.2/lib/SL4/Castle.Core.dll
 delete mode 100644 Rewind/Packages/Castle.Core.2.5.2/lib/releaseNotes.txt
 delete mode 100644 Rewind/Packages/FakeItEasy.1.7.4257.42/FakeItEasy.1.7.4257.42.nupkg
 delete mode 100644 Rewind/Packages/FakeItEasy.1.7.4257.42/lib/NET40/FakeItEasy.dll
 delete mode 100644 Rewind/Packages/FakeItEasy.1.7.4257.42/lib/NET40/FakeItEasy.xml
 delete mode 100644 Rewind/Packages/FakeItEasy.1.7.4257.42/lib/SL4/FakeItEasy-SL.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/Machine.Specifications.0.5.2.0.nupkg
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/lib/Machine.Specifications.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/InstallResharperRunner.4.1.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/InstallResharperRunner.4.5.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/InstallResharperRunner.5.0 - VS2008.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/InstallResharperRunner.5.0 - VS2010.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/InstallResharperRunner.5.1 - VS2008.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/InstallResharperRunner.5.1 - VS2010.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/InstallResharperRunner.6.0 - VS2008.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/InstallResharperRunner.6.0 - VS2010.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/InstallResharperRunner.6.1 - VS2008.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/InstallResharperRunner.6.1 - VS2010.bat
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/Machine.Specifications.GallioAdapter.3.1.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/Machine.Specifications.ReSharperRunner.4.1.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/Machine.Specifications.ReSharperRunner.4.5.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/Machine.Specifications.ReSharperRunner.5.0.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/Machine.Specifications.ReSharperRunner.5.1.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/Machine.Specifications.ReSharperRunner.6.0.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/Machine.Specifications.ReSharperRunner.6.1.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/Machine.Specifications.Reporting.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/Machine.Specifications.dll
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/mspec-clr4.exe
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/mspec-x86-clr4.exe
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/mspec-x86.exe
 delete mode 100644 Rewind/Packages/Machine.Specifications.0.5.2.0/tools/mspec.exe

[33mcommit 1e9f2ff46ad3ec86213fae65c2b736a930d3c717[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Feb 26 17:18:57 2013 +0000

    Remove OpenWrap, depend directly on OpenRasta binaries

 delete mode 100644 Rewind/paramore.api/paramore.api.wrapdesc
 delete mode 100644 Rewind/paramore.api/wraps/.gitignore
 delete mode 100644 Rewind/paramore.api/wraps/SharpZipLib-0.86.0.wrap
 delete mode 100644 Rewind/paramore.api/wraps/openfilesystem-1.0.1.87877626.wrap
 delete mode 100644 Rewind/paramore.api/wraps/openwrap-1.0.2.86964541.wrap
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/bin-net35/OpenRasta.Client.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/bin-net35/OpenRasta.Client.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/bin-net35/OpenWrap.Testing.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/bin-net35/OpenWrap.Testing.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/bin-net35/OpenWrap.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/bin-net35/OpenWrap.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/bin-net35/_OpenRasta.Client.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/bin-net35/_OpenWrap.Testing.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/bin-net35/_OpenWrap.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.Build.Bootstrap.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.Build.Bootstrap.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.CSharp.targets
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/build/OpenWrap.tasks
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/commands/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/commands/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/openwrap.wrapdesc
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Tests.dll
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/OpenWrap.Tests.pdb
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/Repositories/feedodata.xml
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/tests/Repositories/feedodata2.xml
 delete mode 100644 Rewind/paramore.api/wraps/openwrap/version
 delete mode 100644 Rewind/paramore.api/wraps/tdnet-framework-2.0.0.48555719.wrap
 delete mode 100644 Rewind/wraps/SharpZipLib-0.86.0.wrap
 delete mode 100644 Rewind/wraps/_cache/SharpZipLib-0.86.0/SharpZipLib.wrapdesc
 delete mode 100644 Rewind/wraps/_cache/SharpZipLib-0.86.0/bin-11/ICSharpCode.SharpZipLib.dll
 delete mode 100644 Rewind/wraps/_cache/SharpZipLib-0.86.0/bin-net20/ICSharpCode.SharpZipLib.dll
 delete mode 100644 Rewind/wraps/_cache/SharpZipLib-0.86.0/bin-sl20/SharpZipLib.Silverlight3.dll
 delete mode 100644 Rewind/wraps/_cache/SharpZipLib-0.86.0/bin-sl20/SharpZipLib.Silverlight4.dll
 delete mode 100644 Rewind/wraps/_cache/openfilesystem-1.0.1.87877626/bin-net35/Mono.Posix.dll
 delete mode 100644 Rewind/wraps/_cache/openfilesystem-1.0.1.87877626/bin-net35/OpenFileSystem.dll
 delete mode 100644 Rewind/wraps/_cache/openfilesystem-1.0.1.87877626/bin-net35/OpenFileSystem.pdb
 delete mode 100644 Rewind/wraps/_cache/openfilesystem-1.0.1.87877626/openfilesystem.wrapdesc
 delete mode 100644 Rewind/wraps/_cache/openfilesystem-1.0.1.87877626/version
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/OpenRasta.Client.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/OpenRasta.Client.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/OpenWrap.Testing.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/OpenWrap.Testing.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/OpenWrap.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/OpenWrap.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Build.Bootstrap.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Build.Bootstrap.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.CSharp.targets
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.tasks
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/commands/OpenWrap.Commands.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/commands/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/openwrap.wrapdesc
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Commands.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Tests.dll
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Tests.pdb
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/Repositories/feedodata.xml
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/tests/Repositories/feedodata2.xml
 delete mode 100644 Rewind/wraps/_cache/openwrap-1.0.2.86964541/version
 delete mode 100644 Rewind/wraps/_cache/tdnet-framework-2.0.0.48555719/bin-net35/TestDriven.Framework.dll
 delete mode 100644 Rewind/wraps/_cache/tdnet-framework-2.0.0.48555719/tdnet-framework.wrapdesc
 delete mode 100644 Rewind/wraps/_cache/tdnet-framework-2.0.0.48555719/version
 delete mode 100644 Rewind/wraps/openfilesystem-1.0.1.87877626.wrap
 delete mode 100644 Rewind/wraps/openwrap-1.0.2.86964541.wrap
 delete mode 100644 Rewind/wraps/openwrap/bin-net35/OpenRasta.Client.dll
 delete mode 100644 Rewind/wraps/openwrap/bin-net35/OpenRasta.Client.pdb
 delete mode 100644 Rewind/wraps/openwrap/bin-net35/OpenWrap.Testing.dll
 delete mode 100644 Rewind/wraps/openwrap/bin-net35/OpenWrap.Testing.pdb
 delete mode 100644 Rewind/wraps/openwrap/bin-net35/OpenWrap.dll
 delete mode 100644 Rewind/wraps/openwrap/bin-net35/OpenWrap.pdb
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.Build.Bootstrap.dll
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.Build.Bootstrap.pdb
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.CSharp.targets
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/wraps/openwrap/build/OpenWrap.tasks
 delete mode 100644 Rewind/wraps/openwrap/commands/OpenWrap.Commands.dll
 delete mode 100644 Rewind/wraps/openwrap/commands/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/wraps/openwrap/openwrap.wrapdesc
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Commands.dll
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Tests.dll
 delete mode 100644 Rewind/wraps/openwrap/tests/OpenWrap.Tests.pdb
 delete mode 100644 Rewind/wraps/openwrap/tests/Repositories/feedodata.xml
 delete mode 100644 Rewind/wraps/openwrap/tests/Repositories/feedodata2.xml
 delete mode 100644 Rewind/wraps/openwrap/version
 delete mode 100644 Rewind/wraps/tdnet-framework-2.0.0.48555719.wrap

[33mcommit 507d9576c879f238ada7ba32b293e09ea23c63c7[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Feb 5 18:10:06 2013 +0000

    New version of Raven

 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/content/Web.config.transform
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net35/Raven.Abstractions-3.5.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net35/Raven.Abstractions-3.5.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net35/Raven.Client.Lightweight-3.5.XML
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net35/Raven.Client.Lightweight-3.5.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net35/Raven.Client.Lightweight-3.5.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net40/AsyncCtpLibrary.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net40/AsyncCtpLibrary.xml
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net40/Raven.Abstractions.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net40/Raven.Abstractions.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net40/Raven.Client.Debug.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net40/Raven.Client.Debug.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net40/Raven.Client.Lightweight.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net40/Raven.Client.Lightweight.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net40/Raven.Client.MvcIntegration.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/net40/Raven.Client.MvcIntegration.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/sl40/MissingBitFromSilverlight.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/sl40/MissingBitFromSilverlight.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/sl40/Raven.Client.Silverlight.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/lib/sl40/Raven.Client.Silverlight.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/BouncyCastle.Crypto.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/ICSharpCode.NRefactory.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Lucene.Net.Contrib.Spatial.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Lucene.Net.Contrib.Spatial.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Lucene.Net.Contrib.Spatial.xml
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Lucene.Net.Contrib.SpellChecker.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Lucene.Net.Contrib.SpellChecker.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Lucene.Net.Contrib.SpellChecker.xml
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Lucene.Net.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Lucene.Net.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/NLog.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/NLog.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Newtonsoft.Json.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Newtonsoft.Json.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Newtonsoft.Json.xml
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Abstractions.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Abstractions.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Database.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Database.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Munin.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Munin.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Storage.Esent.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Storage.Esent.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Storage.Managed.dll
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Storage.Managed.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/server/Raven.Studio.xap
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/tools/Raven.Backup.exe
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/tools/Raven.Backup.pdb
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/tools/Raven.Smuggler.exe
 delete mode 100644 Rewind/Packages/RavenDB.1.0.573/tools/Raven.Smuggler.pdb

[33mcommit ff646ac857f78913f067f87485fa7f81384dfe67[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Jan 29 18:22:38 2013 +0000

    Fixed defect failing to call GetAll()

 delete mode 100644 Rewind/paramore.api/src/paramore.api/Contributors/DependecyPipelineContributor.cs

[33mcommit 64613a2d2117d94eda2a0c3188596afbe14cf58c[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Jan 22 17:52:43 2013 +0000

    Adding angularjs into the project; update jquery and modernizer

 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-1.7.1-vsdoc.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-1.7.1.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-1.7.1.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-ui-1.8.20.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery-ui-1.8.20.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery.unobtrusive-ajax.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery.unobtrusive-ajax.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery.validate.unobtrusive.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/jquery.validate.unobtrusive.min.js
 delete mode 100644 Rewind/paramore.web/Scripts/lib/knockout-2.1.0.debug.js

[33mcommit 23f141a16bc234709c4f1a2c7ebd7a1edf995316[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Jan 22 11:42:37 2013 +0000

     A whole set of files went missing - adding in - might require reset to
     head

 delete mode 100644 Rewind/paramore.tests/paramore.tests.csproj

[33mcommit 0a4f99cb4a5aca5cf5fd9ec7fabbe11ef818198b[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sun Jan 13 17:10:12 2013 +0000

    Clean up module naming

 delete mode 100644 Rewind/paramore.domain/Documents/MeetingDocument.cs
 delete mode 100644 Rewind/paramore.domain/Documents/MeetingDocumentTickets.cs
 delete mode 100644 Rewind/paramore.domain/Documents/SpeakerDocument.cs
 delete mode 100644 Rewind/paramore.domain/Documents/VenueDocument.cs
 delete mode 100644 Rewind/paramore.domain/DomainServices/FiftyPercentOverbookingPolicy.cs
 delete mode 100644 Rewind/paramore.domain/DomainServices/IAmAnOverbookingPolicy.cs
 delete mode 100644 Rewind/paramore.domain/Entities/Meetings/Meeting.cs
 delete mode 100644 Rewind/paramore.domain/Entities/Meetings/Ticket.cs
 delete mode 100644 Rewind/paramore.domain/Entities/Meetings/Tickets.cs
 delete mode 100644 Rewind/paramore.domain/Entities/Speakers/Speaker.cs
 delete mode 100644 Rewind/paramore.domain/Factories/IIssueTickets.cs
 delete mode 100644 Rewind/paramore.domain/Factories/IScheduler.cs
 delete mode 100644 Rewind/paramore.domain/Factories/Scheduler.cs
 delete mode 100644 Rewind/paramore.domain/Factories/TicketIssuer.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/Address.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/AggregateRoot.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/Capacity.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/City.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/ContactName.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/EmailAddress.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/IAmAvalueType.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/MeetingDate.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/MeetingState.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/PhoneNumber.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/PostCode.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/SpeakerBio.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/SpeakerName.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/Street.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/VenueContact.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/VenueMap.cs
 delete mode 100644 Rewind/paramore.domain/ValueTypes/VenueName.cs

[33mcommit 3632bbb8c952cf8d9b64341721f297937142c706[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Jan 1 23:19:46 2013 +0000

    Adding tests for the thin read layer

 delete mode 100644 Rewind/paramore.domain/Venues/Venue.cs

[33mcommit 921d3b712eccfeae6ff5a85e408566d8533a85c3[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Mon Dec 31 17:44:40 2012 +0000

    Getting toward Python acceptance tests - adding an entry point

 delete mode 100644 Rewind/paramore.api/src/paramore.api/Views/EntryPointView.aspx
 delete mode 100644 Rewind/paramore.api/src/paramore.api/Views/EntryPointView.aspx.cs

[33mcommit 539fd3486a072d730316f491d468df2e6cb54cb8[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Dec 22 21:27:56 2012 +0000

    kill broken api project

 delete mode 100644 Rewind/paramore.api/paramore.api.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.api/src/paramore.api/Web.config
 delete mode 100644 Rewind/paramore.api/src/paramore.api/paramore.api.csproj
 delete mode 100644 Rewind/paramore.api/src/paramore.api/paramore.api.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/SharpZipLib-0.86.0.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/SharpZipLib.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/bin-11/ICSharpCode.SharpZipLib.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/bin-net20/ICSharpCode.SharpZipLib.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/bin-net20/_ICSharpCode.SharpZipLib.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/bin-sl20/SharpZipLib.Silverlight3.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/bin-sl20/SharpZipLib.Silverlight4.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/bin-net35/Mono.Posix.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/bin-net35/OpenFileSystem.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/bin-net35/OpenFileSystem.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/bin-net35/_Mono.Posix.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/bin-net35/_OpenFileSystem.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/openfilesystem.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/bin-net35/OpenFileSystem.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/bin-net35/_Mono.Posix.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/bin-net35/_OpenFileSystem.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/openfilesystem.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/bin-net35/OpenRasta.Codecs.WebForms.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/bin-net35/OpenRasta.Codecs.WebForms.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/openrasta-codecs-webforms.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/tests/OpenRasta.Codecs.WebForms.Tests.Unit.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/tests/OpenRasta.Codecs.WebForms.Tests.Unit.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/bin-net35/OpenRasta.Testing.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/bin-net35/OpenRasta.Testing.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/bin-net35/OpenRasta.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/bin-net35/OpenRasta.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/bin-net35/Resources/error-test.htm
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/openrasta-core.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-devtools-2.1.0.81133919/openrasta-devtools.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-devtools-2.1.0.81133919/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-hosting-aspnet-2.1.0.80252216/bin-net35/OpenRasta.Hosting.AspNet.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-hosting-aspnet-2.1.0.80252216/bin-net35/OpenRasta.Hosting.AspNet.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-hosting-aspnet-2.1.0.80252216/openrasta-hosting-aspnet.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-hosting-aspnet-2.1.0.80252216/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-templates-2.1.0.80255366/commands-net35/OpenRasta.Templates.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-templates-2.1.0.80255366/commands-net35/OpenRasta.Templates.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-templates-2.1.0.80255366/openrasta-templates.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-templates-2.1.0.80255366/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenRasta.Client.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenRasta.Client.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenWrap.Testing.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenWrap.Testing.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenWrap.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenWrap.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/_OpenRasta.Client.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/_OpenWrap.Testing.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/_OpenWrap.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Build.Bootstrap.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Build.Bootstrap.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.CSharp.targets
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.511.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.511.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.tasks
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/commands/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/commands/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/openwrap.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.511.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.511.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Tests.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Tests.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/Repositories/feedodata.xml
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/Repositories/feedodata2.xml
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/OpenRasta.Client.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/OpenWrap.Testing.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/OpenWrap.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/_OpenRasta.Client.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/_OpenWrap.Testing.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/_OpenWrap.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Build.Bootstrap.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Build.Bootstrap.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.CSharp.targets
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.tasks
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/commands/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/commands/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/openwrap.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Tests.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/OpenWrap.Tests.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/Repositories/feedodata.xml
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/Repositories/feedodata2.xml
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/tdnet-framework-2.0.0.48555719/bin-net35/TestDriven.Framework.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/tdnet-framework-2.0.0.48555719/bin-net35/_TestDriven.Framework.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/tdnet-framework-2.0.0.48555719/tdnet-framework.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/tdnet-framework-2.0.0.48555719/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openfilesystem-1.0.0.61263243.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openfilesystem-1.0.1.87877626.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openrasta-codecs-webforms-2.1.0.80254244.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openrasta-core-2.1.0.83282449.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openrasta-devtools-2.1.0.81133919.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openrasta-hosting-aspnet-2.1.0.80252216.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openrasta-templates-2.1.0.80255366.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap-1.0.0.63365789.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap-1.0.2.86964541.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/OpenRasta.Client.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/OpenWrap.Testing.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/OpenWrap.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/_OpenRasta.Client.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/_OpenWrap.Testing.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/_OpenWrap.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Build.Bootstrap.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Build.Bootstrap.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.CSharp.targets
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.tasks
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/commands/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/commands/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/openwrap.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Tests.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Tests.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/Repositories/feedodata.xml
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/Repositories/feedodata2.xml
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/tdnet-framework-2.0.0.48555719.wrap

[33mcommit e522420d88db4212b65c1af1891988e2ce0174ef[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Nov 29 09:56:08 2012 +0000

    Using this as a workspace to play with the Open Rasta tutorial; prior to
    changing tutorial, adding OpenRasta support

 delete mode 100644 Rewind/paramore.api/src/paramore.api/Scripts/jquery-1.4.1-vsdoc.js
 delete mode 100644 Rewind/paramore.api/src/paramore.api/Scripts/jquery-1.4.1.js
 delete mode 100644 Rewind/paramore.api/src/paramore.api/Scripts/jquery-1.4.1.min.js

[33mcommit 5c3fd755e3e47a3273e84da77afaa98b20f9c5ca[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Oct 26 16:01:20 2012 +0100

    Add openwrap and openrasta remove the specflow approach to testing

 delete mode 100644 Rewind/paramore.api/src/paramore.api/paramore.api.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/SharpZipLib.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/bin-11/ICSharpCode.SharpZipLib.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/bin-net20/ICSharpCode.SharpZipLib.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/bin-net20/_ICSharpCode.SharpZipLib.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/bin-sl20/SharpZipLib.Silverlight3.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/SharpZipLib-0.86.0/bin-sl20/SharpZipLib.Silverlight4.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/bin-net35/Mono.Posix.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/bin-net35/OpenFileSystem.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/bin-net35/OpenFileSystem.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/bin-net35/_Mono.Posix.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/bin-net35/_OpenFileSystem.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/openfilesystem.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.0.61263243/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/bin-net35/Mono.Posix.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/bin-net35/OpenFileSystem.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/bin-net35/OpenFileSystem.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/bin-net35/_Mono.Posix.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/bin-net35/_OpenFileSystem.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/openfilesystem.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openfilesystem-1.0.1.87877626/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/bin-net35/OpenRasta.Codecs.WebForms.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/bin-net35/OpenRasta.Codecs.WebForms.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/openrasta-codecs-webforms.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/tests/OpenRasta.Codecs.WebForms.Tests.Unit.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/tests/OpenRasta.Codecs.WebForms.Tests.Unit.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-codecs-webforms-2.1.0.80254244/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/bin-net35/OpenRasta.Testing.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/bin-net35/OpenRasta.Testing.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/bin-net35/OpenRasta.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/bin-net35/OpenRasta.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/bin-net35/Resources/error-test.htm
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/openrasta-core.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-core-2.1.0.83282449/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-devtools-2.1.0.81133919/openrasta-devtools.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-devtools-2.1.0.81133919/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-hosting-aspnet-2.1.0.80252216/bin-net35/OpenRasta.Hosting.AspNet.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-hosting-aspnet-2.1.0.80252216/bin-net35/OpenRasta.Hosting.AspNet.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-hosting-aspnet-2.1.0.80252216/openrasta-hosting-aspnet.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-hosting-aspnet-2.1.0.80252216/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-templates-2.1.0.80255366/commands-net35/OpenRasta.Templates.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-templates-2.1.0.80255366/commands-net35/OpenRasta.Templates.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-templates-2.1.0.80255366/openrasta-templates.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openrasta-templates-2.1.0.80255366/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenRasta.Client.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenRasta.Client.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenWrap.Testing.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenWrap.Testing.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenWrap.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/bin-net35/OpenWrap.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Build.Bootstrap.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Build.Bootstrap.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.511.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/build/OpenWrap.Resharper.511.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/commands/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/commands/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/openwrap.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.511.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Resharper.511.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Tests.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/tests/OpenWrap.Tests.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.0.63365789/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/_OpenRasta.Client.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/_OpenWrap.Testing.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/bin-net35/_OpenWrap.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.CSharp.targets
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/build/OpenWrap.tasks
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/Repositories/feedodata.xml
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/openwrap-1.0.2.86964541/tests/Repositories/feedodata2.xml
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/tdnet-framework-2.0.0.48555719/bin-net35/TestDriven.Framework.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/tdnet-framework-2.0.0.48555719/bin-net35/_TestDriven.Framework.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/tdnet-framework-2.0.0.48555719/tdnet-framework.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/_cache/tdnet-framework-2.0.0.48555719/version
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openfilesystem-1.0.0.61263243.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openrasta-codecs-webforms-2.1.0.80254244.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openrasta-core-2.1.0.83282449.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openrasta-devtools-2.1.0.81133919.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openrasta-hosting-aspnet-2.1.0.80252216.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openrasta-templates-2.1.0.80255366.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap-1.0.0.63365789.wrap
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/OpenRasta.Client.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/OpenRasta.Client.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/OpenWrap.Testing.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/OpenWrap.Testing.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/OpenWrap.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/OpenWrap.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/_OpenRasta.Client.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/_OpenWrap.Testing.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/bin-net35/_OpenWrap.dll.entrypoint
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Build.Bootstrap.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Build.Bootstrap.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.CSharp.targets
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/build/OpenWrap.tasks
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/commands/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/commands/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/openwrap.wrapdesc
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Build.Tasks.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Build.Tasks.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Commands.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Commands.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.450.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.450.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.500.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.500.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.510.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Resharper.510.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Tests.dll
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/OpenWrap.Tests.pdb
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/Repositories/feedodata.xml
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/tests/Repositories/feedodata2.xml
 delete mode 100644 Rewind/paramore.api/src/paramore.api/wraps/openwrap/version
 delete mode 100644 Rewind/paramore.features/AddAVenue.feature
 delete mode 100644 Rewind/paramore.features/AddAVenue.feature.cs
 delete mode 100644 Rewind/paramore.features/App.config
 delete mode 100644 Rewind/paramore.features/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.features/ScheduleAMeeting.feature
 delete mode 100644 Rewind/paramore.features/ScheduleAMeeting.feature.cs
 delete mode 100644 Rewind/paramore.features/Steps/AddAVenueSteps.cs
 delete mode 100644 Rewind/paramore.features/Steps/ScheduleAMeetingSteps.cs
 delete mode 100644 Rewind/paramore.features/Tools/FuzzyDateParser.cs
 delete mode 100644 Rewind/paramore.features/packages.config
 delete mode 100644 Rewind/paramore.features/paramore.features.csproj

[33mcommit a4b7b7eac1de9925ce0f39e3cc2c761dd423aae9[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Apr 17 19:51:19 2012 +0100

    Add more CRUD methods to Command Processor example; clean up handling
    Raven documents whilst preserving encapsulation

 delete mode 100644 Rewind/paramore.domain/Meetings/IAmAnOverbookingPolicy.cs
 delete mode 100644 Rewind/paramore.domain/Meetings/IIssueTickets.cs
 delete mode 100644 Rewind/paramore.domain/Meetings/IScheduler.cs
 delete mode 100644 Rewind/paramore.domain/Meetings/Meeting.cs
 delete mode 100644 Rewind/paramore.domain/Meetings/Ticket.cs
 delete mode 100644 Rewind/paramore.domain/Speakers/Speaker.cs
 delete mode 100644 Rewind/paramore.infrastructure/Domain/IAggregateRoot.cs
 delete mode 100644 Rewind/paramore.infrastructure/Domain/IEntity.cs
 delete mode 100644 Rewind/paramore.infrastructure/Domain/IRepository.cs
 delete mode 100644 Rewind/paramore.infrastructure/Domain/IUnitOfWork.cs
 delete mode 100644 Rewind/paramore.infrastructure/Domain/Id.cs
 delete mode 100644 Rewind/paramore.infrastructure/Domain/Version.cs
 delete mode 100644 Rewind/paramore.infrastructure/Raven/IAmADataObject.cs
 delete mode 100644 Rewind/paramore.infrastructure/Raven/IAmAUnitOfWorkFactory.cs
 delete mode 100644 Rewind/paramore.infrastructure/Raven/RavenConnection.cs
 delete mode 100644 Rewind/paramore.infrastructure/Raven/Repository.cs
 delete mode 100644 Rewind/paramore.infrastructure/Raven/UnitOfWork.cs
 delete mode 100644 Rewind/paramore.infrastructure/Raven/UnitOfWorkFactory.cs

[33mcommit 4a655a424de8c66e9ed37ee2f23501eac9df74d8[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Mar 7 18:19:36 2012 +0000

    remove transaction attribute - not really a good example

 delete mode 100644 Brighter/Examples/tasklist.web/Handlers/BeginTransaction.cs
 delete mode 100644 Brighter/Examples/tasklist.web/Handlers/BeginTransactionAttribute.cs

[33mcommit bbe30bedb5066cc023e25d4104c38150404b64ee[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Feb 14 18:34:55 2012 +0000

    Added an example of using a handler to validate a command

 delete mode 100644 Brighter/Examples/tasklist.web/Tests/TaskCommandHandler_UnitTests.cs

[33mcommit e6135d554cfef5c7ac9e453eb8ea5d844ff1daba[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Jan 10 22:56:51 2012 +0000

    Trying to get handler pipeline instantiated

 delete mode 100644 Brighter/paramore.commandprocessor.ioccontainers/IoCContainers/TinyIoC.cs

[33mcommit 7e66ae47d3b4fea2a9d44309818642f1f869405d[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sun Jan 1 16:45:48 2012 +0000

    work on the command processor example

 delete mode 100644 Brighter/Examples/tasklist.web/Tests/tasklistfixture.cs

[33mcommit e60dbf15551d8e3997e619de6b47b08ac8ce6f52[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Fri Dec 30 02:11:25 2011 +0000

    Work on an example

 delete mode 100644 Brighter/Examples/SampleSite/Modules/HelloWorld.cs
 delete mode 100644 Brighter/Examples/SampleSite/Web.Debug.config
 delete mode 100644 Brighter/Examples/SampleSite/Web.Release.config
 delete mode 100644 Brighter/Lib/Antlr3.Runtime.dll
 delete mode 100644 Brighter/Lib/AsyncCtpLibrary.dll
 delete mode 100644 Brighter/Lib/Castle.Core.dll
 delete mode 100644 Brighter/Lib/Castle.Core.xml
 delete mode 100644 Brighter/Lib/Castle.DynamicProxy2.dll
 delete mode 100644 Brighter/Lib/Castle.DynamicProxy2.xml
 delete mode 100644 Brighter/Lib/Castle.Facilities.Logging.dll
 delete mode 100644 Brighter/Lib/Castle.Facilities.Logging.xml
 delete mode 100644 Brighter/Lib/Castle.MicroKernel.dll
 delete mode 100644 Brighter/Lib/Castle.MicroKernel.xml
 delete mode 100644 Brighter/Lib/Castle.Services.Logging.NLogIntegration.dll
 delete mode 100644 Brighter/Lib/Castle.Services.Logging.NLogIntegration.xml
 delete mode 100644 Brighter/Lib/Castle.Services.Logging.log4netIntegration.dll
 delete mode 100644 Brighter/Lib/Castle.Services.Logging.log4netIntegration.xml
 delete mode 100644 Brighter/Lib/Castle.Windsor.dll
 delete mode 100644 Brighter/Lib/Castle.Windsor.xml
 delete mode 100644 Brighter/Lib/DotLiquid.dll
 delete mode 100644 Brighter/Lib/FSharp.PowerPack.Compatibility.dll
 delete mode 100644 Brighter/Lib/FSharp.PowerPack.dll
 delete mode 100644 Brighter/Lib/FakeItEasy-1.7.4166.27-NET3.5.zip
 delete mode 100644 Brighter/Lib/FakeItEasy-SL.dll
 delete mode 100644 Brighter/Lib/FakeItEasy.dll
 delete mode 100644 Brighter/Lib/FakeItEasy.xml
 delete mode 100644 Brighter/Lib/Gherkin.dll
 delete mode 100644 Brighter/Lib/HtmlAgilityPack.dll
 delete mode 100644 Brighter/Lib/HtmlAgilityPlus.dll
 delete mode 100644 Brighter/Lib/IKVM.OpenJDK.Core.dll
 delete mode 100644 Brighter/Lib/IKVM.OpenJDK.Text.dll
 delete mode 100644 Brighter/Lib/IKVM.Runtime.dll
 delete mode 100644 Brighter/Lib/InstallResharperRunner.5.1 - VS2010.bat
 delete mode 100644 Brighter/Lib/License-ServiceStack.txt
 delete mode 100644 Brighter/Lib/License-SisoDb.txt
 delete mode 100644 Brighter/Lib/Machine.Specifications.ReSharperRunner.6.0.dll
 delete mode 100644 Brighter/Lib/Machine.Specifications.ReSharperRunner.6.0.pdb
 delete mode 100644 Brighter/Lib/Machine.Specifications.Reporting.Templates.dll
 delete mode 100644 Brighter/Lib/Machine.Specifications.Reporting.dll
 delete mode 100644 Brighter/Lib/Machine.Specifications.Reporting.pdb
 delete mode 100644 Brighter/Lib/Machine.Specifications.SeleniumSupport.dll
 delete mode 100644 Brighter/Lib/Machine.Specifications.SeleniumSupport.pdb
 delete mode 100644 Brighter/Lib/Machine.Specifications.TDNetRunner.dll
 delete mode 100644 Brighter/Lib/Machine.Specifications.TDNetRunner.pdb
 delete mode 100644 Brighter/Lib/Machine.Specifications.WatinSupport.dll
 delete mode 100644 Brighter/Lib/Machine.Specifications.WatinSupport.pdb
 delete mode 100644 Brighter/Lib/Machine.Specifications.dll
 delete mode 100644 Brighter/Lib/Machine.Specifications.pdb
 delete mode 100644 Brighter/Lib/NDjango.Core40.dll
 delete mode 100644 Brighter/Lib/NLog.dll
 delete mode 100644 Brighter/Lib/NLog.pdb
 delete mode 100644 Brighter/Lib/NLog.xsd
 delete mode 100644 Brighter/Lib/Nancy.Authentication.Forms.dll
 delete mode 100644 Brighter/Lib/Nancy.Authentication.Forms.pdb
 delete mode 100644 Brighter/Lib/Nancy.Hosting.Aspnet.dll
 delete mode 100644 Brighter/Lib/Nancy.Hosting.Aspnet.pdb
 delete mode 100644 Brighter/Lib/Nancy.ViewEngines.DotLiquid.dll
 delete mode 100644 Brighter/Lib/Nancy.ViewEngines.DotLiquid.pdb
 delete mode 100644 Brighter/Lib/Nancy.ViewEngines.NDjango.dll
 delete mode 100644 Brighter/Lib/Nancy.ViewEngines.NDjango.pdb
 delete mode 100644 Brighter/Lib/Nancy.ViewEngines.Razor.dll
 delete mode 100644 Brighter/Lib/Nancy.ViewEngines.Razor.pdb
 delete mode 100644 Brighter/Lib/Nancy.ViewEngines.Spark.dll
 delete mode 100644 Brighter/Lib/Nancy.ViewEngines.Spark.pdb
 delete mode 100644 Brighter/Lib/Nancy.dll
 delete mode 100644 Brighter/Lib/Nancy.pdb
 delete mode 100644 Brighter/Lib/Newtonsoft.Json.dll
 delete mode 100644 Brighter/Lib/Newtonsoft.Json.pdb
 delete mode 100644 Brighter/Lib/Newtonsoft.Json.xml
 delete mode 100644 Brighter/Lib/Raven.Abstractions.dll
 delete mode 100644 Brighter/Lib/Raven.Abstractions.pdb
 delete mode 100644 Brighter/Lib/Raven.Client.Debug.dll
 delete mode 100644 Brighter/Lib/Raven.Client.Debug.pdb
 delete mode 100644 Brighter/Lib/Raven.Client.Lightweight.XML
 delete mode 100644 Brighter/Lib/Raven.Client.Lightweight.dll
 delete mode 100644 Brighter/Lib/Raven.Client.Lightweight.pdb
 delete mode 100644 Brighter/Lib/Raven.Client.MvcIntegration.dll
 delete mode 100644 Brighter/Lib/Raven.Client.MvcIntegration.pdb
 delete mode 100644 Brighter/Lib/Raven.Json.dll
 delete mode 100644 Brighter/Lib/Raven.Json.pdb
 delete mode 100644 Brighter/Lib/ServiceStack.Text.dll
 delete mode 100644 Brighter/Lib/ServiceStack.Text.pdb
 delete mode 100644 Brighter/Lib/ServiceStack.Text.xml
 delete mode 100644 Brighter/Lib/Spark.dll
 delete mode 100644 Brighter/Lib/Spark.pdb
 delete mode 100644 Brighter/Lib/SpecUnit.dll
 delete mode 100644 Brighter/Lib/SpecUnit.pdb
 delete mode 100644 Brighter/Lib/System.CoreEx.dll
 delete mode 100644 Brighter/Lib/System.Reactive.dll
 delete mode 100644 Brighter/Lib/System.Web.Razor.dll
 delete mode 100644 Brighter/Lib/TechTalk.SpecFlow.Generator.dll
 delete mode 100644 Brighter/Lib/TechTalk.SpecFlow.IdeIntegration.dll
 delete mode 100644 Brighter/Lib/TechTalk.SpecFlow.Parser.dll
 delete mode 100644 Brighter/Lib/TechTalk.SpecFlow.Reporting.dll
 delete mode 100644 Brighter/Lib/TechTalk.SpecFlow.Silverlight3.dll
 delete mode 100644 Brighter/Lib/TechTalk.SpecFlow.Utils.dll
 delete mode 100644 Brighter/Lib/TechTalk.SpecFlow.dll
 delete mode 100644 Brighter/Lib/TestDriven.Framework.dll
 delete mode 100644 Brighter/Lib/ThoughtWorks.Selenium.Core.dll
 delete mode 100644 Brighter/Lib/ThoughtWorks.Selenium.Core.pdb
 delete mode 100644 Brighter/Lib/WatiN.Core.dll
 delete mode 100644 Brighter/Lib/acknowledgements.txt
 delete mode 100644 Brighter/Lib/changelog.txt
 delete mode 100644 Brighter/Lib/fakeiteasylicence.txt
 delete mode 100644 Brighter/Lib/license.txt
 delete mode 100644 Brighter/Lib/log4net.dll
 delete mode 100644 Brighter/Lib/log4net.license.txt
 delete mode 100644 Brighter/Lib/log4net.xml
 delete mode 100644 Brighter/Lib/nunit.framework.dll
 delete mode 100644 Brighter/Lib/nunit.framework.xml
 delete mode 100644 Brighter/Lib/specflow.exe
 delete mode 100644 Brighter/Lib/test.exe
 delete mode 100644 Brighter/Lib/test.exe.config

[33mcommit def0754c28cae2450bd08e4cd9cf9d2d78da8d42[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Dec 28 16:45:11 2011 +0000

    Move to NuGet package dependencies - broken for the moment, need to fix Paramore.CommandProcessor to fix

 delete mode 100644 Rewind/Lib/Antlr3.Runtime.dll
 delete mode 100644 Rewind/Lib/Castle.Core.dll
 delete mode 100644 Rewind/Lib/Castle.Core.xml
 delete mode 100644 Rewind/Lib/Castle.DynamicProxy2.dll
 delete mode 100644 Rewind/Lib/Castle.DynamicProxy2.xml
 delete mode 100644 Rewind/Lib/Castle.Facilities.Logging.dll
 delete mode 100644 Rewind/Lib/Castle.Facilities.Logging.xml
 delete mode 100644 Rewind/Lib/Castle.MicroKernel.dll
 delete mode 100644 Rewind/Lib/Castle.MicroKernel.xml
 delete mode 100644 Rewind/Lib/Castle.Services.Logging.NLogIntegration.dll
 delete mode 100644 Rewind/Lib/Castle.Services.Logging.NLogIntegration.xml
 delete mode 100644 Rewind/Lib/Castle.Services.Logging.log4netIntegration.dll
 delete mode 100644 Rewind/Lib/Castle.Services.Logging.log4netIntegration.xml
 delete mode 100644 Rewind/Lib/Castle.Windsor.dll
 delete mode 100644 Rewind/Lib/Castle.Windsor.xml
 delete mode 100644 Rewind/Lib/DotLiquid.dll
 delete mode 100644 Rewind/Lib/FSharp.PowerPack.Compatibility.dll
 delete mode 100644 Rewind/Lib/FSharp.PowerPack.dll
 delete mode 100644 Rewind/Lib/FakeItEasy-SL.dll
 delete mode 100644 Rewind/Lib/FakeItEasy.dll
 delete mode 100644 Rewind/Lib/HtmlAgilityPack.dll
 delete mode 100644 Rewind/Lib/HtmlAgilityPlus.dll
 delete mode 100644 Rewind/Lib/License-ServiceStack.txt
 delete mode 100644 Rewind/Lib/License-SisoDb.txt
 delete mode 100644 Rewind/Lib/Machine.Specifications.ReSharperRunner.6.0.dll
 delete mode 100644 Rewind/Lib/Machine.Specifications.ReSharperRunner.6.0.pdb
 delete mode 100644 Rewind/Lib/Machine.Specifications.Reporting.dll
 delete mode 100644 Rewind/Lib/Machine.Specifications.Reporting.pdb
 delete mode 100644 Rewind/Lib/Machine.Specifications.SeleniumSupport.pdb
 delete mode 100644 Rewind/Lib/Machine.Specifications.TDNetRunner.pdb
 delete mode 100644 Rewind/Lib/Machine.Specifications.WatinSupport.pdb
 delete mode 100644 Rewind/Lib/Machine.Specifications.dll
 delete mode 100644 Rewind/Lib/Machine.Specifications.pdb
 delete mode 100644 Rewind/Lib/NDjango.Core40.dll
 delete mode 100644 Rewind/Lib/NLog.xsd
 delete mode 100644 Rewind/Lib/Nancy.Authentication.Forms.dll
 delete mode 100644 Rewind/Lib/Nancy.Authentication.Forms.pdb
 delete mode 100644 Rewind/Lib/Nancy.Hosting.Aspnet.dll
 delete mode 100644 Rewind/Lib/Nancy.Hosting.Aspnet.pdb
 delete mode 100644 Rewind/Lib/Nancy.ViewEngines.DotLiquid.dll
 delete mode 100644 Rewind/Lib/Nancy.ViewEngines.DotLiquid.pdb
 delete mode 100644 Rewind/Lib/Nancy.ViewEngines.NDjango.dll
 delete mode 100644 Rewind/Lib/Nancy.ViewEngines.NDjango.pdb
 delete mode 100644 Rewind/Lib/Nancy.ViewEngines.Razor.dll
 delete mode 100644 Rewind/Lib/Nancy.ViewEngines.Razor.pdb
 delete mode 100644 Rewind/Lib/Nancy.ViewEngines.Spark.dll
 delete mode 100644 Rewind/Lib/Nancy.ViewEngines.Spark.pdb
 delete mode 100644 Rewind/Lib/Nancy.dll
 delete mode 100644 Rewind/Lib/Nancy.pdb
 delete mode 100644 Rewind/Lib/Newtonsoft.Json.dll
 delete mode 100644 Rewind/Lib/Newtonsoft.Json.pdb
 delete mode 100644 Rewind/Lib/Raven.Abstractions.dll
 delete mode 100644 Rewind/Lib/Raven.Abstractions.pdb
 delete mode 100644 Rewind/Lib/Raven.Client.Lightweight.dll
 delete mode 100644 Rewind/Lib/Raven.Client.Lightweight.pdb
 delete mode 100644 Rewind/Lib/Raven.Json.dll
 delete mode 100644 Rewind/Lib/Raven.Json.pdb
 delete mode 100644 Rewind/Lib/ServiceStack.Text.dll
 delete mode 100644 Rewind/Lib/ServiceStack.Text.pdb
 delete mode 100644 Rewind/Lib/ServiceStack.Text.xml
 delete mode 100644 Rewind/Lib/Spark.dll
 delete mode 100644 Rewind/Lib/Spark.pdb
 delete mode 100644 Rewind/Lib/SpecUnit.dll
 delete mode 100644 Rewind/Lib/SpecUnit.pdb
 delete mode 100644 Rewind/Lib/System.CoreEx.dll
 delete mode 100644 Rewind/Lib/System.Reactive.dll
 delete mode 100644 Rewind/Lib/System.Web.Razor.dll
 delete mode 100644 Rewind/Lib/TechTalk.SpecFlow.IdeIntegration.dll
 delete mode 100644 Rewind/Lib/ThoughtWorks.Selenium.Core.dll
 delete mode 100644 Rewind/Lib/ThoughtWorks.Selenium.Core.pdb
 delete mode 100644 Rewind/Lib/WatiN.Core.dll
 delete mode 100644 Rewind/Lib/acknowledgements.txt
 delete mode 100644 Rewind/Lib/changelog.txt
 delete mode 100644 Rewind/Lib/log4net.dll
 delete mode 100644 Rewind/Lib/log4net.license.txt
 delete mode 100644 Rewind/Lib/log4net.xml
 delete mode 100644 Rewind/Lib/nunit.framework.dll
 delete mode 100644 Rewind/Lib/paramore.commandprocessor.dll
 delete mode 100644 Rewind/Lib/paramore.commandprocessor.ioccontainers.dll
 delete mode 100644 Rewind/Lib/paramore.commandprocessor.ioccontainers.pdb
 delete mode 100644 Rewind/Lib/paramore.commandprocessor.pdb
 delete mode 100644 Rewind/Lib/test.exe
 delete mode 100644 Rewind/Lib/test.exe.config
 delete mode 100644 Rewind/paramore.commandprocessor/IChainPathExplorer.cs
 delete mode 100644 Rewind/paramore.web/Content/Scripts/jquery-1.4.1-vsdoc.js
 delete mode 100644 Rewind/paramore.web/Content/Scripts/jquery-1.4.1.js
 delete mode 100644 Rewind/paramore.web/Content/Scripts/jquery-1.4.1.min.js
 delete mode 100644 Rewind/paramore.web/Content/Scripts/modernizr-2.0.6.js
 delete mode 100644 Rewind/paramore.web/Content/Web.config
 delete mode 100644 Rewind/paramore.web/Modules/EntrypointModule.cs
 delete mode 100644 Rewind/paramore.web/Modules/ScheduledMeetingsModule.cs
 delete mode 100644 Rewind/paramore.web/Modules/VenueModule.cs
 delete mode 100644 Rewind/paramore.web/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.web/Web.Debug.config
 delete mode 100644 Rewind/paramore.web/Web.Release.config
 delete mode 100644 Rewind/paramore.web/Web.config
 delete mode 100644 Rewind/paramore.web/paramore.web.csproj
 delete mode 100644 Rewind/paramore.web/paramore.web.csproj.usr

[33mcommit 080cc6a096e5027fdf9516480da36e39fd56692a[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Dec 28 15:22:25 2011 +0000

    check in path explorer

 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/ChainOfResponsibilityTests.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/CommandProcessorTests.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyCommand.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyCommandHandler.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyDependentCommandHandler.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyDoubleDecoratedHandler.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyEntity.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyEvent.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyEventHandler.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyImplicitHandler.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyLoggingHander.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyLoggingHandlerAttribute.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyOtherEventHandler.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyPostLoggingHandlerAttribute.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyPreAndPostDecoratedHandler.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyPreValidationHandlerAttribute.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyUnitOfWorkFactory.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyValidationHandler.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/CommandProcessors/TestDoubles/MyValidationHandlerAttribute.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.commandprocessor.tests/paramore.commandprocessor.tests.csproj
 delete mode 100644 Rewind/paramore.commandprocessor/ChainPathExplorer.cs
 delete mode 100644 Rewind/paramore.commandprocessor/ChainofResponsibilityBuilder.cs
 delete mode 100644 Rewind/paramore.commandprocessor/Chains.cs
 delete mode 100644 Rewind/paramore.commandprocessor/Command.cs
 delete mode 100644 Rewind/paramore.commandprocessor/CommandProcessor.cs
 delete mode 100644 Rewind/paramore.commandprocessor/Event.cs
 delete mode 100644 Rewind/paramore.commandprocessor/HandlerFactory.cs
 delete mode 100644 Rewind/paramore.commandprocessor/HandlerName.cs
 delete mode 100644 Rewind/paramore.commandprocessor/HandlerTiming.cs
 delete mode 100644 Rewind/paramore.commandprocessor/IChainofResponsibilityBuilder.cs
 delete mode 100644 Rewind/paramore.commandprocessor/ICommand.cs
 delete mode 100644 Rewind/paramore.commandprocessor/IHandleRequests.cs
 delete mode 100644 Rewind/paramore.commandprocessor/IRequest.cs
 delete mode 100644 Rewind/paramore.commandprocessor/Properties/AssemblyInfo.cs
 delete mode 100644 Rewind/paramore.commandprocessor/ReflectionExtensions.cs
 delete mode 100644 Rewind/paramore.commandprocessor/RequestHandler.cs
 delete mode 100644 Rewind/paramore.commandprocessor/RequestHandlerAttribute.cs
 delete mode 100644 Rewind/paramore.commandprocessor/RequestHandlers.cs
 delete mode 100644 Rewind/paramore.commandprocessor/paramore.commandprocessor.csproj
 delete mode 100644 Rewind/paramore.utility/TinyIoC.cs

[33mcommit 955b9f1815af7aad981eb3f42ab4adab43071858[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Nov 10 22:14:43 2011 +0000

    final cleanup

 delete mode 100644 Ignorance/paramore.utility/TinyIoC.cs
 delete mode 100644 Ignorance/story_backlog.txt

[33mcommit cc4da7c22e9a5a690315215f265a5336bab2b157[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Nov 10 22:04:09 2011 +0000

    trying to fix feature files

 delete mode 100644 Rewind/Lib/TechTalk.SpecFlow.VsIntegration.dll
 delete mode 100644 Rewind/Lib/TechTalk.SpecFlow.targets
 delete mode 100644 Rewind/Lib/TechTalk.SpecFlow.tasks
 delete mode 100644 Rewind/Lib/specflow.exe
 delete mode 100644 Rewind/paramore.features/AddNewVenue.feature
 delete mode 100644 Rewind/paramore.features/AddNewVenue.feature.cs
 delete mode 100644 Rewind/paramore.features/ScheduleAMeeting.feature.cs

[33mcommit 8ff39dbdbf3dfc54552ebc47073ffb3cb4b46dda[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Nov 10 15:21:52 2011 +0000

    try to fix name issue

 delete mode 100644 ignorance/_ReSharper.paramore/JbDecompilerCache/decompiler/Raven.Client.Lightweight-55ec/t/Raven/Client/IDocumentStore.cs
 delete mode 100644 ignorance/paramore.domain/Meetings/FiftyPercentOverbookingPolicy.cs
 delete mode 100644 ignorance/paramore.domain/Meetings/IAmAnOverbookingPolicy.cs
 delete mode 100644 ignorance/paramore.domain/Meetings/IIssueTickets.cs
 delete mode 100644 ignorance/paramore.domain/Meetings/IScheduler.cs
 delete mode 100644 ignorance/paramore.domain/Meetings/Scheduler.cs
 delete mode 100644 ignorance/paramore.domain/Meetings/Ticket.cs
 delete mode 100644 ignorance/paramore.domain/Meetings/TicketIssuer.cs
 delete mode 100644 ignorance/paramore.domain/Meetings/Tickets.cs
 delete mode 100644 ignorance/paramore.infrastructure/Domain/IUnitOfWork.cs
 delete mode 100644 ignorance/paramore.infrastructure/Raven/IAmADataObject.cs
 delete mode 100644 ignorance/paramore.infrastructure/Raven/IAmAUnitOfWorkFactory.cs
 delete mode 100644 ignorance/paramore.infrastructure/Raven/Repository.cs
 delete mode 100644 ignorance/paramore.infrastructure/Raven/UnitOfWork.cs
 delete mode 100644 ignorance/paramore.tests/domain/Meetings/OverbookingPolicyTests.cs
 delete mode 100644 ignorance/paramore.tests/domain/Meetings/SchedulerTests.cs
 delete mode 100644 ignorance/paramore.tests/domain/Meetings/TicketIssuerTests.cs

[33mcommit 5a6794f7c036e9eabc46b452127269de5292500b[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Nov 9 23:50:30 2011 +0000

    try to pull out command processor

 delete mode 100644 Ignorance/paramore.infrastructure/TinyIoC/TinyIoC.cs
 delete mode 100644 Ignorance/paramore.services/CommandHandlers/HandlerName.cs
 delete mode 100644 Ignorance/paramore.services/CommandHandlers/HandlerTiming.cs
 delete mode 100644 Ignorance/paramore.services/CommandHandlers/IHandleCommands.cs
 delete mode 100644 Ignorance/paramore.services/CommandHandlers/RequestHandler.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/ChainPathExplorer.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/ChainofResponsibilityBuilder.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/Chains.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/Command.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/CommandProcessor.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/Event.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/HandlerFactory.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/IChainofResponsibilityBuilder.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/ICommand.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/ReflectionExtensionMethods/ReflectionExtensionMethods.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/RequestHandlerRequiredDecoratorAttribute.cs
 delete mode 100644 Ignorance/paramore.services/CommandProcessors/RequestHandlers.cs
 delete mode 100644 Ignorance/paramore.services/Common/IRequest.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/ChainOfResponsibilityTests.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/CommandProcessorTests.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyCommand.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyCommandHandler.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyDependentCommandHandler.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyDoubleDecoratedHandler.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyEntity.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyEvent.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyEventHandler.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyImplicitHandler.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyLoggingHander.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyLoggingHandlerAttribute.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyOtherEventHandler.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyPostLoggingHandlerAttribute.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyPreAndPostDecoratedHandler.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyPreValidationHandlerAttribute.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyUnitOfWorkFactory.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyValidationHandler.cs
 delete mode 100644 Ignorance/paramore.tests/services/CommandProcessors/TestDoubles/MyValidationHandlerAttribute.cs
 delete mode 100644 ignorance/paramore.services/CommandProcessors/ChainPathExplorer.cs
 delete mode 100644 ignorance/paramore.services/CommandProcessors/ChainofResponsibilityBuilder.cs
 delete mode 100644 ignorance/paramore.services/CommandProcessors/Chains.cs
 delete mode 100644 ignorance/paramore.services/CommandProcessors/CommandProcessor.cs
 delete mode 100644 ignorance/paramore.services/CommandProcessors/HandlerFactory.cs
 delete mode 100644 ignorance/paramore.services/CommandProcessors/IChainofResponsibilityBuilder.cs
 delete mode 100644 ignorance/paramore.services/CommandProcessors/ReflectionExtensionMethods/ReflectionExtensionMethods.cs
 delete mode 100644 ignorance/paramore.services/CommandProcessors/RequestHandlerRequiredDecoratorAttribute.cs
 delete mode 100644 ignorance/paramore.services/CommandProcessors/RequestHandlers.cs

[33mcommit da33ea9f0f0304dea4853e6e76fa971b99f284be[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Sep 7 17:22:47 2011 +0100

    First steps with Nancy

 delete mode 100644 Ignorance/paramore.features/Steps/AddNewvenueSteps.cs
 delete mode 100644 Ignorance/paramore.web/Views/Meeting.cshtml

[33mcommit 2a1560e25501c95a02602090f4d4526bdab8ad1b[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Aug 6 23:37:54 2011 +0100

    get the acceptance test passing, make more value types for collaborating objects

 delete mode 100644 Ignorance/paramore.domain/Common/Address.cs
 delete mode 100644 Ignorance/paramore.domain/Locations/Location.cs
 delete mode 100644 Ignorance/paramore.domain/Locations/LocationContact.cs
 delete mode 100644 Ignorance/paramore.domain/Locations/LocationMap.cs
 delete mode 100644 Ignorance/paramore.domain/Locations/LocationName.cs
 delete mode 100644 ignorance/paramore.infrastructure/Raven/BootstrapRaven.cs

[33mcommit 76579610f05ed970bcbf4a273cd4d7e2b96bfe0b[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Aug 6 18:40:52 2011 +0100

    Work to hook up raven

 delete mode 100644 Ignorance/Lib/NLog.xml
 delete mode 100644 Ignorance/Lib/SQLite.NET/Doc/SQLite.NET.chm
 delete mode 100644 Ignorance/Lib/SQLite.NET/readme.htm
 delete mode 100644 Ignorance/Lib/SQLite/sqlite-3_6_23_1.zip
 delete mode 100644 Ignorance/Lib/SQLite/sqlite3.def
 delete mode 100644 Ignorance/Lib/SQLite/sqlite3.dll
 delete mode 100644 Ignorance/Lib/SQLite/sqlite3.exe
 delete mode 100644 Ignorance/Lib/SQLite/sqlite3_analyzer-3.6.1.zip
 delete mode 100644 Ignorance/Lib/SQLite/sqlite3_analyzer.exe
 delete mode 100644 Ignorance/Lib/SQLite/sqlitedll-3_6_23_1.zip
 delete mode 100644 Ignorance/paramore.domain/Locations/LocationFactory.cs
 delete mode 100644 Ignorance/paramore.domain/Speakers/SpeakerFactory.cs

[33mcommit b30d703cb44994dad3933601f41a9a4e5dc4b4d5[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Aug 4 15:48:27 2011 +0100

    add support for RavenDb as the database on this one

 delete mode 100644 Ignorance/Lib/SisoDb.Sql2008.dll
 delete mode 100644 Ignorance/Lib/SisoDb.Sql2008.pdb
 delete mode 100644 Ignorance/Lib/SisoDb.dll
 delete mode 100644 Ignorance/Lib/SisoDb.pdb
 delete mode 100644 Ignorance/Lib/SisoDb.xml

[33mcommit 6e5a1ce3261a34df11dff6178c101e08802ad413[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Aug 2 18:11:26 2011 +0100

    wrking on overbooking policy

 delete mode 100644 Ignorance/paramore.domain/Meetings/MeetingFactory.cs

[33mcommit 1355c639aebbf3b248c2e70f2db0c6cf0ba656b7[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Sat Jul 30 17:05:30 2011 +0100

    adding events to the command processor

 delete mode 100644 Ignorance/paramore.services/Events/Location/LocationCreatedEvent.cs
 delete mode 100644 Ignorance/paramore.services/Events/Speaker/DomainEvent.cs
 delete mode 100644 Ignorance/paramore.services/Events/Speaker/SpeakerCreatedEvent.cs
 delete mode 100644 ignorance/paramore.services/CommandProcessors/ChainOfRepsonsibility.cs

[33mcommit 57e923192f5c0fed8b5e867c64a032cb55decd71[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Jul 26 18:59:55 2011 +0100

    Interim

 delete mode 100644 Ignorance/Lib/Rhino.Mocks.dll
 delete mode 100644 Ignorance/Lib/Rhino.Mocks.xml

[33mcommit 9d965f28b1d09cbc7d75e15b3a9a2452aeef86c3[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Mon Jul 25 10:57:29 2011 +0100

    update to working r# version https://github.com/machine/machine.specifications#readme

 delete mode 100644 Ignorance/Lib/Machine.Specifications.GallioAdapter.3.1.dll
 delete mode 100644 Ignorance/Lib/Machine.Specifications.GallioAdapter.3.1.pdb
 delete mode 100644 Ignorance/Lib/Machine.Specifications.GallioAdapter.plugin
 delete mode 100644 Ignorance/Lib/Machine.Specifications.ReSharperRunner.5.1.dll
 delete mode 100644 Ignorance/Lib/Machine.Specifications.ReSharperRunner.5.1.pdb
 delete mode 100644 Ignorance/Lib/Machine.Specifications.dll.tdnet
 delete mode 100644 Ignorance/Lib/mspec-clr4.exe
 delete mode 100644 Ignorance/Lib/mspec-clr4.pdb
 delete mode 100644 Ignorance/Lib/mspec-x86-clr4.exe
 delete mode 100644 Ignorance/Lib/mspec-x86-clr4.pdb
 delete mode 100644 Ignorance/Lib/mspec-x86.exe
 delete mode 100644 Ignorance/Lib/mspec-x86.pdb
 delete mode 100644 Ignorance/Lib/mspec.exe
 delete mode 100644 Ignorance/Lib/mspec.pdb

[33mcommit 37fc602130c33b72ced5bc378ae8114c4d74e8cb[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Jul 20 19:07:32 2011 +0100

    renaming to match new project name:

 delete mode 100644 Ignorance/UserGroupManagement.Configuration/ConfigurationHelper.cs
 delete mode 100644 Ignorance/UserGroupManagement.Configuration/Properties/AssemblyInfo.cs
 delete mode 100644 Ignorance/UserGroupManagement.Configuration/UserGroupManagement.Configuration.csproj
 delete mode 100644 Ignorance/UserGroupManagement.Configuration/UserGroupManagement.Configuration.csproj.user
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Common/Address.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Common/EmailAddress.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Common/PhoneNumber.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Locations/ContactName.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Locations/Location.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Locations/LocationContact.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Locations/LocationFactory.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Locations/LocationMap.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Locations/LocationName.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Meetings/Meeting.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Meetings/MeetingFactory.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Properties/AssemblyInfo.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Speakers/Speaker.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Speakers/SpeakerBio.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Speakers/SpeakerFactory.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/Speakers/SpeakerName.cs
 delete mode 100644 Ignorance/UserGroupManagement.Domain/UserGroupManagement.Domain.csproj
 delete mode 100644 Ignorance/UserGroupManagement.Features/Properties/AssemblyInfo.cs
 delete mode 100644 Ignorance/UserGroupManagement.Features/ScheduleAMeeting.feature
 delete mode 100644 Ignorance/UserGroupManagement.Features/ScheduleAMeeting.feature.cs
 delete mode 100644 Ignorance/UserGroupManagement.Features/Steps/ScheduleAMeetingSteps.cs
 delete mode 100644 Ignorance/UserGroupManagement.Features/UserGroupManagement.Features.csproj
 delete mode 100644 Ignorance/UserGroupManagement.Infrastructure/Domain/IAggregateRoot.cs
 delete mode 100644 Ignorance/UserGroupManagement.Infrastructure/Domain/IEntity.cs
 delete mode 100644 Ignorance/UserGroupManagement.Infrastructure/Domain/IRepository.cs
 delete mode 100644 Ignorance/UserGroupManagement.Infrastructure/Properties/AssemblyInfo.cs
 delete mode 100644 Ignorance/UserGroupManagement.Infrastructure/UserGroupManagement.Infrastructure.csproj
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandHandlers/HandlerName.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandHandlers/HandlerTiming.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandHandlers/IHandleCommands.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandHandlers/RequestHandler.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandHandlers/ScheduleMeetingCommandHandler.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandProcessor/ChainOfRepsonsibility.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandProcessor/ChainPathExplorer.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandProcessor/ChainofResponsibilityBuilder.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandProcessor/Chains.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandProcessor/HandlerFactory.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandProcessor/ReflectionExtensionMethods/ReflectionExtensionMethods.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandProcessor/RequestHandlerRequiredDecoratorAttribute.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/CommandProcessor/RequestHandlers.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/Commands/Command.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/Commands/ICommand.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/Commands/ScheduleMeetingCommand.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/Common/IRequest.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/Events/Location/LocationCreatedEvent.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/Events/Meeting/MeetingScheduledEvent.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/Events/Speaker/DomainEvent.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/Events/Speaker/SpeakerCreatedEvent.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/Properties/AssemblyInfo.cs
 delete mode 100644 Ignorance/UserGroupManagement.ServiceLayer/UserGroupManagement.ServiceLayer.csproj
 delete mode 100644 Ignorance/UserGroupManagement.Tests/CommandProcessor/ChainOfResponsibilityTests.cs
 delete mode 100644 Ignorance/UserGroupManagement.Tests/Properties/AssemblyInfo.cs
 delete mode 100644 Ignorance/UserGroupManagement.Tests/UserGroupManagement.Tests.csproj
 delete mode 100644 Ignorance/UserGroupManagement.Utility/EnumerationExtensions.cs
 delete mode 100644 Ignorance/UserGroupManagement.Utility/Properties/AssemblyInfo.cs
 delete mode 100644 Ignorance/UserGroupManagement.Utility/UserGroupManagement.Utility.csproj
 delete mode 100644 Ignorance/UserGroupManagement.Web/Content/Scripts/jquery-1.4.1-vsdoc.js
 delete mode 100644 Ignorance/UserGroupManagement.Web/Content/Scripts/jquery-1.4.1.js
 delete mode 100644 Ignorance/UserGroupManagement.Web/Content/Scripts/jquery-1.4.1.min.js
 delete mode 100644 Ignorance/UserGroupManagement.Web/Properties/AssemblyInfo.cs
 delete mode 100644 Ignorance/UserGroupManagement.Web/UserGroupManagement.Web.csproj
 delete mode 100644 Ignorance/UserGroupManagement.Web/UserGroupManagement.Web.csproj.user
 delete mode 100644 Ignorance/UserGroupManagement.Web/Web.Debug.config
 delete mode 100644 Ignorance/UserGroupManagement.Web/Web.Release.config
 delete mode 100644 Ignorance/UserGroupManagement.Web/Web.config
 delete mode 100644 Ignorance/UserGroupManagement.sln
 delete mode 100644 Ignorance/_ReSharper.UserGroupManagement/DecompilerCache/metadata/System-496f/Assembly.Location.txt
 delete mode 100644 Ignorance/_ReSharper.UserGroupManagement/DecompilerCache/metadata/System-496f/System.ComponentModel.Component.cs

[33mcommit 919682578e4f865f4409852b9bfd956e82509d1c[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Thu Jun 30 18:05:54 2011 +0100

    probably does not pass tests, just overnight

 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Tests/CommandProcessor/CommandProcessor.cs

[33mcommit a57bd880646e99ec32b2863860b5032a9253c767[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Wed Jun 29 14:46:55 2011 +0100

    added first version of chain of responsibility builder

 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Tests/Commands/ShouldHaveCommandHandlerForCommand.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Tests/Configuration/ConcerningConfigurationHelper/WhenLookingForAllTheCommandHandlers.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Tests/Configuration/ConcerningConfigurationHelper/WhenLookingForAllTheCommands.cs

[33mcommit f7a384fc05a2e7f3623f769eafaa0217aac1e2df[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Jun 28 18:13:55 2011 +0100

    not sure why this is not committing

 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Domain/Location/ContactName.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Domain/Location/Location.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Domain/Location/LocationContact.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Domain/Location/LocationFactory.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Domain/Location/LocationMap.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Domain/Location/LocationName.cs

[33mcommit 9e47b9d4875ba40a39a60b7816447f2391aa0a50[m
Author: Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
Date:   Tue Jun 28 12:59:13 2011 +0100

    clean up for DDD work

 delete mode 100644 UserGroup_DDD-CQRS/Lib/Fohjin.DDD.Bus.dll
 delete mode 100644 UserGroup_DDD-CQRS/Lib/Fohjin.DDD.Bus.pdb
 delete mode 100644 UserGroup_DDD-CQRS/Lib/Fohjin.DDD.EventStore.SQLite.dll
 delete mode 100644 UserGroup_DDD-CQRS/Lib/Fohjin.DDD.EventStore.SQLite.pdb
 delete mode 100644 UserGroup_DDD-CQRS/Lib/Fohjin.DDD.EventStore.dll
 delete mode 100644 UserGroup_DDD-CQRS/Lib/Fohjin.DDD.EventStore.pdb
 delete mode 100644 UserGroup_DDD-CQRS/Lib/Fohjin.DDD.Events.dll
 delete mode 100644 UserGroup_DDD-CQRS/Lib/Fohjin.DDD.Events.pdb
 delete mode 100644 UserGroup_DDD-CQRS/Lib/Fohjin.DDD.Reporting.dll
 delete mode 100644 UserGroup_DDD-CQRS/Lib/Fohjin.DDD.Reporting.pdb
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.CommandHandlers/IHandleCommands.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.CommandHandlers/Properties/AssemblyInfo.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.CommandHandlers/ScheduleMeetingCommandHandler.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.CommandHandlers/UserGroupManagement.CommandHandlers.csproj
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Commands/Command.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Commands/ICommand.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Commands/Properties/AssemblyInfo.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Commands/ScheduleMeetingCommand.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Commands/UserGroupManagement.Commands.csproj
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Commands/UserGroupManagement.Commands.csproj.user
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Configuration/DomainDatabaseBootStrapper.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Configuration/ReportingDatabaseBootStrapper.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Domain/Momentos/LocationMemento.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Domain/Momentos/MeetingMemento.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Domain/Momentos/SpeakerMemento.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Events/Location/LocationCreatedEvent.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Events/Meeting/MeetingScheduledEvent.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Events/Properties/AssemblyInfo.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Events/Speaker/SpeakerCreatedEvent.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Events/UserGroupManagement.Events.csproj
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Reporting.Dto/MeetingDetailsReport.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Reporting.Dto/MeetingReport.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Reporting.Dto/Properties/AssemblyInfo.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Reporting.Dto/UserGroupManagement.Reporting.Dto.csproj
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.SlowTests/ConcerningScheduleMeetingCommandHandler/WhenSchedulingANewMeeting.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.SlowTests/Properties/AssemblyInfo.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.SlowTests/Reporting/Dto/ReportFixture.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.SlowTests/UserGroupManagement.SlowTests.csproj
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Tests/Commands/ShouldHaveSerializeableCommands.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Tests/Domain/ConcerningLocations/WhenCreatingAMementoForALocation.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Tests/Domain/ConcerningMeetings/WhenCreatingAMementoForAMeeting.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement.Tests/Domain/ConcerningSpeakers/WhenCreatingAMementoForASpeaker.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Content/Site.css
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Controllers/AccountController.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Controllers/HomeController.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Global.asax
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Global.asax.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Models/AccountModels.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Properties/AssemblyInfo.cs
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/MicrosoftAjax.debug.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/MicrosoftAjax.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/MicrosoftMvcAjax.debug.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/MicrosoftMvcAjax.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/MicrosoftMvcValidation.debug.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/MicrosoftMvcValidation.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/jquery-1.4.1-vsdoc.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/jquery-1.4.1.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/jquery-1.4.1.min.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/jquery.validate-vsdoc.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/jquery.validate.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Scripts/jquery.validate.min.js
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/UserGroupManagement.csproj
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/UserGroupManagement.csproj.user
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Views/Account/ChangePassword.aspx
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Views/Account/ChangePasswordSuccess.aspx
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Views/Account/LogOn.aspx
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Views/Account/Register.aspx
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Views/Home/About.aspx
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Views/Home/Index.aspx
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Views/Shared/Error.aspx
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Views/Shared/LogOnUserControl.ascx
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Views/Shared/Site.Master
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Views/Web.config
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Web.Debug.config
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Web.Release.config
 delete mode 100644 UserGroup_DDD-CQRS/UserGroupManagement/Web.config
