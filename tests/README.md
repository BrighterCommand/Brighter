# Tests

## Developer Tests

These are test that use a TDD approach, and create [Developer Tests](https://wiki.c2.com/?DeveloperTest). As such, a failing test always implicates the last edit.

Use Mocks only for:

* Test Isolation: A shared fixture, such as a Database, File, Time makes it hard to have tests run together. Use a Mock to substitute for a shared fixture to ensure isolation of the **test**. Note isolation is **not** of the SUT i.e. class, but the **test**.
* Slow Tests: Tests should be fast, again a Mock can be used to replace a slow component, usually I/O to ensure we get fast feedback.
* Fragile Tests: A test that does not run predictably, often due to I/O issues. Replace the source of unpredictability.

Paramore.Brighter.Core.Tests has Developer Tests.

## Automated Tests

These tests may be Test After i.e. we implement something and then want to avoid regression. Most of our tests around middleware and backing stores are of this form. They are often:

* Not-Isolated: They use a shared resource, often running in Docker, so care must be taken around tests impacting each other
* Slow: They do I/O so they are slow, do not run them unless getting feedback for that middleware or store
* Fragile: They may break due to I/O issues, docker container failures etc.

Everything not in Paramore.Brighter.Cor.Tests is an Automated Test

In some cases, you may get faster feedback by running a sample.

We continue to exercise these on a CI server, to check for breaks in libraries, Core changes etc. But be aware that if Red, that might not be the code, but environment. Some tests may have a Skip flag, indicating that they are Fragile, and they should indicate how to help them pass.

Be wary that some of these tests may be dubious production examples due to wrangling to automate. Look at the samples instead.


