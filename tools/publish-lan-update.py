"""Publish a signed Windows x64 update to the home LAN server."""
import argparse
import base64
import getpass
import hashlib
import json
from pathlib import Path
import re
import zipfile

import paramiko
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("publish_dir", type=Path)
parser.add_argument("--key", type=Path, required=True)
parser.add_argument("--notes", required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
revision = int(re.search(r"const int Revision = (\d+)", (root / "Plain Craft Launcher 2/Modules/LanUpdate.cs").read_text(encoding="utf-8-sig"))[1])
assert (args.publish_dir / "PCL Ap.exe").is_file(), "Missing launcher"
archive = args.publish_dir.parent / f"PCL-Ap-lan-{revision}.zip"
with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(args.publish_dir / "PCL Ap.exe", "PCL Ap.exe")
    for file in (args.publish_dir / "UI").rglob("*"):
        if file.is_file():
            z.write(file, file.relative_to(args.publish_dir).as_posix())
payload = json.dumps({"Revision": revision, "Url": f"http://192.168.5.7:19997/{archive.name}",
                      "Sha256": hashlib.sha256(archive.read_bytes()).hexdigest(), "Notes": args.notes}, ensure_ascii=False).encode()
key = serialization.load_pem_private_key(args.key.read_bytes(), None)
source = (root / "Plain Craft Launcher 2/Modules/LanUpdate.cs").read_text(encoding="utf-8-sig")
public_pem = key.public_key().public_bytes(serialization.Encoding.PEM, serialization.PublicFormat.SubjectPublicKeyInfo).decode().strip()
assert public_pem in source, "Signing key does not match the launcher's trusted public key"
envelope = json.dumps({"Payload": base64.b64encode(payload).decode(),
                       "Signature": base64.b64encode(key.sign(payload, padding.PKCS1v15(), hashes.SHA256())).decode()})
client = paramiko.SSHClient()
client.load_system_host_keys()
# Only accept the host enrolled in known_hosts during initial deployment.
client.connect("192.168.5.7", username="alecapu", password=getpass.getpass("SSH password: "), timeout=15)
try:
    sftp = client.open_sftp()
    remote = "/srv/pcl-ap-updates/"
    # New revisions must use unique package names. Publish the manifest last.
    try:
        sftp.stat(remote + archive.name)
    except FileNotFoundError:
        pass
    else:
        raise RuntimeError("Revision already published; increase LanUpdate.Revision and rebuild.")
    sftp.put(str(archive), remote + archive.name + ".tmp")
    sftp.rename(remote + archive.name + ".tmp", remote + archive.name)
    with sftp.file(remote + "latest.json.tmp", "w") as f:
        f.write(envelope)
    sftp.posix_rename(remote + "latest.json.tmp", remote + "latest.json")
    with sftp.file(remote + "index.html", "w") as f:
        f.write(f'<!doctype html><meta charset="utf-8"><title>PCL Ap</title><h1>PCL Ap 内网更新</h1><p>内网版本 {revision}</p><a href="{archive.name}">下载 Windows x64 启动器</a>')
    print(f"Published revision {revision}: http://192.168.5.7:19997/")
finally:
    client.close()
