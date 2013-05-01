# Introduction

Welcome to the Durandal Starter Project!

Durandal is a cross-device, cross-platform application framework 
designed to make building Single Page Applications (SPAs) easy to create and maintain. 
We've used it to build build apps for PC, Mac, Linux, iOS and Android...and now it's your turn...

This template sets up a basic navigation-style architecture for you. Below are a few 
"points of interest" for you to investigate along with some brief explanation. For further
information please visit us at http://www.durandaljs.com

## Points of Interest

* The main app code lives under *App* and is organized into AMD modules and HTML views.
* Css, fonts and images can be found under *Content*.
* Third party script libraries are located under *Scripts*.
* Css and script library bundling has been configured in *App_Start/DurandalBundleConfig.cs*.
* The *Durandal/Index* view contains the host page's html, links and script references.
* Application startup begins with the *App/main.js*
* Notice the differences in the way that the welcome module is declared (constructor function) vs. the flicr module (object instance).

## Explanation of the SPA Architecture

The application starts when require.js loads and executes the applications's *main.js* module.
This module starts up Durandal, does some configuration and then tells the framework to display
the application's *shell*. You can think of this loosely as a "main window." In Durandal, a UI is
constructed by building View Models (aka Presentation Models) or Controllers and Views. The framework
binds them together and inserts them into the DOM. Durandal's composition features allow you to break
down very complex user interfaces into small modules which can be recursively composed together. This 
technique makes even the most complex user interfaces relatively easy to build. In this starter kit the
*viewmodels/shell* module is bound to the "views/shell" html and inserted into the DOM. The shell view
then composes in "page" view models based on the navigation location. Navigation is handled by the router
plugin. Under the covers it uses SammyJS, but ultimately urls get mapped to modules which
then get composed into the page by the shell. This happens because the router changes the value of the 
*activeItem* property which is bound in the shell's view. Each page view model is free to follow it's own architecure internally, depending on 
what makes the most sense for the feature being developed. The shell is unconcerned with the inner details of the
pages it is displaying, as you would hope. To build on top of this sample, begin by creating view models for
the pages you wish to add to the navigation structure. In main.js, register the view models with the router.
Create a view for each module following the naming convention you see presented here.

## Going Further

Durandal is built on jQuery, Knockout and RequireJS. So, you have all the power of those tools
available to you to build your application. Additionally, this template uses SammyJS for navigation. It also
provides you with a powerful CSS framework to get started with: Bootstrap, which has been augmented
with Font-Awesome to supply you with hundreds of font-based (vector) icons applicable with a simple 
CSS class. Durandal builds on top of this by providng you with a consistent API specifically designed for
SPA development. The API uses promises for all asynchronous code, highly favors modularization and
composition, and gives you a simple convention for organizing and easily optimizing your project. All of Durandal is
provided as AMD modules, each of which has it's API documented on web site at http://durandaljs.com/pages/docs. 
You can head over there for more in depth information. There are also other articles you may wish to read in order to learn how to create
skinnable, bindable and templatable widgets as well as how to use the optimizer to create the main-built.js file you
see here, which contains all the JS and HTML for your app in a single file.

We sincerely hope you enjoy working with Durandal and that it makes your SPA application development a joy. Please 
join the community by participating in our google group, forking and submitting pull requests for bug fixes and new
feature ideas.

With Warm Wishes for Excellent Application Development,

Rob Eisenberg

(aka EisenbergEffect)