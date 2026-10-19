"""Opt-in contract tests for the LocalCloud test runner's endpoint selection."""
import importlib.util
import io
import json
from pathlib import Path
import unittest
from unittest.mock import patch
from urllib.error import HTTPError

spec = importlib.util.spec_from_file_location(
    "localcloud_tests", Path(__file__).with_name("localcloud-tests.py"))
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class EndpointIdentityTests(unittest.TestCase):
    def setUp(self):
        self.services = [
            {"service_id": "gcs", "env_var": "STORAGE_EMULATOR_HOST",
             "protocol": "rest", "plaintext_port": 5382},
            {"service_id": "firestore", "env_var": "FIRESTORE_EMULATOR_HOST",
             "protocol": "grpc", "plaintext_port": 5384},
        ]
        self.advertised = {"STORAGE_EMULATOR_HOST": "http://localhost:15382",
                           "FIRESTORE_EMULATOR_HOST": "localhost:15384"}
        self.gateway = "http://127.0.0.1:15380"

    def resolve(self, ambient=None):
        return runner.resolve_endpoints(self.gateway, self.services,
                                        self.advertised, ambient or {})

    def test_exported_sdk_mappings_take_precedence_over_native_ports_and_docker(self):
        with patch.object(runner.subprocess, "run", side_effect=AssertionError("Docker must not be queried")):
            selected = self.resolve(self.advertised)
        self.assertEqual(selected, self.advertised)

    def test_missing_ambient_values_use_authoritative_exports(self):
        self.assertEqual(self.resolve(), self.advertised)

    def test_application_environment_rejects_a_reachable_unrelated_endpoint(self):
        def response(gateway, path, project, body=None):
            if path.startswith("/readiness"):
                return {"ready": True, "services": self.services}
            if path == "/health":
                return {"version": "test"}
            if path == "/services/config":
                return {"services": [{"service_id": "cloudiam",
                                      "config": {"mode": {"effective": "open"}}}]}
            return self.advertised

        with patch.dict(runner.os.environ, {
            "LOCALCLOUD_GATEWAY_URL": self.gateway, "GOOGLE_CLOUD_PROJECT": "owned-test",
            "FIRESTORE_EMULATOR_HOST": "localhost:25384",
        }, clear=True), patch.object(runner, "request", side_effect=response), \
                patch.object(runner.subprocess, "run", side_effect=FileNotFoundError), \
                patch.object(runner.socket, "create_connection") as probe:
            with self.assertRaisesRegex(RuntimeError, "identity|mapping"):
                runner.local_environment()
            probe.assert_not_called()

    def test_loopback_aliases_and_storage_api_prefix_are_equivalent(self):
        selected = self.resolve({"STORAGE_EMULATOR_HOST": "http://127.0.0.1:15382/storage/v1/",
                                 "FIRESTORE_EMULATOR_HOST": "[::1]:15384"})
        self.assertEqual(selected, self.advertised)

    def test_unrelated_ambient_endpoint_is_rejected_before_any_socket_probe(self):
        cases = [("FIRESTORE_EMULATOR_HOST", "localhost:25384"),
                 ("FIRESTORE_EMULATOR_HOST", "unrelated.example:15384"),
                 ("FIRESTORE_EMULATOR_HOST", "firestore.googleapis.com:443"),
                 ("STORAGE_EMULATOR_HOST", "http://localhost:25382"),
                 ("STORAGE_EMULATOR_HOST", "http://storage.googleapis.com:80")]
        for variable, endpoint in cases:
            with self.subTest(endpoint=endpoint), \
                    patch.object(runner.subprocess, "run", side_effect=FileNotFoundError), \
                    patch.object(runner.socket, "create_connection") as probe:
                with self.assertRaisesRegex(RuntimeError, "identity|mapping"):
                    self.resolve({variable: endpoint})
                probe.assert_not_called()

    def test_missing_export_does_not_trust_an_ambient_endpoint(self):
        del self.advertised["FIRESTORE_EMULATOR_HOST"]
        with self.assertRaisesRegex(RuntimeError, "advertised"):
            self.resolve({"FIRESTORE_EMULATOR_HOST": "localhost:5384"})

    def test_invalid_endpoint_formats_fail_closed(self):
        invalid = {
            "STORAGE_EMULATOR_HOST": ("localhost:15382", "https://localhost:15382",
                                      "http://user@localhost:15382", "http://localhost:15382/other",
                                      "http://localhost:15382?target=x", "http://localhost:15382#x"),
            "FIRESTORE_EMULATOR_HOST": ("http://localhost:15384", "localhost:15384/path",
                                        "localhost", "user@localhost:15384"),
        }
        for variable, endpoints in invalid.items():
            for endpoint in endpoints:
                with self.subTest(endpoint=endpoint):
                    original = self.advertised[variable]
                    self.advertised[variable] = endpoint
                    with self.assertRaises(RuntimeError):
                        self.resolve()
                    self.advertised[variable] = original

    def test_only_the_container_publishing_this_gateway_can_supply_native_port_mappings(self):
        self.advertised = {"STORAGE_EMULATOR_HOST": "http://localhost:5382",
                           "FIRESTORE_EMULATOR_HOST": "localhost:5384"}
        containers = [
            {"NetworkSettings": {"Ports": {
                "5380/tcp": [{"HostIp": "127.0.0.1", "HostPort": "25380"}],
                "5384/tcp": [{"HostIp": "127.0.0.1", "HostPort": "25384"}],
            }}},
            {"NetworkSettings": {"Ports": {
                "5380/tcp": [{"HostIp": "127.0.0.1", "HostPort": "15380"}],
                "5382/tcp": [{"HostIp": "0.0.0.0", "HostPort": "15382"}],
                "5384/tcp": [{"HostIp": "127.0.0.1", "HostPort": "15384"}],
            }}},
        ]
        from subprocess import CompletedProcess
        replies = [CompletedProcess([], 0, "other selected\n"),
                   CompletedProcess([], 0, json.dumps(containers))]
        with patch.object(runner.subprocess, "run", side_effect=replies):
            selected = self.resolve({"STORAGE_EMULATOR_HOST": "http://localhost:15382",
                                     "FIRESTORE_EMULATOR_HOST": "localhost:15384"})
        self.assertEqual(selected["FIRESTORE_EMULATOR_HOST"], "127.0.0.1:15384")

    def test_published_mapping_does_not_authorize_an_unrelated_override(self):
        mapped = {"STORAGE_EMULATOR_HOST": "http://localhost:15382",
                  "FIRESTORE_EMULATOR_HOST": "localhost:15384"}
        with patch.object(runner, "published_endpoints", return_value=mapped):
            with self.assertRaisesRegex(RuntimeError, "identity"):
                self.resolve({"FIRESTORE_EMULATOR_HOST": "localhost:25384"})

    def docker_records(self, records):
        from subprocess import CompletedProcess
        return [CompletedProcess([], 0, "a" * 12 + "\n"),
                CompletedProcess([], 0, json.dumps(records))]

    def test_gateway_binding_in_another_address_family_is_not_its_container(self):
        records = [{"NetworkSettings": {"Ports": {
            "5380/tcp": [{"HostIp": "::", "HostPort": "15380"}],
            "5382/tcp": [{"HostIp": "::", "HostPort": "15382"}],
            "5384/tcp": [{"HostIp": "::", "HostPort": "15384"}],
        }}}]
        with patch.object(runner.subprocess, "run", side_effect=self.docker_records(records)):
            with self.assertRaisesRegex(RuntimeError, "gateway.*identity|gateway.*mapping"):
                runner.published_endpoints(self.gateway, self.services)

    def test_non_gateway_listener_cannot_identify_the_gateway_container(self):
        records = [{"NetworkSettings": {"Ports": {
            "8080/tcp": [{"HostIp": "127.0.0.1", "HostPort": "15380"}],
            "5382/tcp": [{"HostIp": "127.0.0.1", "HostPort": "15382"}],
            "5384/tcp": [{"HostIp": "127.0.0.1", "HostPort": "15384"}],
        }}}]
        with patch.object(runner.subprocess, "run", side_effect=self.docker_records(records)):
            with self.assertRaisesRegex(RuntimeError, "gateway.*identity|gateway.*mapping"):
                runner.published_endpoints(self.gateway, self.services)

    def test_sdk_bindings_in_another_family_use_their_concrete_published_addresses(self):
        records = [{"NetworkSettings": {"Ports": {
            "5380/tcp": [{"HostIp": "127.0.0.1", "HostPort": "15380"}],
            "5382/tcp": [{"HostIp": "::", "HostPort": "15382"}],
            "5384/tcp": [{"HostIp": "::1", "HostPort": "15384"}],
        }}}]
        with patch.object(runner.subprocess, "run", side_effect=self.docker_records(records)):
            selected = runner.published_endpoints(self.gateway, self.services)
        self.assertEqual(selected, {"STORAGE_EMULATOR_HOST": "http://[::1]:15382",
                                    "FIRESTORE_EMULATOR_HOST": "[::1]:15384"})

    def test_only_the_documented_firestore_browser_limitation_uses_native_evidence(self):
        message = "Validated server access required for Firestore controls"
        cases = [("firestore", 403, message, True), ("gcs", 403, message, False),
                 ("firestore", 500, message, False), ("firestore", 403, "IAM denied", False)]
        for service, code, diagnostic, allowed in cases:
            with self.subTest(service=service, code=code, diagnostic=diagnostic):
                error = HTTPError("http://localhost/browse/" + service, code, "Denied", {},
                                  io.BytesIO(json.dumps({"error": {"message": diagnostic}}).encode()))
                with patch.object(runner, "request", side_effect=error):
                    if allowed:
                        runner.verify_browse(self.gateway, "owned-test", service)
                    else:
                        with self.assertRaises(HTTPError):
                            runner.verify_browse(self.gateway, "owned-test", service)
                error.close()


if __name__ == "__main__":
    unittest.main()
