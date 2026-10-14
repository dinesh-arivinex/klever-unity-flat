# Read-only AWS preparation; never uploads or modifies bucket objects.
import subprocess,json,tempfile,hashlib,re
from pathlib import Path
PROFILE='klever-iam'
BUCKET='klass-vr-file'
def aws(*args):
 return json.loads(subprocess.check_output(['aws',*args,'--profile',PROFILE,'--output','json'],text=True,encoding='utf-8'))
assert aws('sts','get-caller-identity')['Account']=='868542477648'
root=Path(tempfile.mkdtemp(prefix='klever-simulation-pause-'))
bridge=Path('Assets/StreamingAssets/simulation-session-bridge.js').read_bytes()
version=hashlib.sha256(bridge).hexdigest()[:12]
bridge_name='klever-session-bridge.'+version+'.js'
(root/bridge_name).write_bytes(bridge)
folders=aws('s3api','list-objects-v2','--bucket',BUCKET,'--prefix','klass-simulations/','--delimiter','/').get('CommonPrefixes',[])
entries=[]
for folder in folders:
 prefix=folder['Prefix']
 objects=aws('s3api','list-objects-v2','--bucket',BUCKET,'--prefix',prefix,'--delimiter','/').get('Contents',[])
 for obj in objects:
  key=obj['Key']
  if not key.lower().endswith(('.html','.htm')):continue
  index=len(entries)
  original=root/(str(index)+'.original.html')
  metadata=aws('s3api','get-object','--bucket',BUCKET,'--key',key,str(original))
  raw=original.read_bytes()
  text=raw.decode('utf-8-sig')
  if 'klever:session-control' in text or 'klever-session-bridge.' in text:continue
  match=re.search(r'<head(?:\s[^>]*)?>',text,re.I)
  if not match:continue
  tag='\n<script src="./'+bridge_name+'"></script>\n'
  updated=(text[:match.end()]+tag+text[match.end():]).encode('utf-8')
  (root/(str(index)+'.patched.html')).write_bytes(updated)
  engine='createjs' if 'createjs' in text else 'unity' if 'createUnityInstance' in text or 'UnityLoader' in text else 'construct' if 'c3runtime' in text else 'other'
  entries.append({'key':key,'bridgeKey':prefix+bridge_name,'index':index,'etag':metadata['ETag'],'originalSha256':hashlib.sha256(raw).hexdigest(),'patchedSha256':hashlib.sha256(updated).hexdigest(),'engine':engine,'metadata':metadata})
manifest={'bucket':BUCKET,'profile':PROFILE,'bridge':bridge_name,'entries':entries,'uploaded':False}
(root/'plan.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
summary={'preparedFolder':str(root),'pages':len(entries),'engines':{engine:sum(row['engine']==engine for row in entries) for engine in sorted(set(row['engine'] for row in entries))},'uploaded':False}
print(json.dumps(summary))
