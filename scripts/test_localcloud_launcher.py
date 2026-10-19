"""Exercise the generated launcher's borrowed-runtime path without live resources."""
import importlib.util
import io
import json
import os
from pathlib import Path
from subprocess import CompletedProcess
from types import SimpleNamespace
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location(
    "localcloud_launcher", Path(__file__).with_name("localcloud-test.py"))
launcher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(launcher)


class BorrowedLauncherTests(unittest.TestCase):
    def launch(self, *, advertised=None, supplied=None, records=None, owned=False,
               allowed=True, gateway="http://127.0.0.1:15380"):
        native = {"STORAGE_EMULATOR_HOST": "http://localhost:5382",
                  "FIRESTORE_EMULATOR_HOST": "localhost:5384"}
        supplied = supplied or {"STORAGE_EMULATOR_HOST": "http://localhost:15382",
                                "FIRESTORE_EMULATOR_HOST": "localhost:15384"}
        mappings = {"5380/tcp": [{"HostIp": "127.0.0.1", "HostPort": "15380"}],
                    "5382/tcp": [{"HostIp": "0.0.0.0", "HostPort": "15382"}],
                    "5384/tcp": [{"HostIp": "127.0.0.1", "HostPort": "15384"}]}
        if records is None:
            records = [{"NetworkSettings": {"Ports": mappings}}]
        requests, docker_calls = [], []

        def request(req, timeout=None):
            requests.append((req.method, req.full_url))
            if "/readiness" in req.full_url:
                data = {"ready": True}
            elif "/services" in req.full_url:
                data = {"services": [{"id": s, "enabled": True}
                                     for s in ("gcs", "firestore", "pubsub")]}
            elif "/env?" in req.full_url:
                data = {"GOOGLE_CLOUD_PROJECT": req.full_url.split("project=")[1],
                        **(native if advertised is None else advertised)}
            else:
                data = {}
            return io.BytesIO(json.dumps(data).encode())

        def docker(*args, **kwargs):
            docker_calls.append(args)
            if args == ("ps", "-q"):
                return "a" * 12
            self.assertEqual(args, ("inspect", "a" * 12))
            return json.dumps(records)

        environment = {**supplied, "GOOGLE_CLOUD_PROJECT": "owned-run"}
        if owned:
            del environment["GOOGLE_CLOUD_PROJECT"]
        with patch.dict(os.environ, environment, clear=True), \
                patch.object(launcher.sys, "argv", ["localcloud-test.py", "--existing", gateway, "--skip-build"]), \
                patch.object(launcher, "build_opener", return_value=SimpleNamespace(open=request)), \
                patch.object(launcher, "docker", side_effect=docker), \
                patch.object(launcher.subprocess, "run", return_value=CompletedProcess([], 0)) as run:
            if allowed:
                self.assertEqual(launcher.main(), 0)
                run.assert_called_once()
                child = run.call_args.kwargs["env"]
                self.assertEqual(child["LOCALCLOUD_GATEWAY_URL"], gateway)
                expected = json.loads(launcher.TEST_SPEC_JSON)["dedicated_lane"]
                self.assertEqual(run.call_args.args[0], expected["command"])
            else:
                with self.assertRaisesRegex(RuntimeError, "mapping|endpoint"):
                    launcher.main()
                run.assert_not_called()
                child = None
        # Borrowed runtimes, additional services and supplied projects are retained.
        self.assertTrue(all(call[0] in {"ps", "inspect"} for call in docker_calls))
        deletions = [url for method, url in requests if method == "DELETE"]
        self.assertEqual(len(deletions), 1 if owned else 0)
        if owned and child:
            self.assertTrue(deletions[0].endswith("/projects/" + child["GOOGLE_CLOUD_PROJECT"]))
        return child, docker_calls

    def test_mapped_gcs_and_firestore_exports_reach_the_selected_child_command(self):
        child, _ = self.launch()
        self.assertEqual(child["STORAGE_EMULATOR_HOST"], "http://127.0.0.1:15382")
        self.assertEqual(child["FIRESTORE_EMULATOR_HOST"], "127.0.0.1:15384")

    def test_authoritative_mapped_exports_need_no_container_discovery(self):
        endpoints = {"STORAGE_EMULATOR_HOST": "http://localhost:15382",
                     "FIRESTORE_EMULATOR_HOST": "localhost:15384"}
        child, calls = self.launch(advertised=endpoints, supplied=endpoints)
        self.assertEqual({key: child[key] for key in endpoints}, endpoints)
        self.assertEqual(calls, [])

    def test_unrelated_gcs_or_firestore_override_is_rejected(self):
        for variable, bad in [("STORAGE_EMULATOR_HOST", "http://localhost:25382"),
                              ("FIRESTORE_EMULATOR_HOST", "localhost:25384"),
                              ("STORAGE_EMULATOR_HOST", "http://storage.googleapis.com:15382"),
                              ("FIRESTORE_EMULATOR_HOST", "other.invalid:15384")]:
            supplied = {"STORAGE_EMULATOR_HOST": "http://localhost:15382",
                        "FIRESTORE_EMULATOR_HOST": "localhost:15384", variable: bad}
            with self.subTest(variable=variable, bad=bad):
                self.launch(supplied=supplied, allowed=False)

    def test_a_different_container_cannot_authorize_sdk_overrides(self):
        self.launch(records=[{"NetworkSettings": {"Ports": {
            "5380/tcp": [{"HostIp": "127.0.0.1", "HostPort": "25380"}],
            "5382/tcp": [{"HostIp": "127.0.0.1", "HostPort": "15382"}],
            "5384/tcp": [{"HostIp": "127.0.0.1", "HostPort": "15384"}],
        }}}], allowed=False)

    def test_cross_family_gateway_binding_is_rejected(self):
        for gateway, wrong_family in [("http://127.0.0.1:15380", "::"),
                                      ("http://[::1]:15380", "0.0.0.0")]:
            with self.subTest(gateway=gateway):
                self.launch(gateway=gateway, records=[{"NetworkSettings": {"Ports": {
                    "5380/tcp": [{"HostIp": wrong_family, "HostPort": "15380"}],
                }}}], allowed=False)

    def test_cross_family_sdk_bindings_select_the_actual_ipv6_addresses(self):
        child, _ = self.launch(records=[{"NetworkSettings": {"Ports": {
            "5380/tcp": [{"HostIp": "127.0.0.1", "HostPort": "15380"}],
            "5382/tcp": [{"HostIp": "::", "HostPort": "15382"}],
            "5384/tcp": [{"HostIp": "::1", "HostPort": "15384"}],
        }}}])
        self.assertEqual(child["STORAGE_EMULATOR_HOST"], "http://[::1]:15382")
        self.assertEqual(child["FIRESTORE_EMULATOR_HOST"], "[::1]:15384")

    def test_only_launcher_created_projects_are_removed(self):
        self.launch(owned=True)


if __name__ == "__main__":
    unittest.main()
