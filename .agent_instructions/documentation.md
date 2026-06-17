# Documentation

- Update or add Documentation comments for all exports from assemblies.
  - To be clear exports: means all public and protected methods of public classes/structs/records/enums.
    - We do not add Documentation comments internal or private classes, or internal methods
  - Documentation are indicated by `///`
  - Documentation comments use XML
  - Documentation comments show up in Intellisense for developers. Bear this in mind when writing comments, as they should be helpful to a developer using the API but not so verbose that a developer would not choose to read it when using intellisense. Use `<remarks>` for notes on implementation or more detailed instructions.
  - They should also be helpful to a developer or LLM reading the code.
  - We provide some guidance on specific elements:
  - Use `<summary>` element to provide an overview of the purpose of the class or method. What behavior or state does it encapsulate? What would you use it for. Use `<paramref>` if you refer to parameters in the summary.
  - Use the `<param>` tag to describe parameters to a constructor or method.
    - Use `<see cref="">` to document the type of the parameter
    - Indicate what the parameter is for, what effect setting it has and if it is optional. If it is optional describe any default value and its impact.
    - The developer should be clear what values they need to provide for the parameter to control desired behavior.
  - Use `<returns>` to indicate the `<see cref="">` of the return type, optionality, and what the value represents.
  - Use `<typeparam>` to indicate the intent of a generic type parameter; document any constraints on the type.
  - Use `<exception>` to document any exceptions that the method call can throw.
  - Use `<value>` to document a property. Like a `<summary>` it should indicate purpose. Like a `<param>` or `<return>` it should use `<see cref="">` to indicate type.

```csharp
/// <summary>
/// Gets or sets the current status.
/// </summary>
/// <value>The current status as a <see cref="string"/>.</value>
public string Status { get; set; }
```

- Use `<remarks>` for advice to developers or LLMs working with the code directly. Include information on how the method is implemented where it is not obvious from the code or significant design decisions have been made. Consider what you would want to know if maintaining this method. Use `<see href="">` if you need to link to external documentation.  This can also be used for more detailed information than could be included in the `<summary>`.
  - Prefer to use good variable and method names to express intent, over inline comments.
    - Use the refactoring "Extract Method To Express Intent" to encapsulate code in a named method that explains intent, over using a comment.
    - Do not add comments for what may be easily inferred from the code.
    - In tests you may use //Arrange, //Act, //Assert.
    - If code has a complex algorithm or non-obvious implementation, prefer to use `/// <remarks>`
  - Example:

  ```csharp
  /// <summary>
  /// Sends a message to the specified recipient.
  /// </summary>
  /// <param name="recipient">The recipient's address.</param>
  /// <returns>The message ID.</returns>
  public string SendMessage(string recipient) { ... }
  ```  

- Documentation comments should be changed when APIs change.  
- Document new features and changes in the Docs repository of the BrighterCommand organization.

## Architecture Decision Records

**Recommended Tool**: Use the `/adr <title>` command (see [.claude/commands/adr/adr.md](../../.claude/commands/adr/adr.md)) to create properly formatted ADRs. This automates numbering, template application, and spec linking.

We are using Architecture Decision Records (ADR) to record important design decisions that we make. When you make a significant decicion about design, that would be useful as context to future reviewers, or explorers of the codebase, please record your design decision as an ADR.

Place ADRs in the [ADR directory](../adr)

The template for the ADR is in our [first ADR](../adr/0001-record-architecture-decisions.md).

An ADR should follow the naming convention [Sequence Number]-[Title].md

Scan the ADR directory for existing ADRs to determine the next [Sequence Number] to use.

Use dash-case (aka kebab-case) for the [Title] of the ADR.

## Writing tone for design documents

This guidance applies to ADRs, requirements specs, design specs, and any other long-lived document under `docs/` or `specs/`.

**Write for a future reader, not for the current conversation.** The audience is a contributor reading the document six months or two years from now to understand a design decision. They have no visibility into the chat that produced it.

**Refer to requirements and design artifacts, not to the participants in the authoring conversation.** Concretely:

- ❌ "at the user's direction" → ✅ "per requirement C3" or just state the decision
- ❌ "the user's feedback was singular ('an abstract base class')" → ✅ "requirement F5 specifies a single abstract base"
- ❌ "the user explicitly accepted this cost" → ✅ "the cost is accepted per requirement C1 (spec 0028 lands as PR review feedback, not greenfield work)"
- ❌ "if the user wants the interface anyway during review, the re-introduction is mechanical" → ✅ remove — review-loop asides do not survive past the review
- ❌ "Direct rendering from feedback item 5's framing" → ✅ "Aligns with requirement F4 (payload-mode validator role)"
- ❌ "the spec 0027 PROMPT suggested otherwise" → ✅ replace with the actual technical reason; PROMPT.md is ephemeral working state

**Do not quote conversational asides.** Phrases like *"Arguably it would have been better caught earlier"* or *"we could possibly use ..."* belong in chat transcripts and PR review threads, not in design documents that outlive them.

**Do not reference ephemeral working state.** PROMPT.md, current spec phase ("we are in the requirements phase"), conversation transcripts, and unresolved review back-and-forth are all transient. Either fold the substance into the document, or omit it.

**Trace decisions to durable artifacts.** Acceptable references include: requirement IDs (F1, NF2, C3), other ADRs, code locations, principles in `.agent_instructions/`, and external specifications. References to GitHub PRs and issues are acceptable as historical anchors but should not carry the design rationale — the rationale must live in the document itself.

**The rule of thumb:** if removing the sentence would leave the future reader less informed about *the design*, keep it. If it would only leave them less informed about *who said what when*, remove it.

## Licensing

- We add a license comment to every src file
- The license should be at the very top of each source file, before any using statements or code.
- We use the MIT license.
- You should add your name and the year, if it is a new file.
- You should put the license comment in a `#region Licence` block (note: British spelling, no space)
- An LLM should use the name and year of the contributor instructing the LLM
- As an example

```csharp
#region Licence

/* The MIT License (MIT)
Copyright © [Year] [Your Name] [Your Contact Email]

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
```