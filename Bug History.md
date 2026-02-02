Bug History

Original Commit (76aeced5c - September 2025):
A bug fix for RabbitMQ requeuing inadvertently introduced a logic error. It changed:
if (!_headersToReset.Any(htr => htr.Equals(header.Key)))
    headers.Add(header.Key, header.Value);
to:
if (_headersToReset.Contains(header.Key))
{
    headers[header.Key] = header.Value;
}
The Problem:
This inverted the logic! The _headersToReset list contains headers like DELAY_MILLISECONDS,MESSAGE_TYPE, TOPIC, HANDLED_COUNT, DELIVERY_TAG, and CORRELATION_ID (lines 43-51).
The intent of AddUserDefinedHeaders is to copy user-defined headers from message.Header.Bag intothe headers dictionary, excluding the special headers that Brighter manages internally (those in_headersToReset).
Current Fix:
Your PR correctly adds back the NOT operator:
if (!_headersToReset.Contains(header.Key))
{
    headers[header.Key] = header.Value;
}
This restores the correct logic: "Only add the header if it's NOT in the _headersToReset list."
Summary: The sign was reversed to fix a bug introduced in commit 76aeced5c. The originalSeptember fix accidentally inverted the condition, causing only Brighter's internal headers to becopied instead of excluding them. Your change corrects this.
Am I correct in saying that if you look at the corresponding file in async that has the correct if
(!_headersToReset.Contains(header.Key)), and so the PR brings sync inline with async?

● Search(pattern: "**/RmqMessagePublisher.cs")
  ⎿  Found 2 files (ctrl+o to expand)

● Read(src/Paramore.Brighter.MessagingGateway.RMQ.Async/RmqMessagePublisher.cs)
  ⎿  Read 304 lines

● Yes, you are absolutely correct!

Looking at the async version at RmqMessagePublisher.cs:227-236, the AddUserDefinedHeaders methodhas:
private static void AddUserDefinedHeaders(Message message, Dictionary<string, object?> headers)
{
    message.Header.Bag.Each(header =>
    {
        if (!_headersToReset.Contains(header.Key))
        {
            headers[header.Key] = header.Value;
        }
    });
}
The async version has the correct logic with !_headersToReset.Contains(header.Key), meaning itexcludes the internal headers and includes only user-defined ones.
Your PR correctly fixes the sync version to match the async implementation. The September 2025bug fix (commit 76aeced5c) only affected the sync version, leaving the async and syncimplementations inconsistent. This PR restores parity between the two implementations.
So yes, this PR brings sync inline with async, which is the correct behavior.