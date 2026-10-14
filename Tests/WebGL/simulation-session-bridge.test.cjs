const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const path=require('node:path');
function setup(){
 let now=0,id=1;const timeouts=new Map(),frames=new Map(),messages=[],listeners={};
 const media={paused:false,pause(){this.paused=true;},play(){this.paused=false;return Promise.resolve();}};
 const alreadyPaused={paused:true,pause(){},play(){throw Error('Must not start media which was already paused');}};
 const animation={playState:'running',pause(){this.playState='paused';},play(){this.playState='running';}};
 const parent={postMessage(message,origin){messages.push({message,origin});}};
 const performance={now:()=>now};
 class AudioContext {constructor(){this.state='running';}suspend(){this.state='suspended';return Promise.resolve();}resume(){this.state='running';return Promise.resolve();}}
 const window={parent,performance,AudioContext,setTimeout(fn,delay){const key=id++;timeouts.set(key,{fn,due:now+delay});return key;},clearTimeout(key){timeouts.delete(key);},setInterval(){throw Error('Native intervals not expected for functions');},clearInterval(){},requestAnimationFrame(fn){const key=id++;frames.set(key,fn);return key;},cancelAnimationFrame(key){frames.delete(key);},addEventListener(name,fn){listeners[name]=fn;},dispatchEvent(){}};
 const document={referrer:'https://lesson.example/class',currentScript:{getAttribute:()=>null},querySelectorAll:()=>[media,alreadyPaused],getAnimations:()=>[animation]};
 vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../../Assets/StreamingAssets/simulation-session-bridge.js'),'utf8'),{window,document,performance,URL,Map,Reflect,CustomEvent:function(name,options){this.type=name;this.detail=options.detail;}});
 const control=(paused,overrides={})=>listeners.message({source:parent,origin:'https://lesson.example',data:{type:'klever:session-control',version:1,paused},...overrides});
 const advance=(milliseconds)=>{now+=milliseconds;for(const [key,timer]of [...timeouts])if(timer.due<=now){timeouts.delete(key);timer.fn();}};
 const frame=()=>{now+=16;const pending=[...frames];frames.clear();for(const [,fn]of pending)fn(now);};
 return {window,control,advance,frame,performance,media,animation,messages,parent,timeouts,frames};
}
test('pause preserves remaining timer time, simulation clock, frames and media state',()=>{
 const s=setup();let fired=0,frameTime;
 s.window.setTimeout(()=>fired++,100);s.window.requestAnimationFrame(value=>frameTime=value);
 s.advance(40);s.control(true);s.advance(5000);s.frame();
 assert.equal(fired,0);assert.equal(frameTime,undefined);assert.equal(s.performance.now(),40);assert.equal(s.media.paused,true);assert.equal(s.animation.playState,'paused');
 s.control(false);s.advance(59);assert.equal(fired,0);s.advance(1);assert.equal(fired,1);s.frame();
 assert.equal(frameTime,116);assert.equal(s.media.paused,false);assert.equal(s.animation.playState,'running');
});
test('interval cancellation and new timers while paused retain correct state',()=>{
 const s=setup();let ticks=0,late=0;
 const id=s.window.setInterval(()=>ticks++,20);s.advance(10);s.control(true);s.window.clearInterval(id);
 const cancelled=s.window.setTimeout(()=>{throw Error('Cancelled timer ran');},1);s.window.clearTimeout(cancelled);
 s.window.setTimeout(()=>late++,30);s.advance(1000);s.control(false);s.advance(29);assert.equal(late,0);s.advance(1);assert.equal(late,1);assert.equal(ticks,0);
});
test('spoofed parent/source/version messages cannot pause and duplicate control is idempotent',()=>{
 const s=setup();s.control(true,{origin:'https://attacker.example'});assert.equal(s.media.paused,false);
 s.control(true,{source:{}});assert.equal(s.media.paused,false);
 s.control(true,{data:{type:'klever:session-control',version:2,paused:true}});assert.equal(s.media.paused,false);
 s.control(true);s.advance(100);s.control(true);s.advance(100);assert.equal(s.performance.now(),0);s.control(false);assert.equal(s.performance.now(),0);
 assert.equal(s.messages[0].message.type,'klever:simulation-ready');assert(s.messages.every(message=>message.origin==='https://lesson.example'));
});

test('Web Audio pauses existing contexts and defers contexts opened/resumed during pause',async()=>{
 const s=setup();const first=new s.window.AudioContext();s.control(true);assert.equal(first.state,'suspended');
 const second=new s.window.AudioContext();assert.equal(second.state,'suspended');await second.resume();assert.equal(second.state,'suspended');
 s.control(false);assert.equal(first.state,'running');assert.equal(second.state,'running');
});
