"""Run this repository's selected tests against an isolated LocalCloud container."""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
import time
import uuid
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import urlsplit, urlunsplit
from urllib.request import ProxyHandler, Request, build_opener

ROOT = Path(__file__).resolve().parents[1]
TEST_SPEC_JSON = r"""
{
  "image": "agentcloud/localcloud@sha256:88e85b86b294e1e4f71b8c63ec43de79d170946ff39556f0e2709f66aa0d368c",
  "services": [
    "firestore",
    "gcs"
  ],
  "gateway_port": 5380,
  "ports": [
    5380,
    5382,
    5384,
    5392
  ],
  "endpoint_variables": [
    "FIRESTORE_EMULATOR_HOST",
    "LOCALCLOUD_GATEWAY_URL",
    "STORAGE_EMULATOR_HOST"
  ],
  "project_variables": [
    "GOOGLE_CLOUD_PROJECT"
  ],
  "build": [
    {
      "cwd": ".",
      "command": [
        "bash",
        "scripts/localcloud-build.sh"
      ]
    },
    {
      "cwd": ".",
      "command": [
        "python3",
        "-B",
        "-m",
        "unittest",
        "discover",
        "-s",
        "scripts",
        "-p",
        "test_localcloud_*.py",
        "-v"
      ]
    },
    {
      "cwd": ".",
      "command": [
        "bash",
        "scripts/localcloud-build.sh",
        "--dotnet",
        "list",
        "tests/Paramore.Brighter.Gcp.Tests/Paramore.Brighter.Gcp.Tests.csproj",
        "package",
        "--include-transitive",
        "--no-restore"
      ]
    },
    {
      "cwd": ".",
      "command": [
        "bash",
        "scripts/localcloud-build.sh",
        "--dotnet",
        "test",
        "tests/Paramore.Brighter.Test.Generator.Tests/Paramore.Brighter.Test.Generator.Tests.csproj",
        "-c",
        "Release",
        "--no-restore",
        "--no-build",
        "-f",
        "net10.0",
        "--logger",
        "GitHubActions",
        "--verbosity",
        "quiet"
      ]
    },
    {
      "cwd": ".",
      "command": [
        "bash",
        "scripts/localcloud-build.sh",
        "--dotnet",
        "test",
        "tests/Paramore.Brighter.TickerQ.Tests/Paramore.Brighter.TickerQ.Tests.csproj",
        "--filter",
        "Fragile!=CI",
        "--configuration",
        "Release",
        "--no-restore",
        "--no-build",
        "--verbosity",
        "quiet",
        "--logger",
        "console;verbosity=minimal"
      ]
    },
    {
      "cwd": ".",
      "command": [
        "python3",
        "-B",
        "scripts/localcloud-tests.py",
        "collect"
      ]
    }
  ],
  "config": {
    "version": 1,
    "context": {
      "project": "repository-tests"
    },
    "host": {
      "docker_socket": false
    },
    "server": {
      "persistence": true,
      "auto_seed": false
    },
    "updater": {
      "enabled": false
    },
    "services": {
      "enabled": [
        "firestore",
        "gcs"
      ]
    }
  },
  "dedicated_lane": {
    "cwd": ".",
    "command": [
      "python3",
      "-B",
      "scripts/localcloud-tests.py",
      "dedicated",
      "tests/Paramore.Brighter.Gcp.Tests/Transformers",
      "tests/Paramore.Brighter.Gcp.Tests/Firestore/Inbox",
      "tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/Generated/Sync/When_Adding_A_Message_It_Should_Be_Stored_With_All_Properties.cs",
      "tests/Paramore.Brighter.Gcp.Tests/Outbox/Firestore/Generated/Async/When_Adding_A_Message_It_Should_Be_Stored_With_All_Properties_Async.cs"
    ]
  },
  "full_suite": {
    "cwd": ".",
    "command": [
      "python3",
      "-B",
      "scripts/localcloud-tests.py",
      "full"
    ]
  }
}
"""


def docker(*args: str, check: bool = True) -> str:
    result = subprocess.run(
        ["docker", *args], text=True, capture_output=True, check=False
    )
    if check and result.returncode:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip())
    return result.stdout.strip()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--full-suite",
        action="store_true",
        help="run the normal regression suite instead of the selected service tests",
    )
    parser.add_argument(
        "--existing",
        metavar="GATEWAY",
        help="borrow an already running LocalCloud instead of starting a container",
    )
    parser.add_argument(
        "--skip-build",
        action="store_true",
        help="dependencies/build were already prepared",
    )
    args = parser.parse_args()
    manifest = json.loads(TEST_SPEC_JSON)
    ident = "lc-tests-" + uuid.uuid4().hex[:12]
    container_created = False
    volume_created = False
    project_owned = False
    failed = True
    gateway = args.existing
    ports: dict[int, int] = {}
    supplied = os.environ.get("GOOGLE_CLOUD_PROJECT", "")
    project = supplied or "lc-tests-" + uuid.uuid4().hex[:12]
    if not re.fullmatch(r"[a-z][a-z0-9-]{0,29}", project):
        raise RuntimeError("Invalid LocalCloud project ID")
    opener = build_opener(ProxyHandler({}))

    def request(method: str, path: str, body: dict | None = None) -> object:
        payload = None if body is None else json.dumps(body).encode()
        req = Request(
            str(gateway).rstrip("/") + path,
            data=payload,
            method=method,
            headers={
                "Content-Type": "application/json",
                "X-LocalCloud-User": "repository-tests",
            },
        )
        with opener.open(req, timeout=30) as response:
            return json.load(response)

    borrowed_ports: dict[int, tuple[str, int]] | None = None

    def existing_port_mapping() -> dict[int, tuple[str, int]]:
        """Bind a supplied SDK override to the container serving this gateway."""
        selected = urlsplit(str(gateway))
        if selected.hostname not in {"127.0.0.1", "::1"}:
            raise RuntimeError(
                "SDK overrides require an explicit loopback gateway address"
            )
        addresses = {
            "127.0.0.1": "127.0.0.1",
            "0.0.0.0": "127.0.0.1",
            "::1": "::1",
            "::": "::1",
        }
        identifiers = docker("ps", "-q").split()
        if not identifiers or any(
            not re.fullmatch(r"[0-9a-f]{12,64}", item) for item in identifiers
        ):
            raise RuntimeError(
                "Cannot verify the existing LocalCloud container mapping"
            )
        matches = []
        gateway_port = selected.port or (443 if selected.scheme == "https" else 80)
        for info in json.loads(docker("inspect", *identifiers)):
            bindings = info.get("NetworkSettings", {}).get("Ports", {})
            if any(
                int(item["HostPort"]) == gateway_port
                and addresses.get(item.get("HostIp")) == selected.hostname
                for item in bindings.get(str(manifest["gateway_port"]) + "/tcp") or []
            ):
                matches.append(bindings)
        if len(matches) != 1:
            raise RuntimeError(
                "Cannot uniquely verify the existing LocalCloud gateway mapping"
            )
        result = {}
        for native, bindings in matches[0].items():
            if not native.endswith("/tcp"):
                continue
            published = {
                (addresses[item["HostIp"]], int(item["HostPort"]))
                for item in bindings or []
                if item.get("HostIp") in addresses
            }
            preferred = {item for item in published if item[0] == selected.hostname}
            choices = preferred or published
            if len(choices) == 1:
                result[int(native.split("/")[0])] = choices.pop()
        return result

    def endpoint_identity(value: str) -> tuple:
        is_url = "://" in value
        parsed = urlsplit(value if is_url else "//" + value)
        if (
            parsed.hostname not in {"localhost", "127.0.0.1", "::1", "0.0.0.0"}
            or parsed.port is None
            or parsed.port <= 0
            or parsed.username is not None
            or parsed.password is not None
            or parsed.query
            or parsed.fragment
            or (is_url and parsed.scheme not in {"http", "https"})
        ):
            raise RuntimeError(
                "LocalCloud SDK endpoint must be an explicit local endpoint"
            )
        return (parsed.scheme, parsed.port, parsed.path.rstrip("/"))

    def mapped_endpoint(value: str, supplied: str | None = None) -> str:
        nonlocal borrowed_ports
        identity = endpoint_identity(value)
        is_url = "://" in value
        parsed = urlsplit(value if is_url else "//" + value)
        port = parsed.port
        if args.existing:
            if not supplied or endpoint_identity(supplied) == identity:
                return value
            if borrowed_ports is None:
                borrowed_ports = existing_port_mapping()
            if port not in borrowed_ports or endpoint_identity(supplied) != (
                parsed.scheme,
                borrowed_ports[port][1],
                parsed.path.rstrip("/"),
            ):
                raise RuntimeError(
                    "Supplied SDK endpoint differs from the verified gateway mapping"
                )
            host, published_port = borrowed_ports[port]
            authority = (
                ("[" + host + "]" if ":" in host else host) + ":" + str(published_port)
            )
            return (
                urlunsplit((parsed.scheme, authority, parsed.path, "", ""))
                if is_url
                else authority
            )
        if (
            parsed.hostname not in {"localhost", "127.0.0.1", "0.0.0.0"}
            or port not in ports
        ):
            raise RuntimeError(
                "LocalCloud advertised an endpoint outside its published container ports"
            )
        authority = "127.0.0.1:" + str(ports[port])
        return (
            urlunsplit(
                (parsed.scheme, authority, parsed.path, parsed.query, parsed.fragment)
            )
            if is_url
            else authority
        )

    try:
        if gateway is None:
            docker("info", "--format", "{{.ServerVersion}}")
            docker(
                "volume", "create", "--label", "localcloud.test-owner=" + ident, ident
            )
            volume_created = True
            command = [
                "create",
                "--name",
                ident,
                "--label",
                "localcloud.test-owner=" + ident,
                "--memory",
                "4g",
                "-v",
                ident + ":/var/lib/localcloud",
            ]
            for port in manifest["ports"]:
                # Clients may discover native ports directly from the runtime.
                command += ["-p", f"127.0.0.1:{port}:{port}"]
            command.append(manifest["image"])
            docker(*command)
            container_created = True
            with tempfile.TemporaryDirectory(prefix="localcloud-tests-") as temporary:
                config_path = Path(temporary) / "config.json"
                config_path.write_text(json.dumps(manifest["config"]))
                docker(
                    "cp", str(config_path), ident + ":/etc/localcloud/localcloud.yaml"
                )
            docker("start", ident)
            info = json.loads(docker("inspect", ident))[0]
            ports = {
                int(port.split("/")[0]): int(bindings[0]["HostPort"])
                for port, bindings in info["NetworkSettings"]["Ports"].items()
                if bindings
            }
            gateway = "http://127.0.0.1:" + str(ports[manifest["gateway_port"]])
        deadline = time.monotonic() + 240
        while True:
            try:
                readiness = request("GET", "/readiness")
                if isinstance(readiness, dict) and readiness.get("ready") is True:
                    request("GET", "/projects")
                    break
            except (OSError, ValueError):
                pass
            if (
                container_created
                and docker("inspect", "--format", "{{.State.Running}}", ident) != "true"
            ):
                raise RuntimeError("LocalCloud container exited during startup")
            if time.monotonic() >= deadline:
                raise RuntimeError("LocalCloud did not become ready within 240 seconds")
            time.sleep(2)
        services = request("GET", "/services")
        rows = services.get("services", []) if isinstance(services, dict) else services
        if not isinstance(rows, list) or any(not isinstance(row, dict) for row in rows):
            raise RuntimeError("LocalCloud service listing must be a list of records")
        enabled = {
            s.get("service_id", s.get("id")) for s in rows if s.get("enabled") is True
        }
        required = set(manifest["services"])
        if not required.issubset(enabled) or (
            container_created and enabled != required
        ):
            raise RuntimeError(
                "LocalCloud enabled services differ from this repository's test contract: "
                + str(sorted(enabled))
            )
        try:
            request("POST", "/projects", {"project_id": project})
            project_owned = not supplied
        except HTTPError as exc:
            if exc.code != 409 or not supplied:
                raise
        advertised = request("GET", "/env?format=json&project=" + project)
        if (
            not isinstance(advertised, dict)
            or advertised.get("GOOGLE_CLOUD_PROJECT") != project
        ):
            raise RuntimeError("LocalCloud did not advertise the selected project")
        env = {
            k: v
            for k, v in os.environ.items()
            if not k.endswith("_EMULATOR_HOST")
            and k
            not in {
                "GOOGLE_APPLICATION_CREDENTIALS",
                "GOOGLE_CLOUD_PROJECT",
                "GCLOUD_PROJECT",
                "GCP_PROJECT",
                "GOROOT",
                "SPARK_HOME",
                "PYSPARK_PYTHON",
                "PYSPARK_DRIVER_PYTHON",
            }
        }
        env.update(
            {
                "GOOGLE_CLOUD_PROJECT": project,
                "LOCALCLOUD_GATEWAY_URL": str(gateway),
                "LOCALCLOUD_TEST": "1",
            }
        )
        for variable in manifest.get("project_variables", ["GOOGLE_CLOUD_PROJECT"]):
            if variable == "LOCALCLOUD_GATEWAY_URL":
                continue
            env[variable] = project
        for variable in manifest["endpoint_variables"]:
            if variable == "LOCALCLOUD_GATEWAY_URL":
                continue
            if not advertised.get(variable):
                raise RuntimeError("LocalCloud did not advertise " + variable)
            env[variable] = mapped_endpoint(
                str(advertised[variable]), os.environ.get(variable)
            )
        print(
            "LocalCloud:",
            gateway,
            "project:",
            project,
            "services:",
            ", ".join(manifest["services"]),
            flush=True,
        )
        phases = [] if args.skip_build else manifest["build"]
        phases = [
            *phases,
            manifest["full_suite"] if args.full_suite else manifest["dedicated_lane"],
        ]
        for phase in phases:
            cwd = (ROOT / phase["cwd"]).resolve()
            if not cwd.is_relative_to(ROOT):
                raise RuntimeError("Test working directory escapes the repository")
            print("Running:", " ".join(phase["command"]), flush=True)
            result = subprocess.run(phase["command"], cwd=cwd, env=env, check=False)
            if result.returncode:
                return result.returncode
        failed = False
        return 0
    finally:
        try:
            if project_owned:
                request("DELETE", "/projects/" + project)
        finally:
            if container_created:
                logs = docker("logs", "--tail", "100", ident, check=False)
                if failed:
                    print(logs, file=sys.stderr)
                try:
                    docker("rm", "-f", ident)
                finally:
                    if volume_created:
                        docker("volume", "rm", ident)
            elif volume_created:
                docker("volume", "rm", ident)


if __name__ == "__main__":
    sys.exit(main())
