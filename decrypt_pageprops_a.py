#!/usr/bin/env python3
import argparse
import base64
import hashlib
import json
import sys
from typing import Any, Optional, Tuple

# --- Optional local install support (pip --target ./vendor) ---
# If you install pycryptodome into ./vendor, this lets the script import it.
# You can change "vendor" to whatever folder you used.
import os
VENDOR_DIR = os.path.join(os.path.dirname(__file__), "vendor")
if os.path.isdir(VENDOR_DIR):
    sys.path.insert(0, VENDOR_DIR)

try:
    from Crypto.Cipher import AES
except ImportError as e:
    raise SystemExit(
        "Missing dependency: pycryptodome.\n\n"
        "Install in a local folder (recommended):\n"
        "  python -m pip install pycryptodome --target ./vendor\n\n"
        "Or install in a venv:\n"
        "  python -m venv .venv\n"
        "  .venv\\Scripts\\activate  (Windows)\n"
        "  pip install pycryptodome\n"
    ) from e


def b64decode_loose(s: str) -> bytes:
    """
    Base64 decode that tolerates missing '=' padding and urlsafe variants.
    Note: if len(data_chars) % 4 == 1, that's not valid base64 -> caller likely sliced wrong.
    """
    s = s.strip()
    s = s.replace("-", "+").replace("_", "/")

    # Add padding if needed (only valid for mod 2 or 3; mod 1 is invalid)
    mod = len(s) % 4
    if mod == 1:
        raise ValueError("Invalid base64 length (mod 4 == 1). Likely wrong blob split.")
    if mod in (2, 3):
        s += "=" * (4 - mod)

    return base64.b64decode(s)


def split_blob_guess(blob: str) -> Tuple[str, str]:
    """
    Frontend logic (from your bundle) is effectively:
      key_str = blob.substring(n) where n = len(blob) - 21
      enc_b64 = blob.substring(0, n)

    However, real-world blobs sometimes include an extra delimiter or edge cases.
    This tries a few plausible splits and picks the first that base64-decodes cleanly.
    """
    blob = blob.strip().strip('"').strip("'")

    # Try the expected split first: last 21 chars is key
    candidates = []
    L = len(blob)
    for key_len in (21, 22, 20, 23):  # small fuzz in case something shifted
        if L <= key_len + 10:
            continue
        key_str = blob[L - key_len :]
        enc_b64 = blob[: L - key_len]
        candidates.append((key_str, enc_b64))

    # Also try splitting on the LAST '=' as a delimiter (some blobs look like "...=KEY")
    # Only if it yields a plausible key length.
    last_eq = blob.rfind("=")
    if last_eq != -1 and last_eq < L - 5:
        possible_key = blob[last_eq + 1 :]
        possible_enc = blob[: last_eq + 1]
        if 18 <= len(possible_key) <= 30:
            candidates.insert(0, (possible_key, possible_enc))

    # Validate by attempting base64 decode of enc_b64
    errors = []
    for key_str, enc_b64 in candidates:
        try:
            _ = b64decode_loose(enc_b64)
            return key_str, enc_b64
        except Exception as e:
            errors.append((key_str[:8], len(enc_b64), str(e)))

    # If none worked, show a helpful diagnostic
    diag = "\n".join([f"- key~{k}..., enc_len={n}: {err}" for k, n, err in errors[:8]])
    raise ValueError("Could not split blob into (key_str, enc_b64). Attempts:\n" + diag)


def decrypt_blob(blob: str) -> Any:
    """
    Matches the JS implementation you have in the bundle:

      key = sha256(key_str)
      raw = base64(enc_b64)
      iv  = raw[:16]
      ct  = raw[16:]
      aes-256-ctr decrypt with iv as counter block
      plaintext is utf-8 JSON
    """
    key_str, enc_b64 = split_blob_guess(blob)

    key = hashlib.sha256(key_str.encode("utf-8")).digest()
    raw = b64decode_loose(enc_b64)

    if len(raw) < 17:
        raise ValueError(f"Decoded payload too short ({len(raw)} bytes).")

    iv = raw[:16]
    ct = raw[16:]

    # PyCryptodome CTR: treat the 16-byte IV as the initial counter value (big-endian)
    cipher = AES.new(key, AES.MODE_CTR, nonce=b"", initial_value=int.from_bytes(iv, "big"))
    pt = cipher.decrypt(ct)

    # Plaintext should be JSON
    text = pt.decode("utf-8", errors="strict")
    return json.loads(text)


def find_pageprops_a(data: Any) -> Optional[str]:
    """
    First try the standard Next.js-ish path: data["props"]["pageProps"]["a"] or data["pageProps"]["a"].
    If not found, do a recursive search for a dict containing key "pageProps" with an "a" inside.
    """
    # Common direct paths
    if isinstance(data, dict):
        # data.pageProps.a
        pp = data.get("pageProps")
        if isinstance(pp, dict) and isinstance(pp.get("a"), str):
            return pp["a"]

        # data.props.pageProps.a
        props = data.get("props")
        if isinstance(props, dict):
            pp2 = props.get("pageProps")
            if isinstance(pp2, dict) and isinstance(pp2.get("a"), str):
                return pp2["a"]

    # Recursive search
    def rec(node: Any) -> Optional[str]:
        if isinstance(node, dict):
            if "pageProps" in node and isinstance(node["pageProps"], dict):
                a = node["pageProps"].get("a")
                if isinstance(a, str):
                    return a
            for v in node.values():
                got = rec(v)
                if got is not None:
                    return got
        elif isinstance(node, list):
            for it in node:
                got = rec(it)
                if got is not None:
                    return got
        return None

    return rec(data)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("json_file", help="JSON file containing the response (e.g. meta.json)")
    ap.add_argument("-o", "--out", help="Write decrypted JSON to a file (optional)")
    args = ap.parse_args()

    with open(args.json_file, "r", encoding="utf-8") as f:
        data = json.load(f)

    blob = find_pageprops_a(data)
    if not blob:
        raise SystemExit("Could not find pageProps.a in the provided JSON.")

    decrypted = decrypt_blob(blob)

    out_text = json.dumps(decrypted, ensure_ascii=False, indent=2)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(out_text)
        print(f"OK: wrote decrypted JSON to {args.out}")
    else:
        print(out_text)


if __name__ == "__main__":
    main()
