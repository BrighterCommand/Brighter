# 43. Optional Mutual TLS (mTLS) Support for RabbitMQ Messaging Gateway

## Status

Proposed (Draft PR for discussion)

## Context

Brighter supports RabbitMQ as a messaging transport via dedicated messaging gateway assemblies. Today, RabbitMQ connections may be established either without TLS or using server-side TLS, depending on deployment configuration. However, Brighter does not currently offer first-class support for **mutual TLS (mTLS)**, where both client and server authenticate each other using X.509 certificates.

mTLS is increasingly required in regulated, zero-trust, and defence-in-depth environments, where network-level trust is insufficient and explicit client authentication is mandated. Users integrating Brighter into such environments currently need to rely on external tooling, custom forks, or unsupported configuration paths to achieve this.

Introducing mTLS support affects:
- connection semantics for RabbitMQ gateways
- user-facing configuration surfaces
- security posture and operational expectations
- testing strategy (particularly around transport-level behaviour)

Under Brighter’s contribution guidelines, changes that introduce new capabilities or alter connection semantics are expected to be documented via an Architecture Decision Record (ADR) prior to implementation.

## Decision


Brighter will introduce **optional, opt-in mutual TLS (mTLS) support** for RabbitMQ messaging gateways, with the following constraints:

1. mTLS support is **fully optional** and **disabled by default**.
2. Existing non-TLS and TLS-only configurations will continue to function unchanged.
3. mTLS configuration will be expressed explicitly via RabbitMQ gateway configuration objects.
4. Support will be implemented with **parity across synchronous and asynchronous RabbitMQ gateways**.
5. No changes will be made to existing message wire formats, headers, or CloudEvents propagation.

The primary mechanism for enabling mTLS will be the ability to supply an `X509Certificate2` (and associated trust configuration) to the RabbitMQ connection factory in a controlled, backwards-compatible manner.

## Rationale

This decision is driven by several guiding principles already present in Brighter’s architecture:

- **Backwards compatibility**: Existing users must not experience behavioural changes unless they explicitly opt in.
- **Optionality**: Security features should be paid for only by users who need them.
- **Parity**: Brighter avoids partial implementations that fragment behaviour across transports or sync/async models.
- **Explicitness**: Security configuration should be intentional and visible, not implicit or inferred.

mTLS is best implemented at the messaging gateway layer, where transport-specific connection concerns already reside. This avoids leaking transport details into core assemblies and aligns with Brighter’s existing separation of concerns.

## Alternatives Considered

### 1. Implicit mTLS via environment or OS certificate stores

This approach was rejected for the initial implementation because:
- it introduces platform-specific behaviour
- it obscures configuration intent
- it complicates testing and reproducibility

Certificate store integration may be considered in a future ADR if sufficient demand and precedent emerge.

### 2. Mandatory mTLS for all RabbitMQ connections

Rejected because it:
- breaks existing deployments
- violates Brighter’s optionality principle
- forces users to adopt security requirements they may not need

### 3. External sidecar or proxy-based mTLS only

While viable in some deployments, this approach does not provide a first-class Brighter API and pushes critical security configuration outside the application boundary.

## Consequences

### Positive

- Enables Brighter adoption in zero-trust and regulated environments
- Provides a clear, supported path for RabbitMQ mTLS configuration
- Preserves backwards compatibility and existing behaviour
- Aligns with Brighter’s architectural principles and contributor guidelines

### Negative

- Increases configuration surface area for RabbitMQ gateways
- Requires careful testing across sync/async variants
- Introduces additional security-related documentation and support burden

## Testing Strategy

The implementation will be supported by:

- **Unit tests** verifying correct propagation of mTLS configuration into the RabbitMQ connection factory
- **Integration tests** (optionally gated via environment flags) exercising real RabbitMQ brokers configured with mTLS
- Explicit tests ensuring:
  - non-TLS and TLS-only behaviour remains unchanged
  - mTLS failures surface clear, actionable errors
  - CloudEvents headers and tracing context are preserved under mTLS

## Non-Goals

This ADR explicitly does **not** cover:

- automatic certificate rotation
- dynamic trust store reloading
- platform-specific certificate store integration
- changes to message serialization, headers, or wire contracts

These may be revisited in future ADRs if requirements evolve.

## References

- Brighter Contributing Guide (`Contributing.md`)
- Existing Architecture Decision Records under `docs/adr`
- Issue #3902: RabbitMQ Mutual TLS support (https://github.com/BrighterCommand/Brighter/issues/3902)
- PATH-210X extracted Critical Review Guidelines for Brighter

---

*This ADR intentionally precedes any implementation work in order to align on scope, constraints, and non-goals before code is written.*

