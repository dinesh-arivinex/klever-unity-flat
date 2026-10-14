# Uses the already installed .NET SDK/Roslyn. No package restore or Unity claim.
import subprocess,tempfile,json,shutil
from pathlib import Path
root=Path.cwd()
tmp=Path(tempfile.mkdtemp(prefix='klever-source-check-'))
sdk=Path('C:/Program Files/dotnet/sdk/10.0.201/Roslyn/bincore')
refs=Path('C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/10.0.5/ref/net10.0')
source=r"""
using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
class Program {
 static void Expect(SessionTransition actual,SessionTransition expected) {if(actual!=expected)throw new Exception("Incorrect session transition: "+actual+" expected "+expected);}
 static int Main(string[] files) {
  Expect(SessionControlPolicy.Decide(true,false,false,false,false,false,false,false),SessionTransition.Start);
  Expect(SessionControlPolicy.Decide(true,false,false,false,false,true,false,false),SessionTransition.Restart);
  Expect(SessionControlPolicy.Decide(true,false,false,true,false,true,true,false),SessionTransition.Restart);
  Expect(SessionControlPolicy.Decide(true,false,false,false,false,true,true,false),SessionTransition.Resume);
  Expect(SessionControlPolicy.Decide(true,false,false,false,true,true,false,false),SessionTransition.Resume);
  Expect(SessionControlPolicy.Decide(true,false,true,false,false,true,false,false),SessionTransition.Pause);
  Expect(SessionControlPolicy.Decide(true,true,true,true,false,true,true,false),SessionTransition.Stop);
  Expect(SessionControlPolicy.Decide(true,false,false,true,false,false,false,true),SessionTransition.Restart);
  Expect(SessionControlPolicy.Decide(false,false,false,false,false,true,true,false),SessionTransition.Ignore);
  int errors=0;
  foreach(var symbols in new[]{new string[0],new[]{"UNITY_WEBGL"},new[]{"UNITY_WEBGL","UNITY_EDITOR"}})
   foreach(var file in files) {
    var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(file),new CSharpParseOptions(LanguageVersion.CSharp9,preprocessorSymbols:symbols),file);
    foreach(var diagnostic in tree.GetDiagnostics()) if(diagnostic.Severity==DiagnosticSeverity.Error){Console.WriteLine(diagnostic);errors++;}
   }
  Console.WriteLine("9 real session-policy cases passed; C# syntax checked across editor/WebGL/native symbols. This is not a Unity compile/playback test.");
  return errors==0?0:1;
 }
}
"""
(tmp/'Program.cs').write_text(source,encoding='utf-8')
args=['/nologo','/noconfig','/nostdlib','/langversion:9','/target:exe','/out:'+str(tmp/'Checks.dll')]
args += ['/reference:'+str(file) for file in refs.glob('*.dll')]
for name in ['Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll']:
 args.append('/reference:'+str(sdk/name));shutil.copy2(sdk/name,tmp/name)
args += [str(tmp/'Program.cs'),str(root/'Assets/Scripts/Service/SessionControlPolicy.cs')]
(tmp/'compile.rsp').write_text('\n'.join('"'+arg+'"' for arg in args),encoding='utf-8')
subprocess.run(['dotnet',str(sdk/'csc.dll'),'@'+str(tmp/'compile.rsp')],check=True)
(tmp/'Checks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net10.0','framework':{'name':'Microsoft.NETCore.App','version':'10.0.5'}}}),encoding='utf-8')
files=['Assets/Scripts/Service/DeviceSynHandler.cs','Assets/Scripts/Service/SessionControlPolicy.cs','Assets/Scripts/API/API.cs','Assets/Scripts/MQTT/LiveViewPublisher.cs','Assets/Scripts/MQTT/LiveViewObserver.cs','Assets/NativeWebViewPlugin/Scripts/WebViewController.cs']
subprocess.run(['dotnet',str(tmp/'Checks.dll'),*files],check=True)
