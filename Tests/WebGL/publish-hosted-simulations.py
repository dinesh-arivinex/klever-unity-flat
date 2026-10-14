# Explicit, conditional S3 rollout; dry-run unless --apply. Original pages are backed up first.
import argparse,subprocess,json,hashlib,urllib.parse,datetime,uuid
from pathlib import Path
parser=argparse.ArgumentParser();parser.add_argument('plan');parser.add_argument('--apply',action='store_true');args=parser.parse_args()
root=Path(args.plan).resolve().parent;plan=json.loads(Path(args.plan).read_text(encoding='utf-8'))
assert plan['bucket']=='klass-vr-file' and plan['profile']=='klever-iam'
def aws(*parts):return json.loads(subprocess.check_output(['aws',*parts,'--profile','klever-iam','--output','json'],text=True,encoding='utf-8'))
assert aws('sts','get-caller-identity')['Account']=='868542477648'
assert not plan.get('uploaded')
bridge=root/plan['bridge'];assert bridge.is_file()
assert hashlib.sha256(bridge.read_bytes()).hexdigest()[:12] in plan['bridge']
for row in plan['entries']:
 assert row['key'].startswith('klass-simulations/') and row['key'].lower().endswith(('.html','.htm'))
 original=root/(str(row['index'])+'.original.html');patched=root/(str(row['index'])+'.patched.html')
 assert hashlib.sha256(original.read_bytes()).hexdigest()==row['originalSha256']
 assert hashlib.sha256(patched.read_bytes()).hexdigest()==row['patchedSha256']
 assert plan['bridge'].encode() in patched.read_bytes()
 current=aws('s3api','head-object','--bucket',plan['bucket'],'--key',row['key'])
 assert current['ETag']==row['etag'],'Hosted page changed after preparation; regenerate the plan.'
if not args.apply:
 print(json.dumps({'validatedPages':len(plan['entries']),'willBackUp':True,'writesPerformed':False}));raise SystemExit(0)
backup='maintenance-backups/simulation-pause/'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'-'+uuid.uuid4().hex[:8]+'/'
receipt={'backupPrefix':backup,'applied':[],'complete':False}
receipt_path=root/'rollout.json'
def save():receipt_path.write_text(json.dumps(receipt,indent=2),encoding='utf-8')
def metadata_options(row):
 metadata=row['metadata'];options=[]
 for name,flag in [('ContentType','--content-type'),('ContentEncoding','--content-encoding'),('ContentDisposition','--content-disposition'),('ContentLanguage','--content-language'),('WebsiteRedirectLocation','--website-redirect-location')]:
  if metadata.get(name):options.extend([flag,str(metadata[name])])
 if metadata.get('Metadata'):options.extend(['--metadata',json.dumps(metadata['Metadata'])])
 return options
# Back up every original before touching any entry page, guarded against concurrent changes.
for row in plan['entries']:
 source=plan['bucket']+'/'+row['key']
 aws('s3api','copy-object','--bucket',plan['bucket'],'--key',backup+row['key'],'--copy-source',source,'--copy-source-if-match',row['etag'])
save()
try:
 for row in plan['entries']:
  aws('s3api','put-object','--bucket',plan['bucket'],'--key',row['bridgeKey'],'--body',str(bridge),'--content-type','application/javascript','--cache-control','public,max-age=31536000,immutable')
  tags=aws('s3api','get-object-tagging','--bucket',plan['bucket'],'--key',row['key']).get('TagSet',[])
  tagging=['--tagging',urllib.parse.urlencode({tag['Key']:tag['Value'] for tag in tags})] if tags else []
  result=aws('s3api','put-object','--bucket',plan['bucket'],'--key',row['key'],'--body',str(root/(str(row['index'])+'.patched.html')),'--if-match',row['etag'],'--cache-control','no-cache,max-age=0,must-revalidate',*metadata_options(row),*tagging)
  receipt['applied'].append({'key':row['key'],'etag':result['ETag']});save()
 receipt['complete']=True;save();plan['uploaded']=True;Path(args.plan).write_text(json.dumps(plan,indent=2),encoding='utf-8')
 print(json.dumps({'uploadedPages':len(receipt['applied']),'backupPrefix':backup,'unityBuildDeployed':False}))
except Exception:
 # Conditional restoration never overwrites a newer edit by another operator.
 receipt['restored']=[];receipt['rollbackIncomplete']=[]
 for applied in reversed(receipt['applied']):
  row=next(row for row in plan['entries'] if row['key']==applied['key'])
  cache=['--cache-control',row['metadata']['CacheControl']] if row['metadata'].get('CacheControl') else []
  try:
   tags=aws('s3api','get-object-tagging','--bucket',plan['bucket'],'--key',backup+row['key']).get('TagSet',[])
   tagging=['--tagging',urllib.parse.urlencode({tag['Key']:tag['Value'] for tag in tags})] if tags else []
   aws('s3api','put-object','--bucket',plan['bucket'],'--key',row['key'],'--body',str(root/(str(row['index'])+'.original.html')),'--if-match',applied['etag'],*metadata_options(row),*cache,*tagging)
   receipt['restored'].append(row['key'])
  except Exception:receipt['rollbackIncomplete'].append(row['key'])
  save()
 receipt['rolledBack']=not receipt['rollbackIncomplete'];save();raise
