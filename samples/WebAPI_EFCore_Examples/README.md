# Web API and EF Core Example
This sample shows a typical scenario when using WebAPI and Brighter/Darker. It demonstrates both using Brigher and Darker to implement the API endpoints, and using a work queue to handle asynchronous work that results from handling the API call.

## Architecture

## Build and Deploy

### Building

Use the build.sh file to build the project and publish it to the out directory. The Dockerfile assumes the app will be published here. Why not use a multi-stage Docker build? We can't do this as the projects here reference projects not NuGet packages for Brighter libraries and there are not in the Docker build context.

A common error is to chanbe something, forget to run build.sh and thus copy the old version to the Docker image.




