# Read-back verification; no credentials or HTML bodies are printed.
import argparse,subprocess,json,hashlib,tempfile
from pathlib import Path
parser=argparse.ArgumentParser();parser.add_argument('plan');args=parser.parse_args()
root=Path(args.plan).resolve().parent;plan=json.loads(Path(args.plan).read_text(encoding='utf-8'))
receipt=json.loads((root/'rollout.json').read_text(encoding='utf-8'))
assert plan['uploaded'] and receipt['complete']
assert plan['bucket']=='klass-vr-file' and plan['profile']=='klever-iam'
def aws(*parts):return json.loads(subprocess.check_output(['aws',*parts,'--profile','klever-iam','--output','json'],text=True,encoding='utf-8'))
assert aws('sts','get-caller-identity')['Account']=='868542477648'
bridge_hash=hashlib.sha256((root/plan['bridge']).read_bytes()).hexdigest()
with tempfile.TemporaryDirectory(prefix='klever-pause-readback-') as directory:
 file=Path(directory)/'object'
 for row in plan['entries']:
  metadata=aws('s3api','get-object','--bucket',plan['bucket'],'--key',row['key'],str(file))
  assert hashlib.sha256(file.read_bytes()).hexdigest()==row['patchedSha256']
  assert metadata.get('CacheControl')=='no-cache,max-age=0,must-revalidate'
  assert plan['bridge'].encode() in file.read_bytes()
  aws('s3api','get-object','--bucket',plan['bucket'],'--key',row['bridgeKey'],str(file))
  assert hashlib.sha256(file.read_bytes()).hexdigest()==bridge_hash
  aws('s3api','get-object','--bucket',plan['bucket'],'--key',receipt['backupPrefix']+row['key'],str(file))
  assert hashlib.sha256(file.read_bytes()).hexdigest()==row['originalSha256']
summary={'pagesVerified':len(plan['entries']),'bridgeCopiesVerified':len(plan['entries']),'originalBackupsVerified':len(plan['entries']),'bridgeSha256':bridge_hash,'backupPrefix':receipt['backupPrefix'],'unityBuildDeployed':False}
(root/'verification.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(json.dumps(summary))
