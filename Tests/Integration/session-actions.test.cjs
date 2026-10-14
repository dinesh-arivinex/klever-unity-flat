const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
function setup(){
 const published=[],updates=[];
 const live={_id:'live',schoolId:'school',teacherId:'owner',sessionId:'session',isStart:true,isStop:false,isPaused:true,experienceId:'experience'};
 class ApiError extends Error{constructor(statusCode,message){super(message);this.statusCode=statusCode;}}
 const context={module:{exports:{}},require(name){
  if(name==='http-status')return {BAD_REQUEST:400,CONFLICT:409,SERVICE_UNAVAILABLE:503};
  if(name==='mongoose')return {Types:{ObjectId:{isValid:()=>false}}};
  if(name==='../models')return {liveControl:{findOne:async()=>live,updateOne:async(query,value)=>updates.push({query,value})}};
  if(name==='../utils/ApiError')return ApiError;
  if(name==='../config/logger')return {warn(){}};
  if(name==='../mqtt/topics')return {topics:{sessionControl:school=>'school/'+school}};
  if(name==='../mqtt/client')return {publishJson:async(topic,payload,options)=>published.push({topic,payload,options})};
  throw Error('Unexpected dependency '+name);
 }};
 const source=path.resolve(__dirname,'../../../../../klever-backend/src/services/sessionControl.service.js');
 vm.runInNewContext(fs.readFileSync(source,'utf8'),context,{filename:source});
 return {service:context.module.exports,published,updates};
}
test('identical Start/Resume playback flags publish distinct explicit actions',async()=>{
 const s=setup();const flags={isStart:true,isStop:false,isPaused:false};
 const start=await s.service.updateLiveControl('school',{...flags,action:'start'},{id:'owner',role:'teacher'});
 const resume=await s.service.updateLiveControl('school',{...flags,action:'resume'},{id:'owner',role:'teacher'});
 assert.equal(start.action,'start');assert.equal(resume.action,'resume');assert.equal(s.published.length,2);
 assert(s.published.every(message=>message.options.retain===true&&message.options.qos===1));
 assert(s.updates.every(update=>!('action' in update.value.$set)),'Actions must not become sticky database playback state');
});
test('unknown or inconsistent actions do not publish or write',async()=>{
 const s=setup();
 await assert.rejects(s.service.updateLiveControl('school',{action:'delete'}),error=>error.statusCode===400);
 await assert.rejects(s.service.updateLiveControl('school',{action:'start',isStart:true,isPaused:true}),error=>error.statusCode===400);
 await assert.rejects(s.service.updateLiveControl('school',{action:'pause',isPaused:false}),error=>error.statusCode===400);
 assert.equal(s.published.length,0);assert.equal(s.updates.length,0);
});
test('owner authorization remains authoritative with the new action field',async()=>{
 const s=setup();
 await assert.rejects(s.service.updateLiveControl('school',{action:'resume',isStart:true,isPaused:false},{id:'other',role:'teacher'}),error=>error.statusCode===409);
 assert.equal(s.published.length,0);assert.equal(s.updates.length,0);
});
test('legacy controls without action retain the existing API contract',async()=>{
 const s=setup();const result=await s.service.updateLiveControl('school',{isStart:true,isPaused:false});
 assert(!('action' in result));assert.equal(result.isStart,true);assert.equal(result.isPaused,false);
});
