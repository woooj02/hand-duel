"""Show of Hands: reference implementation of the hand clip format, version 1.

This file is the tie-breaker when docs/contract.md and an implementation disagree.
It has no dependencies beyond the Python 3 standard library.

    python handclip.py make   <out-dir>     write the sample clips
    python handclip.py check  <clip.json>   validate one clip and print a summary
"""
import base64
import json
import math
import struct
import sys

VERSION = 1
FPS = 30
JOINTS = 26
MIN_FRAMES = 30
MAX_FRAMES = 300
FRAME_BYTES = 2 + 6 + 8 + JOINTS * 6 * 2  # flags, head position, head rotation, two hands = 328

FLAG_LEFT_TRACKED = 1
FLAG_RIGHT_TRACKED = 2
FLAG_LEFT_MITT = 4
FLAG_RIGHT_MITT = 8

# OpenXR hand joint order.
JOINT_NAMES = [
    "palm", "wrist",
    "thumb_metacarpal", "thumb_proximal", "thumb_distal", "thumb_tip",
    "index_metacarpal", "index_proximal", "index_intermediate", "index_distal", "index_tip",
    "middle_metacarpal", "middle_proximal", "middle_intermediate", "middle_distal", "middle_tip",
    "ring_metacarpal", "ring_proximal", "ring_intermediate", "ring_distal", "ring_tip",
    "little_metacarpal", "little_proximal", "little_intermediate", "little_distal", "little_tip",
]


def _q(value_m):
    """Metres to int16 millimetres, clamped."""
    return max(-32768, min(32767, int(round(value_m * 1000.0))))


def _qr(component):
    return max(-32767, min(32767, int(round(component * 32767.0))))


def encode_frames(frames):
    """frames: list of dicts with flags, head_pos (3), head_rot (4, xyzw), left and right (26 x 3, metres)."""
    out = bytearray()
    for f in frames:
        out += struct.pack("<H", f["flags"])
        out += struct.pack("<3h", *[_q(v) for v in f["head_pos"]])
        out += struct.pack("<4h", *[_qr(v) for v in f["head_rot"]])
        for hand in ("left", "right"):
            for joint in f[hand]:
                out += struct.pack("<3h", *[_q(v) for v in joint])
    return base64.b64encode(bytes(out)).decode("ascii")


def decode_frames(data_b64, frame_count):
    raw = base64.b64decode(data_b64, validate=True)
    if len(raw) != frame_count * FRAME_BYTES:
        raise ValueError(f"data is {len(raw)} bytes, expected {frame_count * FRAME_BYTES}")
    frames = []
    for i in range(frame_count):
        o = i * FRAME_BYTES
        (flags,) = struct.unpack_from("<H", raw, o)
        head_pos = [v / 1000.0 for v in struct.unpack_from("<3h", raw, o + 2)]
        head_rot = [v / 32767.0 for v in struct.unpack_from("<4h", raw, o + 8)]
        hands = []
        p = o + 16
        for _ in range(2):
            joints = []
            for _ in range(JOINTS):
                joints.append([v / 1000.0 for v in struct.unpack_from("<3h", raw, p)])
                p += 6
            hands.append(joints)
        frames.append({"flags": flags, "head_pos": head_pos, "head_rot": head_rot,
                       "left": hands[0], "right": hands[1]})
    return frames


def validate(clip):
    """Returns a list of problems. Empty means the clip is valid."""
    problems = []
    if clip.get("v") != VERSION:
        problems.append("v must be 1")
    if clip.get("fps") != FPS:
        problems.append("fps must be 30")
    n = clip.get("frames")
    if not isinstance(n, int) or n < MIN_FRAMES or n > MAX_FRAMES:
        problems.append("frames must be an integer from 30 to 300")
        return problems
    word = clip.get("word")
    if not isinstance(word, str) or not word or len(word) > 40:
        problems.append("word must be a word id of 1 to 40 characters")
    try:
        frames = decode_frames(clip.get("data", ""), n)
    except Exception as e:  # noqa: BLE001
        problems.append(f"data: {e}")
        return problems
    if not any(f["flags"] & (FLAG_LEFT_TRACKED | FLAG_RIGHT_TRACKED) for f in frames):
        problems.append("no frame has a tracked hand")
    for i, f in enumerate(frames):
        norm = math.sqrt(sum(c * c for c in f["head_rot"]))
        if abs(norm - 1.0) > 0.02:
            problems.append(f"frame {i}: head rotation is not a unit quaternion")
            break
    return problems


# ---------------------------------------------------------------------------------------------
# A small parametric hand, only for making test clips. Stage space: x = actor's right, y = up,
# z = the way the actor faces. Left-handed, metres. See docs/contract.md.
# ---------------------------------------------------------------------------------------------

def _add(a, b): return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]
def _mul(a, s): return [a[0] * s, a[1] * s, a[2] * s]
def _cross(a, b): return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def _norm(a):
    length = math.sqrt(sum(c * c for c in a)) or 1.0
    return [c / length for c in a]


def _rot(v, axis, angle):
    """Rodrigues rotation of v around a unit axis."""
    c, s = math.cos(angle), math.sin(angle)
    d = sum(v[i] * axis[i] for i in range(3))
    cr = _cross(axis, v)
    return [v[i] * c + cr[i] * s + axis[i] * d * (1 - c) for i in range(3)]


# Finger layout in the hand frame: (side toward thumb, forward toward fingertips), metres.
_FINGERS = {
    "index": ((0.015, 0.030), (0.025, 0.095), (0.040, 0.025, 0.022)),
    "middle": ((0.003, 0.030), (0.003, 0.098), (0.045, 0.028, 0.023)),
    "ring": ((-0.008, 0.030), (-0.017, 0.092), (0.040, 0.026, 0.022)),
    "little": ((-0.018, 0.030), (-0.035, 0.080), (0.032, 0.019, 0.020)),
}


def make_hand(wrist, forward, back, is_left, curls, thumb_curl=0.0, spread=0.0):
    """wrist: position. forward: wrist to fingertips. back: normal out of the back of the hand.
    curls: four values 0..1 for index, middle, ring, little. Returns 26 joints in OpenXR order."""
    f = _norm(forward)
    u = _norm(back)
    # Side points toward the thumb. With the palm down and fingers forward the right thumb points
    # to the actor's left.
    s = _norm(_cross(f, u))
    if is_left:
        s = _mul(s, -1.0)

    def at(side, fwd, up=0.0):
        return _add(wrist, _add(_mul(s, side), _add(_mul(f, fwd), _mul(u, up))))

    joints = [None] * JOINTS
    joints[1] = list(wrist)
    joints[0] = at(0.0, 0.055)
    # Thumb: splayed toward the side, curling toward the palm.
    t_dir = _norm(_add(_mul(s, 0.75), _mul(f, 0.65)))
    t_axis = _norm(_cross(t_dir, u))
    t_dir = _rot(t_dir, u, 0.0)
    p = at(0.022, 0.018)
    joints[2] = p
    lengths = (0.045, 0.032, 0.026)
    bend = 0.0
    for k in range(3):
        bend += thumb_curl * (0.5, 0.7, 0.9)[k]
        d = _rot(t_dir, t_axis, -bend)
        d = _add(d, _mul(u, -0.25 * thumb_curl))
        p = _add(p, _mul(_norm(d), lengths[k]))
        joints[3 + k] = p
    base = 6
    for i, name in enumerate(("index", "middle", "ring", "little")):
        meta, knuckle, lens = _FINGERS[name]
        joints[base] = at(meta[0], meta[1])
        fan = spread * (0.18, 0.0, -0.14, -0.3)[i]
        p = at(knuckle[0], knuckle[1])
        joints[base + 1] = p
        direction = _rot(f, u, fan if not is_left else -fan)
        axis = _norm(_cross(direction, u))
        if is_left:
            axis = _mul(axis, 1.0)
        bend = 0.0
        for k in range(3):
            bend += curls[i] * (1.35, 1.6, 1.1)[k]
            d = _rot(direction, s if not is_left else _mul(s, -1.0), 0.0)
            d = _add(_mul(direction, math.cos(bend)), _mul(u, -math.sin(bend)))
            p = _add(p, _mul(_norm(d), lens[k]))
            joints[base + 2 + k] = p
        base += 5
    return joints


def _quat_yaw_pitch(yaw, pitch):
    """Unity-style quaternion (x, y, z, w) for a yaw around y then a pitch around x."""
    cy, sy = math.cos(yaw / 2), math.sin(yaw / 2)
    cp, sp = math.cos(pitch / 2), math.sin(pitch / 2)
    return [cy * sp, sy * cp, -sy * sp, cy * cp]


REST_L = lambda: make_hand([-0.17, -0.42, 0.22], [0.15, 0.1, 1], [0, 1, 0], True, [0.35] * 4, 0.3)   # noqa: E731
REST_R = lambda: make_hand([0.17, -0.42, 0.22], [-0.15, 0.1, 1], [0, 1, 0], False, [0.35] * 4, 0.3)  # noqa: E731


def _frame(t, left, right, flags=FLAG_LEFT_TRACKED | FLAG_RIGHT_TRACKED, yaw=0.0, pitch=0.0):
    head = [0.004 * math.sin(t * 1.3), 0.003 * math.sin(t * 2.1), 0.0]
    return {"flags": flags, "head_pos": head, "head_rot": _quat_yaw_pitch(yaw, pitch),
            "left": left, "right": right}


def sample_wave(seconds=4.0):
    frames = []
    for i in range(int(seconds * FPS)):
        t = i / FPS
        swing = 0.45 * math.sin(t * 2 * math.pi * 1.6)
        fwd = _rot([0, 1, 0], [0, 0, 1], swing)
        right = make_hand([0.22, -0.12, 0.34], fwd, [0, 0, -1], False, [0.05] * 4, 0.0, 0.6)
        frames.append(_frame(t, REST_L(), right, yaw=0.05 * math.sin(t)))
    return frames


def sample_piano(seconds=8.0):
    frames = []
    for i in range(int(seconds * FPS)):
        t = i / FPS
        drift = 0.10 * math.sin(t * 0.9)
        def curls(phase):
            return [0.25 + 0.45 * max(0.0, math.sin(t * 9.0 + phase + k * 1.7)) for k in range(4)]
        left = make_hand([-0.14 + drift, -0.30, 0.32], [0, -0.1, 1], [0, 1, 0.1], True, curls(0.0), 0.2, 0.3)
        right = make_hand([0.14 + drift, -0.30, 0.32], [0, -0.1, 1], [0, 1, 0.1], False, curls(2.0), 0.2, 0.3)
        frames.append(_frame(t, left, right, pitch=0.25))
    return frames


def sample_rain(seconds=8.0):
    frames = []
    for i in range(int(seconds * FPS)):
        t = i / FPS
        fall = (t * 0.5) % 1.0
        y = 0.12 - 0.42 * fall
        def curls(phase):
            return [0.2 + 0.35 * (0.5 + 0.5 * math.sin(t * 14.0 + phase + k * 1.3)) for k in range(4)]
        left = make_hand([-0.16, y, 0.36], [0.1, -0.5, 1], [0, 1, 0.5], True, curls(0.0), 0.1, 0.5)
        right = make_hand([0.16, y, 0.36], [-0.1, -0.5, 1], [0, 1, 0.5], False, curls(1.0), 0.1, 0.5)
        frames.append(_frame(t, left, right, pitch=0.3 * fall - 0.1))
    return frames


def sample_knock(seconds=5.0):
    """The left hand is untracked for the middle of the clip, to test the tracked flags."""
    frames = []
    for i in range(int(seconds * FPS)):
        t = i / FPS
        beat = (t * 2.2) % 1.0
        push = 0.07 * max(0.0, math.sin(beat * math.pi)) if t % 2.3 < 1.4 else 0.0
        right = make_hand([0.10, -0.10, 0.30 + push], [0, 0.9, 0.4], [0, 0.3, -1], False, [1.0] * 4, 1.0)
        flags = FLAG_RIGHT_TRACKED | (FLAG_LEFT_TRACKED if not 1.5 < t < 3.5 else 0)
        frames.append(_frame(t, REST_L(), right, flags))
    return frames


def sample_drink(seconds=6.0):
    frames = []
    for i in range(int(seconds * FPS)):
        t = i / FPS
        lift = 0.5 - 0.5 * math.cos(min(1.0, (t % 3.0) / 1.2) * math.pi)
        if (t % 3.0) > 2.0:
            lift = 0.5 + 0.5 * math.cos(min(1.0, ((t % 3.0) - 2.0) / 0.9) * math.pi)
        wrist = [0.16 - 0.10 * lift, -0.30 + 0.20 * lift, 0.30 - 0.16 * lift]
        fwd = _rot([-0.3, 0.2, 1], [0, 0, 1], 0.0)
        back = _rot([1, 0.2, 0.2], [0, 0, 1], -0.9 * lift)
        right = make_hand(wrist, fwd, back, False, [0.55] * 4, 0.45)
        frames.append(_frame(t, REST_L(), right, pitch=-0.25 * lift))
    return frames


def sample_mitt(seconds=3.0):
    """A clip whose right hand carries the mitt flag from 1 s to 2 s."""
    frames = []
    for i in range(int(seconds * FPS)):
        t = i / FPS
        mitt = 1.0 <= t < 2.0
        curls = [1.0, 0.0, 1.0, 1.0] if mitt else [0.1] * 4
        right = make_hand([0.15, -0.15, 0.34], [0, 1, 0], [0, 0, -1], False, curls, 1.0 if mitt else 0.0)
        flags = FLAG_LEFT_TRACKED | FLAG_RIGHT_TRACKED | (FLAG_RIGHT_MITT if mitt else 0)
        frames.append(_frame(t, REST_L(), right, flags))
    return frames


SAMPLES = {
    "wave": ("wave", sample_wave),
    "piano": ("piano", sample_piano),
    "rain": ("rain", sample_rain),
    "knock": ("knocking", sample_knock),
    "drink": ("drinking", sample_drink),
    "mitt": ("wave", sample_mitt),
}


def make_clip(word, frames, glove="ember"):
    return {"v": VERSION, "word": word, "fps": FPS, "frames": len(frames), "glove": glove,
            "app": "sample", "device": "synthetic", "data": encode_frames(frames)}


def main(argv):
    if len(argv) == 3 and argv[1] == "make":
        for name, (word, build) in SAMPLES.items():
            clip = make_clip(word, build())
            problems = validate(clip)
            if problems:
                raise SystemExit(f"{name}: {problems}")
            path = f"{argv[2]}/{name}.clip.json"
            with open(path, "w", encoding="utf-8", newline="\n") as fh:
                json.dump(clip, fh, separators=(",", ":"))
            print(f"{path}: {clip['frames']} frames, {len(clip['data'])} base64 characters")
        return 0
    if len(argv) == 3 and argv[1] == "check":
        with open(argv[2], encoding="utf-8") as fh:
            clip = json.load(fh)
        problems = validate(clip)
        if problems:
            print("INVALID:", "; ".join(problems))
            return 1
        frames = decode_frames(clip["data"], clip["frames"])
        both = sum(1 for f in frames if f["flags"] & 3 == 3)
        print(f"valid: word={clip['word']} frames={clip['frames']} both-hands-tracked={both}")
        print("frame 0 right wrist (m):", [round(v, 3) for v in frames[0]["right"][1]])
        print("frame 0 right index tip (m):", [round(v, 3) for v in frames[0]["right"][10]])
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
