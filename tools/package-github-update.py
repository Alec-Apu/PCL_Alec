"""Build signed assets, then upload them to a GitHub release with gh."""
import argparse,base64,hashlib,json,re,zipfile
from pathlib import Path
from cryptography.hazmat.primitives import hashes,serialization
from cryptography.hazmat.primitives.asymmetric import padding
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('publish_dir',type=Path);p.add_argument('--key',required=True,type=Path);p.add_argument('--tag',required=True);p.add_argument('--notes',required=True)
a=p.parse_args();root=Path(__file__).resolve().parents[1]
s=(root/'Plain Craft Launcher 2/Modules/LanUpdate.cs').read_text(encoding='utf-8-sig')
r=int(re.search(r'const int Revision = ([0-9]+)',s)[1])
k=serialization.load_pem_private_key(a.key.read_bytes(),None)
assert k.public_key().public_bytes(serialization.Encoding.PEM,serialization.PublicFormat.SubjectPublicKeyInfo).decode().strip() in s
assert (a.publish_dir/'PCL Ap.exe').is_file()
zpath=a.publish_dir.parent/f'PCL-Ap-update-{r}.zip'
with zipfile.ZipFile(zpath,'w',zipfile.ZIP_DEFLATED) as z:
 z.write(a.publish_dir/'PCL Ap.exe','PCL Ap.exe')
 for f in (a.publish_dir/'UI').rglob('*'):
  if f.is_file():z.write(f,f.relative_to(a.publish_dir).as_posix())
payload=json.dumps(dict(Revision=r,Url=f'https://github.com/Alec-Apu/PCL_Alec/releases/download/{a.tag}/{zpath.name}',Sha256=hashlib.sha256(zpath.read_bytes()).hexdigest(),Notes=a.notes),ensure_ascii=False).encode()
out=a.publish_dir.parent/'latest.json'
out.write_text(json.dumps(dict(Payload=base64.b64encode(payload).decode(),Signature=base64.b64encode(k.sign(payload,padding.PKCS1v15(),hashes.SHA256())).decode())),encoding='utf-8')
print(zpath);print(out)

