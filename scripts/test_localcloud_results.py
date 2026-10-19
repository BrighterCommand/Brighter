"""Native TRX accounting must distinguish discovered, executed and skipped cases."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location(
    "localcloud_tests", Path(__file__).with_name("localcloud-tests.py"))
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class NativeResultTests(unittest.TestCase):
    def counts(self, *reports, outcomes=None):
        with tempfile.TemporaryDirectory() as directory:
            for index, counters in enumerate(reports):
                root = ET.Element("TestRun", xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
                summary = ET.SubElement(root, "ResultSummary")
                ET.SubElement(summary, "Counters", {k: str(v) for k, v in counters.items()})
                if outcomes is not None:
                    results = ET.SubElement(root, "Results")
                    for outcome in outcomes[index]:
                        ET.SubElement(results, "UnitTestResult", outcome=outcome)
                ET.ElementTree(root).write(Path(directory) / f"framework-{index}.trx")
            return runner.result_counts(directory)

    def test_partial_skips_are_not_counted_as_execution(self):
        self.assertEqual(self.counts(dict(total=10, executed=8, passed=8, failed=0, notExecuted=2)),
                         dict(total=10, executed=8, passed=8, failed=0, skipped=2))

    def test_framework_results_preserve_each_native_counter(self):
        self.assertEqual(self.counts(
            dict(total=5, executed=4, passed=3, failed=1, notExecuted=1),
            dict(total=7, executed=5, passed=5, failed=0, notExecuted=2)),
            dict(total=12, executed=9, passed=8, failed=1, skipped=3))

    def test_vstest_zero_not_executed_counter_uses_native_skipped_results(self):
        # Microsoft.NET.Test.Sdk 18.10.1 leaves notExecuted=0 for xUnit skips.
        self.assertEqual(self.counts(
            dict(total=3, executed=1, passed=1, failed=0, notExecuted=0),
            dict(total=3, executed=1, passed=1, failed=0, notExecuted=0),
            outcomes=[["Passed", "NotExecuted", "NotExecuted"]] * 2),
            dict(total=6, executed=2, passed=2, failed=0, skipped=4))

    def test_vstest_all_skipped_native_results_are_still_rejected(self):
        with self.assertRaisesRegex(RuntimeError, "no executed tests"):
            self.counts(dict(total=2, executed=0, passed=0, failed=0, notExecuted=0),
                        outcomes=[["NotExecuted", "NotExecuted"]])

    def test_zero_skip_counter_requires_consistent_native_outcomes(self):
        counters = dict(total=3, executed=1, passed=1, failed=0, notExecuted=0)
        for outcomes in [None, [[]], [["Passed", "NotExecuted"]],
                         [["Failed", "NotExecuted", "NotExecuted"]],
                         [["Passed", "NotExecuted", "Aborted"]]]:
            with self.subTest(outcomes=outcomes), self.assertRaisesRegex(RuntimeError, "native test counters"):
                self.counts(counters, outcomes=outcomes)

    def test_all_skipped_execution_is_rejected(self):
        with self.assertRaisesRegex(RuntimeError, "no tests|no executed tests"):
            self.counts(dict(total=4, executed=0, passed=0, failed=0, notExecuted=4))

    def test_an_all_skipped_framework_cannot_hide_behind_another_framework(self):
        with self.assertRaisesRegex(RuntimeError, "no tests|no executed tests"):
            self.counts(dict(total=4, executed=0, passed=0, failed=0, notExecuted=4),
                        dict(total=5, executed=5, passed=5, failed=0, notExecuted=0))

    def test_empty_selection_is_rejected(self):
        with self.assertRaisesRegex(RuntimeError, "no tests|no executed tests"):
            self.counts(dict(total=0, executed=0, passed=0, failed=0, notExecuted=0))

    def test_missing_native_results_are_rejected(self):
        with self.assertRaisesRegex(RuntimeError, "no TRX"):
            self.counts()

    def test_inconsistent_or_invalid_native_counters_are_rejected(self):
        valid = dict(total=6, executed=4, passed=4, failed=0, notExecuted=2)
        cases = [dict(total=3), dict(executed=5), dict(passed=5), dict(failed=1),
                 dict(notExecuted=1), dict(notExecuted=3), dict(executed=-1),
                 dict(total=-1), dict(passed=-1), dict(failed=-1),
                 dict(notExecuted=-1), dict(executed="not-an-integer")]
        for changes in cases:
            with self.subTest(changes=changes), self.assertRaisesRegex(RuntimeError, "native test counters"):
                self.counts(valid | changes)
        for missing in valid:
            with self.subTest(missing=missing), self.assertRaisesRegex(RuntimeError, "native test counters"):
                self.counts({k: v for k, v in valid.items() if k != missing})


if __name__ == "__main__":
    unittest.main()
