import gzip
import importlib.util
from pathlib import Path
import struct
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "verify_aspire_motion_acceptance.py"
SPEC = importlib.util.spec_from_file_location("verify_aspire_motion_acceptance", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(MODULE)


def _varint(value):
    encoded = bytearray()
    while value > 0x7f:
        encoded.append((value & 0x7f) | 0x80)
        value >>= 7
    encoded.append(value)
    return bytes(encoded)


def _integer_field(number, value):
    return _varint((number << 3) | 0) + _varint(value)


def _message_field(number, value):
    return _varint((number << 3) | 2) + _varint(len(value)) + value


def _float_field(number, value):
    return _varint((number << 3) | 5) + struct.pack("<f", value)


def _robot(x, y, robot_id=0):
    return _integer_field(2, robot_id) + _float_field(3, x) + _float_field(4, y)


def _vision_packet(yellow, blue):
    frame = b"".join(_message_field(6, item) for item in yellow)
    frame += b"".join(_message_field(7, item) for item in blue)
    return _message_field(1, frame)


def _referee_packet(command, counter):
    return _integer_field(4, command) + _integer_field(5, counter)


def _record(message_type, seconds, payload):
    timestamp_ns = (1_800_000_000 + seconds) * 1_000_000_000
    return (struct.pack(">qii", timestamp_ns, message_type, len(payload)) + payload)


def _archive(records):
    content = b"SSL_LOG_FILE" + struct.pack(">i", 1) + b"".join(records)
    return gzip.compress(content)


class MotionAcceptanceTests(unittest.TestCase):
    def test_both_teams_move_inside_the_same_normal_start_window(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertEqual("passed", result["status"])
        self.assertEqual("NORMAL_START", result["successfulWindow"]["command"])
        self.assertEqual(7, result["successfulWindow"]["commandCounter"])
        self.assertTrue(result["successfulWindow"]["craneMoved"])
        self.assertTrue(result["successfulWindow"]["tigersMoved"])

    def test_motion_in_separate_windows_does_not_satisfy_same_window(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.0, 0.0)])),
            _record(3, 3, _referee_packet(3, 8)),
            _record(4, 4, _vision_packet([_robot(0.2, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 5, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertNotEqual("passed", result["status"])
        self.assertFalse(result["sameWindowMotionObserved"])

    def test_static_team_and_preparatory_commands_do_not_pass(self):
        archive = _archive([
            _record(3, 0, _referee_packet(5, 7)),  # PREPARE_KICKOFF_BLUE
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.0, 0.0)])),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertNotEqual("passed", result["status"])
        self.assertFalse(result["sameWindowMotionObserved"])

    def test_incomplete_gzip_never_passes_even_if_motion_precedes_the_truncated_footer(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
        ])[:-8]

        result = MODULE.assess_archive_bytes(archive)

        self.assertNotEqual("passed", result["status"])
        self.assertTrue(result["partialEvidencePreserved"])


if __name__ == "__main__":
    unittest.main()
