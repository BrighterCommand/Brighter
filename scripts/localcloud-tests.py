#!/usr/bin/env python3
"""Replay native CI targets and opt-in Google client round trips."""
import json
import ipaddress
import os
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import urllib.parse
import urllib.request
import urllib.error
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
GCP = "tests/Paramore.Brighter.Gcp.Tests/Paramore.Brighter.Gcp.Tests.csproj"
SERVICES = ("gcs", "firestore")
# Checkout-relative source paths identify the existing fixtures selected by the lane.
DEDICATED_TARGETS = {
    "tests/Paramore.Brighter.Gcp.Tests/Transformers": "Category=GCS",
    "tests/Paramore.Brighter.Gcp.Tests/Firestore/Inbox":
        "FullyQualifiedName~Paramore.Brighter.Gcp.Tests.Firestore.Inbox.",
    "tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/Generated/Sync/When_Adding_A_Message_It_Should_Be_Stored_With_All_Properties.cs":
        "FullyQualifiedName~Paramore.Brighter.Gcp.Tests.Outbox.Firestore.Sync.WhenAddingAMessageItShouldBeStoredWithAllProperties",
    "tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/Generated/Async/When_Adding_A_Message_It_Should_Be_Stored_With_All_Properties_Async.cs":
        "FullyQualifiedName~Paramore.Brighter.Gcp.Tests.Outbox.Firestore.Async.WhenAddingAMessageItShouldBeStoredWithAllProperties",
}


def dedicated_selection(paths):
    selected = tuple(paths) if paths else tuple(DEDICATED_TARGETS)
    if selected != tuple(DEDICATED_TARGETS):
        raise RuntimeError("Dedicated lane must name all existing fixture paths in manifest order.")
    for path in selected:
        if not (ROOT / path).exists():
            raise RuntimeError(f"Dedicated fixture path is missing: {path}")
    return "(" + "|".join(DEDICATED_TARGETS[path] for path in selected) + ")&Fragile!=CI"


def dotnet(*args):
    return ["bash", "scripts/localcloud-build.sh", "--dotnet", *args]


def request(gateway, path, project, body=None):
    headers = {"X-LocalCloud-Project": project, "X-LocalCloud-User": os.environ.get("LOCALCLOUD_USER", "local-developer")}
    if body is not None:
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(gateway + path, headers=headers,
                                 data=None if body is None else json.dumps(body).encode())
    with urllib.request.urlopen(req, timeout=30) as response:
        result = json.load(response)
    if result.get("error") is True:
        raise RuntimeError(f"LocalCloud {path} failed: {result}")
    return result


def endpoint_identity(endpoint, variable):
    rest = variable == "STORAGE_EMULATOR_HOST"
    address = urllib.parse.urlsplit(endpoint if rest else "//" + endpoint)
    try:
        port = address.port
    except ValueError as exc:
        raise RuntimeError(f"Invalid endpoint: {endpoint}") from exc
    if (not address.hostname or not port or address.username or address.password
            or address.query or address.fragment
            or (rest and (address.scheme != "http" or address.path not in ("", "/", "/storage/v1", "/storage/v1/")))
            or (not rest and ("://" in endpoint or address.path))):
        raise RuntimeError(f"Invalid plaintext SDK endpoint: {endpoint}")
    host = address.hostname.lower()
    if host == "localhost":
        host = "loopback"
    else:
        try:
            if ipaddress.ip_address(host).is_loopback:
                host = "loopback"
        except ValueError:
            pass
    return host, port


def published_endpoints(gateway, services):
    origin = urllib.parse.urlsplit(gateway)
    if origin.hostname not in ("127.0.0.1", "::1"):
        raise RuntimeError("SDK overrides require a concrete loopback gateway address.")
    addresses = {"0.0.0.0": "127.0.0.1", "127.0.0.1": "127.0.0.1",
                 "::": "::1", "::1": "::1"}
    try:
        ids = subprocess.run(["docker", "ps", "--quiet"], check=True, capture_output=True,
                             text=True, timeout=15).stdout.split()
        if not ids:
            raise RuntimeError("No container supplies this gateway's endpoint mapping.")
        containers = json.loads(subprocess.run(["docker", "inspect", *ids], check=True,
                                               capture_output=True, text=True, timeout=15).stdout)
    except (OSError, subprocess.SubprocessError, ValueError) as exc:
        raise RuntimeError("Cannot verify endpoint identity through this gateway's published mapping.") from exc

    port_maps = [container.get("NetworkSettings", {}).get("Ports") or {} for container in containers]
    # 5380 is the catalog's container listener, never a host/runtime port fallback.
    matches = [ports for ports in port_maps
               if any(addresses.get(binding["HostIp"]) == origin.hostname
                      and int(binding["HostPort"]) == origin.port
                      for binding in ports.get("5380/tcp") or [])]
    if len(matches) != 1:
        raise RuntimeError("Cannot uniquely establish the gateway container's endpoint identity.")
    result = {}
    for service in services:
        variable = service["env_var"]
        bindings = matches[0].get(f"{service['plaintext_port']}/tcp") or []
        published = {(addresses[binding["HostIp"]], int(binding["HostPort"]))
                     for binding in bindings if binding["HostIp"] in addresses}
        preferred = {item for item in published if item[0] == origin.hostname}
        choices = preferred or published
        if len(choices) != 1:
            raise RuntimeError(f"Missing unique published endpoint mapping for {service['service_id']}")
        host, port = choices.pop()
        host = f"[{host}]" if ":" in host else host
        endpoint = f"{host}:{port}"
        result[variable] = "http://" + endpoint if variable == "STORAGE_EMULATOR_HOST" else endpoint
    return result


def resolve_endpoints(gateway, services, advertised, ambient):
    selected = {}
    mapped = None
    for service in services:
        variable = service["env_var"]
        endpoint = advertised.get(variable)
        if not endpoint:
            raise RuntimeError(f"Missing advertised SDK endpoint for {service['service_id']}")
        exported_identity = endpoint_identity(endpoint, variable)
        override = ambient.get(variable)
        if override and endpoint_identity(override, variable) != exported_identity:
            if mapped is None:
                mapped = published_endpoints(gateway, services)
            if endpoint_identity(override, variable) != endpoint_identity(mapped[variable], variable):
                raise RuntimeError(f"SDK endpoint identity differs from LocalCloud: {variable}={override}")
            endpoint = mapped[variable]
        selected[variable] = endpoint
    return selected


def local_environment():
    gateway = os.environ.get("LOCALCLOUD_GATEWAY_URL")
    project = os.environ.get("GOOGLE_CLOUD_PROJECT")
    if not gateway or not project:
        raise RuntimeError("The run must supply LOCALCLOUD_GATEWAY_URL and GOOGLE_CLOUD_PROJECT.")
    origin = urllib.parse.urlsplit(gateway)
    if (origin.scheme != "http" or not origin.hostname or not origin.port
            or origin.path not in ("", "/") or origin.username or origin.password
            or origin.query or origin.fragment):
        raise RuntimeError("This test fixture requires the advertised plaintext LocalCloud origin.")
    gateway = gateway.rstrip("/")
    ready = request(gateway, "/readiness?services=" + ",".join(SERVICES), project)
    if ready.get("ready") is not True:
        raise RuntimeError(f"Required LocalCloud services are not ready: {ready}")
    print("Live gateway version: " + request(gateway, "/health", project).get("version", "unavailable"), flush=True)
    configs = request(gateway, "/services/config", project)
    iam = next(s for s in configs["services"] if s["service_id"] == "cloudiam")
    if iam["config"]["mode"]["effective"] != "open":
        raise RuntimeError("Anonymous emulator fixtures require open IAM; no runtime mode is changed.")
    advertised = request(gateway, "/env?format=json&project=" + urllib.parse.quote(project), project)
    services = ready["services"]
    if {service["service_id"] for service in services} != set(SERVICES):
        raise RuntimeError("Readiness did not identify the exact required service set.")
    selected = resolve_endpoints(gateway, services, advertised, os.environ)
    env = os.environ.copy()
    env.update(LOCALCLOUD_GATEWAY_URL=gateway, BRIGHTER_LOCALCLOUD="true")
    for service in services:
        variable = service["env_var"]
        endpoint = selected[variable]
        address = urllib.parse.urlsplit(endpoint if "://" in endpoint else "//" + endpoint)
        if not address.hostname or not address.port:
            raise RuntimeError(f"Endpoint must contain an explicit native port: {endpoint}")
        if "://" in endpoint and address.scheme != "http":
            raise RuntimeError(f"This fixture requires plaintext: {endpoint}")
        if service["protocol"] == "grpc" and "://" in endpoint:
            raise RuntimeError(f"Native gRPC requires a scheme-less authority: {endpoint}")
        with socket.create_connection((address.hostname, address.port), timeout=10):
            pass
        # Storage.V1 5.0.0 uses this value verbatim as BaseUri; its REST API prefix is required.
        if variable == "STORAGE_EMULATOR_HOST":
            base = endpoint.rstrip("/")
            env[variable] = base + "/" if base.endswith("/storage/v1") else base + "/storage/v1/"
        else:
            env[variable] = endpoint
        print(f"{service['service_id']}: {variable}={env[variable]}", flush=True)
    print(f"Run project: {project}; gateway: {gateway}", flush=True)
    return gateway, project, env


def test_command(project, selection=None, framework=None, collect=False):
    args = ["test", project, "--configuration", "Release", "--no-restore", "--no-build", "--verbosity", "quiet", "--logger", "GitHubActions"]
    if selection:
        args += ["--filter", selection]
    if framework:
        args += ["--framework", framework]
    if collect:
        args += ["--list-tests"]
    else:
        args += ["--logger", "console;verbosity=minimal", "--blame-hang-timeout", "15min"]
    return dotnet(*args)


def regression(collect=False):
    # The normal build job's complete broker-free test targets, with its exact filters.
    yield test_command("tests/Paramore.Brighter.Test.Generator.Tests/Paramore.Brighter.Test.Generator.Tests.csproj", framework="net10.0", collect=collect)
    gateways = ["AWS", "AWS.V4", "AzureServiceBus", "Gcp", "Kafka", "MQTT", "MSSQL",
                "PostgresSQL", "Redis", "RMQ.Async", "RMQ.Sync", "RocketMQ"]
    for name in gateways:
        project = f"tests/Paramore.Brighter.{name}.Tests/Paramore.Brighter.{name}.Tests.csproj"
        yield test_command(project, "FullyQualifiedName~GatewayChannelFactoryDeclarationTests", collect=collect)
    for name, selection in [
        ("RocketMQ", "Category=RocketMQBrokerFree"),
        ("AzureServiceBus", "FullyQualifiedName~AzureServiceBusProducerCreationRaceTests"),
        ("AzureServiceBus", "FullyQualifiedName~Paramore.Brighter.AzureServiceBus.Tests.Configuration."),
        ("Core", "Fragile!=CI"), ("Extensions", None), ("Transforms.Adaptors", None),
        ("InMemory", "Fragile!=CI"), ("Hangfire", "Fragile!=CI"),
        ("Quartz", "Fragile!=CI"), ("TickerQ", "Fragile!=CI"), ("Sqlite", "Fragile!=CI"),
    ]:
        project = f"tests/Paramore.Brighter.{name}.Tests/Paramore.Brighter.{name}.Tests.csproj"
        yield test_command(project, selection, collect=collect)
    # Retain every test for the covered services. Pub/Sub, its IAM/project dependencies
    # and Spanner retain their existing separate infrastructure/cloud CI lanes.
    yield test_command(GCP, "(Category=GCS|Category=Firestore)&Fragile!=CI", "net10.0", collect=collect)


def usage(gateway, project):
    result = request(gateway, "/usage?project=" + urllib.parse.quote(project), project)
    return {s["id"]: s["request_count"] for s in result["services"] if s["id"] in SERVICES}


def verify_browse(gateway, project, service):
    try:
        request(gateway, f"/browse/{service}?project=" + urllib.parse.quote(project), project)
    except urllib.error.HTTPError as error:
        with error:
            diagnostic = json.load(error)
        if (service != "firestore" or error.code != 403
                or diagnostic.get("error", {}).get("message")
                != "Validated server access required for Firestore controls"):
            raise
        print("Firestore browse returned 403: validated server access is required. "
              "The frozen service guide permits native readback evidence when browser enumeration is unavailable.", flush=True)


def result_counts(directory):
    results = list(Path(directory).glob("*.trx"))
    if not results:
        raise RuntimeError("VSTest produced no TRX results.")
    counts = dict(total=0, executed=0, passed=0, failed=0, skipped=0)
    for path in results:
        root = ET.parse(path).getroot()
        counters = root.find("./{*}ResultSummary/{*}Counters")
        if counters is None:
            raise RuntimeError(f"Missing native test counters: {path}")
        try:
            total, native_executed, successful, failed, skipped = (
                int(counters.attrib[name])
                for name in ("total", "executed", "passed", "failed", "notExecuted")
            )
        except (KeyError, ValueError) as exc:
            raise RuntimeError(f"Invalid native test counters: {path}") from exc
        if (min(total, native_executed, successful, failed, skipped) < 0
                or total < native_executed
                or native_executed != successful + failed):
            raise RuntimeError(f"Inconsistent native test counters: {path}")
        if skipped == 0 and total > native_executed:
            # VSTest's xUnit adapter leaves notExecuted=0 despite skipped results.
            outcomes = [result.get("outcome")
                        for result in root.findall("./{*}Results/{*}UnitTestResult")]
            if (len(outcomes) != total or outcomes.count("Passed") != successful
                    or outcomes.count("Failed") != failed
                    or outcomes.count("NotExecuted") != total - native_executed):
                raise RuntimeError(f"Inconsistent native test counters: {path}")
            skipped = outcomes.count("NotExecuted")
        if total != native_executed + skipped:
            raise RuntimeError(f"Inconsistent native test counters: {path}")
        if not native_executed:
            raise RuntimeError(f"VSTest produced no executed tests: {path}")
        for name, value in zip(counts, (total, native_executed, successful, failed, skipped)):
            counts[name] += value
    return counts


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ("dedicated", "full", "collect"):
        raise RuntimeError("Expected dedicated, full, or collect.")
    os.chdir(ROOT)
    mode = sys.argv[1]
    if mode != "dedicated" and len(sys.argv) != 2:
        raise RuntimeError("Fixture paths apply only to dedicated mode.")
    selection = dedicated_selection(sys.argv[2:]) if mode == "dedicated" else None
    gateway, project, env = local_environment()
    before = usage(gateway, project)
    commands = ([test_command(GCP, selection, "net10.0")]
                if mode == "dedicated" else list(regression(collect=mode == "collect")))
    if mode == "collect":
        for command in commands:
            print(json.dumps(command), flush=True)
            result = subprocess.run(command, cwd=ROOT, env=env, capture_output=True, text=True, timeout=900)
            if result.returncode:
                print(result.stdout[-8000:] + result.stderr[-8000:], flush=True)
                result.check_returncode()
            listed = sum(line.startswith("    ") for line in result.stdout.splitlines())
            if not listed:
                raise RuntimeError(f"Regression target collected no tests: {command}\n{result.stdout}")
            print(f"Listed {listed} test entries; executed 0.", flush=True)
        return
    counts = dict(total=0, executed=0, passed=0, failed=0, skipped=0)
    for command in commands:
        with tempfile.TemporaryDirectory(prefix="brighter-localcloud-results-") as results:
            command = [*command, "--logger", "trx;LogFilePrefix=localcloud", "--results-directory", results]
            print(json.dumps(command), flush=True)
            subprocess.run(command, cwd=ROOT, env=env, check=True, timeout=3600)
            native_counts = result_counts(results)
            for name in counts:
                counts[name] += native_counts[name]
    for service in SERVICES:
        verify_browse(gateway, project, service)
    after = usage(gateway, project)
    print("Native assertions completed; API request deltas: " + json.dumps(
        {s: after[s] - before[s] for s in before}), flush=True)
    print("The frozen Firestore guide does not promise metering; native readback is the behavioral evidence.", flush=True)
    print("Fixtures delete their resources through native clients; backend owns final run-project removal.", flush=True)
    print(f"Tests: {counts['passed']} passed, {counts['executed']} executed, "
          f"{counts['total']} total, {counts['skipped']} skipped, {counts['failed']} failed "
          "(native VSTest TRX)", flush=True)


if __name__ == "__main__":
    main()
