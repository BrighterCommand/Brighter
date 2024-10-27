# **Contributors Wanted: Inquire Within** #
It isn't wise to build something that can be a part of someone's ecosystem without help, and we are always open to contributions to help us fix issues or work on features

## Getting Started ##

### Making Changes ###

- Make sure you have the latest version
- Submit a ticket for the issue, using the GitHub Issue Tracker
- It's worth checking first to see if someone else has raised your issue. We might have responded to them, or someone might be fixing it.
- If you want to add a feature, as opposed to fixing something that is broken, you should still raise an issue. 
	- That helps us understand what you want to add, so we can give more specific advice and check it does not conflict with some work in progress on a branch etc. 
	- If someone else is already working on the suggestion, then hopefully you can collaborate with them.
	- Add a comment to an issue if you pick it up to work on, so everyone else knows.
- If you have a defect please include the following:
	- Steps to reproduce the issue
	- A failing test if possible (see below)
	- The stacktrace for any errors you encountered


### Submitting Changes ###

0. Fork the project
0. Clone your fork
0. Branch your fork. This is your active development branch   
0.  We separate the core library from add-ons. 

        1.  Consider if your change is really core, or could be shipped as an add-on. Let the user 'buy-in' to your feature over making them take it.
        2.  Minimize your dependency on other libraries - try to limit yourself to the ones you **need** so you don't force consumers to buy a chain of dependencies
        3.  As an example, for a transport, prefer ADO.NET over using an ORM to avoid additional dependencies on other OSS frameworks
        4.  There are obvious exceptions, AWS SDK for AWS components for example. If in doubt ask.    
        
0. If this is a Core project (Brighter or ServiceActivator) 

        1. Use Test Driven-Development. 
	        1. New behaviors should have a test or 
		2. You might want to watch [this video](http://vimeo.com/68375232) to understand our preferred testing approach 
	        3. This project uses [FakeItEasy](https://github.com/FakeItEasy/FakeItEasy). So should you to contribute.
	        
0. If this is a non-core project, such as a Transport or a Store

        1. You may use a Test-After approach here if you prefer, as you are implementing a defined interface for a plug-in
        2. Your testing strategy here may focus on protecting against regression of these components once working
        3. Consider providing a sample to run the code
        4. Consider chaos engineering approaches, i.e. use blockade to simulate network partitions, restart the broker etc.  
              
0. Try to follow the [Microsoft .NET Framework Design Guidelines] (https://github.com/dotnet/corefx/tree/master/Documentation#coding-guidelines)

	1. Providing [BDD] (http://dannorth.net/introducing-bdd/) style tests should provide for the need to use scenarios to test the design of your API
	2. Use the coding style from [dotnet/corefx] (https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md)
	3. You can use [codeformatter] (https://github.com/dotnet/codeformatter) if you can run VS2015 to automatically update your format
        4. Ensure you update the template for your copyright if using codeformatter 
		
0. Make your tests pass   
0. Commit

	1. Try to write a [good commit message](http://tbaggery.com/2008/04/19/a-note-about-git-commit-messages.html).
	    
0. Merge back into your fork
0. Push to your fork
0. Submit a pull request
0. Sit back, and wait. 

	1. Try pinging @BrighterCommmand on Twitter if you hear nothing

### Architecture Decision Record ###
If your pull request makes a change to the design of Brighter, please include an [Architectural Decision Record](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)(ADR) for the change. This should describe the agreed design for the change. The ADR helps us review the change, so that we can understand that we have built what was agreed. (In addition, an ADR captures design decisions so that we can understand why implementation choices were made later).

You can create the ADR as the first step on a new branch/fork. Then your first commit includes the ADR describing the change. This then allows you to create a draft PR which includes the ADR. This allows others to review their understanding of the change, and give feedback early on if those expectations are mismatched. If you update the design as you learn, add another ADR that supercedes the old one.

### Conventional Commits ###
In order to keep a clean commit history, please use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/#specification) when contributing to Brighter.

### Contributor Licence Agreement ###
To safeguard the project we ask you to sign a Contributor Licence Agreement. The goal is to let you keep your copyright, but to assign it to the project so that it can use it in perpetuity. It is still yours, but the project is not at risk from having multiple contributors holding the copyright, with anyone able to hold it to ransom by removing their grant of licence.

The process of signing works through GitHub.

To get started, <a href="https://www.clahub.com/agreements/iancooper/Paramore">sign the Contributor License Agreement</a>. 

### Contributor Code of Conduct ###
Please note that this project is released with a Contributor Code of Conduct. By participating in this project you agree to abide by its terms.

The code of conduct is from [Contributor Covenant](http://contributor-covenant.org/)

