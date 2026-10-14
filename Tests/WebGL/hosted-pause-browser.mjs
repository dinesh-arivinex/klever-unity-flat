import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {readFileSync,mkdtempSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createServer} from 'node:http';
const gameUrl='https://d339ogzg199z0c.cloudfront.net/Burning_of_Magnesium_in_Air/index.html';
const gameOrigin=new URL(gameUrl).origin;
const server=createServer((request,response)=>{
 response.setHeader('Content-Type','text/html');response.setHeader('Referrer-Policy','origin');
 response.end('<!doctype html><title>Hosted pause QA</title><iframe id="game" style="width:1100px;height:800px"></iframe><script>window.acks=[];window.loads=0;const f=document.getElementById("game");f.onload=()=>window.loads++;window.addEventListener("message",e=>{if(e.source===f.contentWindow&&e.origin==='+JSON.stringify(gameOrigin)+')window.acks.push(e.data);});window.control=paused=>f.contentWindow.postMessage({type:"klever:session-control",version:1,paused},'+JSON.stringify(gameOrigin)+');f.src='+JSON.stringify(gameUrl)+';</script>');
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const profile=mkdtempSync(join(tmpdir(),'klever-hosted-pause-browser-'));
const chrome=spawn('C:/Program Files/Google/Chrome/Application/chrome.exe',['--headless','--disable-quic','--no-first-run','--disable-extensions','--remote-debugging-port=0','--user-data-dir='+profile,'about:blank'],{stdio:'ignore',windowsHide:true});
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));let socket;
try{
 let port;
 for(let i=0;i<80;i++){try{port=readFileSync(join(profile,'DevToolsActivePort'),'utf8').split('\n')[0];break;}catch{await delay(100);}}
 assert(port);const info=await(await fetch('http://127.0.0.1:'+port+'/json/version')).json();socket=new WebSocket(info.webSocketDebuggerUrl);
 await new Promise((resolve,reject)=>{socket.onopen=resolve;socket.onerror=reject;});
 let id=0;const pending=new Map();
 socket.onmessage=event=>{const message=JSON.parse(event.data);const task=pending.get(message.id);if(task){pending.delete(message.id);clearTimeout(task.timer);message.error?task.reject(new Error(message.error.message)):task.resolve(message.result);}};
 function cdp(method,params={},sessionId){return new Promise((resolve,reject)=>{const key=++id,timer=setTimeout(()=>{pending.delete(key);reject(new Error('Browser operation timed out'));},15000);pending.set(key,{resolve,reject,timer});socket.send(JSON.stringify({id:key,method,params,...(sessionId?{sessionId}:{})}));});}
 const {targetId}=await cdp('Target.createTarget',{url:'about:blank'});const {sessionId}=await cdp('Target.attachToTarget',{targetId,flatten:true});
 await cdp('Page.enable',{},sessionId);
 const evaluate=async expression=>{const result=await cdp('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true},sessionId);if(result.exceptionDetails)throw new Error('Browser evaluation failed');return result.result.value;};
 const until=async expression=>{for(let n=0;n<160;n++){if(await evaluate(expression))return;await delay(250);}throw new Error('Hosted simulation did not acknowledge pause protocol');};
 await cdp('Page.navigate',{url:'http://127.0.0.1:'+server.address().port+'/'},sessionId);
 await until(`window.acks?.some(value=>value.type==='klever:simulation-ready')`);
 await until(`window.loads===1`);
 await evaluate('control(true)');
 await until(`window.acks.some(value=>value.type==='klever:simulation-pause-ack'&&value.paused===true)`);
 await delay(800);
 await evaluate('control(false)');
 await until(`window.acks.some(value=>value.type==='klever:simulation-pause-ack'&&value.paused===false)`);
 assert.equal(await evaluate('window.loads'),1,'Resume reloaded the hosted simulation');
 console.log(JSON.stringify({actualHostedCreateJsPage:true,crossOriginReady:true,pauseAcknowledged:true,resumeAcknowledged:true,sameDocument:true,unityAppBuildTested:false}));
 await cdp('Browser.close');
}finally{socket?.close();if(chrome.exitCode===null)chrome.kill();await new Promise(resolve=>server.close(resolve));}
